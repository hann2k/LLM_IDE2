using LlmIde.Core.Agents;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using System.Text;
using System.Text.Json;

namespace LlmIde.Core.Providers;

/// <summary>
/// Coordinates chat requests with provider settings.
/// </summary>
public sealed class ChatService
{
    /// <summary>
    /// The provider settings store.
    /// </summary>
    private readonly IProviderSettingsStore providerSettingsStore;

    /// <summary>
    /// The provider map.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IChatProvider> providers;

    /// <summary>
    /// The conversation log store.
    /// </summary>
    private readonly IConversationLogStore conversationLogStore;

    /// <summary>
    /// The context builder.
    /// </summary>
    private readonly ContextBuilder contextBuilder;

    /// <summary>
    /// The rolling context store.
    /// </summary>
    private readonly IRollingContextStore rollingContextStore;

    /// <summary>
    /// The artifact service.
    /// </summary>
    private readonly ArtifactService artifactService;

    /// <summary>
    /// The project metadata store.
    /// </summary>
    private readonly IProjectStore projectStore;

    /// <summary>
    /// The agent tool host (enables in-chat tool calls such as fetch_url).
    /// </summary>
    private readonly IAgentToolHost toolHost;

    /// <summary>
    /// The maximum number of tool resolution turns per chat request.
    /// </summary>
    private const int MaxToolTurns = 4;

    /// <summary>
    /// The in-chat tool instruction injected when tools are available.
    /// </summary>
    private const string ToolInstruction =
        "도구가 필요하면 먼저 다른 텍스트 없이 {\"type\":\"list_tools\"} 만 출력해 도구 목록을 요청하라.\n" +
        "tool_list 응답에서 도구 이름과 인자를 확인한 뒤 {\"type\":\"tool_request\",\"tool\":\"<이름>\",\"arguments\":{ ... }} 로 사용하라.\n" +
        "tool_result가 제공되면 그 내용만 근거로 평소 형식대로 답하라. 결과 없이 추측하지 마라.\n" +
        "외부 정보가 필요 없으면 평소대로 바로 답하라.";

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="providerSettingsStore">The provider settings store.</param>
    /// <param name="providers">The available providers.</param>
    /// <param name="conversationLogStore">The conversation log store.</param>
    /// <param name="contextBuilder">The context builder.</param>
    /// <param name="rollingContextStore">The rolling context store.</param>
    /// <param name="artifactService">The artifact service.</param>
    /// <param name="projectStore">The project metadata store.</param>
    /// <param name="toolHost">The agent tool host for in-chat tool calls.</param>
    public ChatService(
        IProviderSettingsStore providerSettingsStore,
        IReadOnlyDictionary<string, IChatProvider> providers,
        IConversationLogStore conversationLogStore,
        ContextBuilder contextBuilder,
        IRollingContextStore rollingContextStore,
        ArtifactService artifactService,
        IProjectStore projectStore,
        IAgentToolHost toolHost)
    {
        this.providerSettingsStore = providerSettingsStore;
        this.providers = providers;
        this.conversationLogStore = conversationLogStore;
        this.contextBuilder = contextBuilder;
        this.rollingContextStore = rollingContextStore;
        this.artifactService = artifactService;
        this.projectStore = projectStore;
        this.toolHost = toolHost;
    }

    /// <summary>
    /// Gets a value indicating whether in-chat tools are available.
    /// </summary>
    private bool ToolsEnabled => toolHost.HasAnyTool;

    /// <summary>
    /// Sends a single user message to the project's default provider.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The user message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="onChatResponseReady">Callback invoked before compression starts.</param>
    /// <param name="onCompressionRequestReady">Callback invoked before compression is sent.</param>
    /// <param name="onCompressionChunk">Callback invoked for every streamed compression chunk.</param>
    /// <returns>The provider response.</returns>
    public async Task<ChatProviderResponse> SendAsync(
        string projectRoot,
        string message,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? artifactIds = null,
        Action<ChatProviderResponse>? onChatResponseReady = null,
        Action<ChatProviderResponse>? onCompressionRequestReady = null,
        Action<string>? onCompressionChunk = null)
    {
        PreparedChatRequest preparedRequest = PrepareRequest(projectRoot, message, artifactIds ?? []);
        StoreRequestStart(projectRoot, preparedRequest, message);
        List<AgentToolResult> toolResults = [];
        ChatProviderResponse response = await ResolveWithToolsAsync(projectRoot, preparedRequest, toolResults, cancellationToken);
        response.SentRequest = preparedRequest.Request;
        response.RequestId = preparedRequest.RequestId;
        response.ToolResults = toolResults;
        ApplyImportanceResult(projectRoot, preparedRequest, response);
        response.ArtifactCandidates = artifactService.ExtractCandidates(response.Content).ToList();

        StoreAssistantMessage(projectRoot, preparedRequest, response);
        onChatResponseReady?.Invoke(response);

        // Skip compression entirely when the conversation importance is 2 or below.
        if (response.ImportanceWeight > 2)
        {
            ApplyCompressionResult(
                response,
                await CompressAfterChatAsync(
                    projectRoot,
                    preparedRequest,
                    message,
                    response.Content,
                    cancellationToken,
                    onCompressionRequestReady,
                    onCompressionChunk));
        }

        return response;
    }

    /// <summary>
    /// Streams a single user message to the project's default provider.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The user message.</param>
    /// <param name="onRequestReady">Callback invoked after the request is prepared.</param>
    /// <param name="onChunk">Callback invoked for every streamed response chunk.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="onCompressionRequestReady">Callback invoked before compression is sent.</param>
    /// <param name="onCompressionChunk">Callback invoked for every streamed compression chunk.</param>
    /// <param name="onAgentStep">Callback invoked after each tool discovery or tool run, with a short label.</param>
    /// <returns>The completed provider response.</returns>
    public async Task<ChatProviderResponse> StreamAsync(
        string projectRoot,
        string message,
        Action<ChatProviderResponse>? onRequestReady,
        Action<string> onChunk,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? artifactIds = null,
        Action<ChatProviderResponse>? onCompressionRequestReady = null,
        Action<string>? onCompressionChunk = null,
        Action<string>? onAgentStep = null)
    {
        PreparedChatRequest preparedRequest = PrepareRequest(projectRoot, message, artifactIds ?? []);
        StoreRequestStart(projectRoot, preparedRequest, message);
        onRequestReady?.Invoke(CreatePreviewResponse(preparedRequest));

        List<AgentToolResult> toolResults = [];
        string finalContent = await StreamWithToolsAsync(
            projectRoot,
            preparedRequest,
            onChunk,
            toolResults,
            onAgentStep,
            cancellationToken);

        ChatProviderResponse response = new ChatProviderResponse
        {
            Content = finalContent,
            Provider = preparedRequest.Settings.Name,
            Model = preparedRequest.Request.Model,
            SentRequest = preparedRequest.Request,
            RequestId = preparedRequest.RequestId,
            ToolResults = toolResults
        };
        ApplyImportanceResult(projectRoot, preparedRequest, response);
        response.ArtifactCandidates = artifactService.ExtractCandidates(response.Content).ToList();

        StoreAssistantMessage(projectRoot, preparedRequest, response);

        // Skip compression entirely when the conversation importance is 2 or below.
        if (response.ImportanceWeight > 2)
        {
            ApplyCompressionResult(
                response,
                await CompressAfterChatAsync(
                    projectRoot,
                    preparedRequest,
                    message,
                    response.Content,
                    cancellationToken,
                    onCompressionRequestReady,
                    onCompressionChunk));
        }

        return response;
    }

    /// <summary>
    /// Sends the request, resolving tool discovery and tool requests, until a final answer is produced.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="toolResults">The collected tool results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The final provider response.</returns>
    private async Task<ChatProviderResponse> ResolveWithToolsAsync(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        List<AgentToolResult> toolResults,
        CancellationToken cancellationToken)
    {
        ChatProviderResponse response = new ChatProviderResponse();
        int[] step = [0];

        for (int turn = 0; turn <= MaxToolTurns; turn++)
        {
            response = await preparedRequest.Provider.SendAsync(
                preparedRequest.Request,
                preparedRequest.Settings,
                cancellationToken);

            (bool handled, _) = await HandleAgentTurnAsync(projectRoot, preparedRequest, response.Content, toolResults, step, cancellationToken);

            if (!handled)
            {
                return response;
            }
        }

        return response;
    }

    /// <summary>
    /// Streams the request, resolving tool discovery and tool requests, until a final answer is produced.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="onChunk">Callback invoked for every streamed chunk.</param>
    /// <param name="toolResults">The collected tool results.</param>
    /// <param name="onAgentStep">Callback invoked after each tool discovery or tool run, with a short label.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The final answer content.</returns>
    private async Task<string> StreamWithToolsAsync(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        Action<string> onChunk,
        List<AgentToolResult> toolResults,
        Action<string>? onAgentStep,
        CancellationToken cancellationToken)
    {
        int[] step = [0];

        for (int turn = 0; turn <= MaxToolTurns; turn++)
        {
            StringBuilder content = new StringBuilder();

            await foreach (string chunk in preparedRequest.Provider.StreamAsync(
                preparedRequest.Request,
                preparedRequest.Settings,
                cancellationToken))
            {
                content.Append(chunk);
                onChunk(chunk);
            }

            string text = content.ToString();
            (bool handled, string? label) = await HandleAgentTurnAsync(projectRoot, preparedRequest, text, toolResults, step, cancellationToken);

            if (!handled)
            {
                return text;
            }

            onAgentStep?.Invoke(label ?? string.Empty);
        }

        return string.Empty;
    }

    /// <summary>
    /// Handles one model turn: tool discovery (list_tools) or a tool request. Returns whether to continue.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="content">The model response content.</param>
    /// <param name="toolResults">The collected tool results.</param>
    /// <param name="step">The single-element step counter shared across turns (for ordered logging).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True with a label when handled (continue); false when the content is a final answer.</returns>
    private async Task<(bool Handled, string? Label)> HandleAgentTurnAsync(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        string content,
        List<AgentToolResult> toolResults,
        int[] step,
        CancellationToken cancellationToken)
    {
        if (!ToolsEnabled)
        {
            return (false, null);
        }

        ParsedModelMessage parsed = AgentMessageParser.Parse(content);

        if (parsed.Kind == ModelMessageKind.ListTools)
        {
            IReadOnlyList<AgentToolDescriptor> catalog = toolHost.ListTools();
            step[0]++;
            LogToolCall(projectRoot, preparedRequest.RequestId, step[0], new AgentToolResult
            {
                Tool = "list_tools",
                RequestId = "list-tools",
                Ok = true,
                Result = catalog
            });
            preparedRequest.Request.Messages.Add(new ChatMessage { Role = "assistant", Content = content });
            preparedRequest.Request.Messages.Add(new ChatMessage { Role = "user", Content = AgentProtocol.ToolList(catalog) });
            return (true, "도구 목록 요청");
        }

        if (parsed.Kind != ModelMessageKind.ToolRequest || !toolHost.HasTool(parsed.Tool))
        {
            return (false, null);
        }

        AgentToolRequest toolRequest = new AgentToolRequest
        {
            Tool = parsed.Tool,
            RequestId = $"tool-{toolResults.Count + 1:000}",
            Arguments = parsed.Arguments
        };

        AgentToolResult toolResult;

        try
        {
            toolResult = await toolHost.ExecuteAsync(toolRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            toolResult = new AgentToolResult
            {
                Tool = toolRequest.Tool,
                RequestId = toolRequest.RequestId,
                Ok = false,
                ErrorMessage = ex.Message
            };
        }

        toolResults.Add(toolResult);
        step[0]++;
        LogToolCall(projectRoot, preparedRequest.RequestId, step[0], toolResult);
        preparedRequest.Request.Messages.Add(new ChatMessage { Role = "assistant", Content = content });
        preparedRequest.Request.Messages.Add(new ChatMessage { Role = "user", Content = AgentProtocol.ToolResult(toolResult) });
        return (true, DescribeStep(toolResult));
    }

    /// <summary>
    /// Records an agent tool execution to the conversation tool call log (sanitized, no secrets).
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The chat request identifier.</param>
    /// <param name="sequence">The execution order within the request.</param>
    /// <param name="toolResult">The tool result.</param>
    private void LogToolCall(string projectRoot, string requestId, int sequence, AgentToolResult toolResult)
    {
        conversationLogStore.AppendToolCall(
            projectRoot,
            ConversationToolCallRecord.Create(requestId, sequence, toolResult, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Builds a short label describing a tool step (host or query only, never secrets).
    /// </summary>
    /// <param name="toolResult">The tool result.</param>
    /// <returns>The step label.</returns>
    private static string DescribeStep(AgentToolResult toolResult)
    {
        if (toolResult.Result is FetchUrlResult fetchResult
            && Uri.TryCreate(fetchResult.Url, UriKind.Absolute, out Uri? uri))
        {
            return $"{toolResult.Tool} 실행: {uri.Host}";
        }

        if (toolResult.Result is WebSearchResult searchResult)
        {
            return $"{toolResult.Tool} 실행: {searchResult.Query}";
        }

        return $"{toolResult.Tool} 실행";
    }

    /// <summary>
    /// Prepares a provider request and associated metadata.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The user message.</param>
    /// <param name="artifactIds">The artifact identifiers to attach.</param>
    /// <returns>The prepared request.</returns>
    private PreparedChatRequest PrepareRequest(
        string projectRoot,
        string message,
        IReadOnlyList<string> artifactIds)
    {
        ProviderSettingsDocument settingsDocument = providerSettingsStore.Load(projectRoot);
        ProviderSettings settings = GetDefaultProviderSettings(settingsDocument);

        if (!providers.TryGetValue(settings.Name, out IChatProvider? provider))
        {
            throw new InvalidOperationException($"Provider is not available: {settings.Name}");
        }

        bool longTerm = projectStore.ReadProjectInfo(projectRoot).LongTermConversation;
        long nextRequestSequence = conversationLogStore.GetNextRequestSequence(projectRoot);
        string requestId = ConversationSequence.ToId(nextRequestSequence);
        string compressionRequestId = requestId + "c";

        // Long-term conversations also send the most recent 5 user turns (request + response) verbatim.
        IReadOnlyList<ConversationMessageRecord> recentMessages = longTerm
            ? GetRecentUserTurns(projectRoot, 5)
            : [];
        ContextBuildResult context = contextBuilder.Build(projectRoot, requestId, message, artifactIds, longTerm, recentMessages);

        ChatProviderRequest request = new ChatProviderRequest
        {
            Model = settings.Model,
            Messages = context.Messages
        };

        // Inject the current time so the model can reason about "now" (before the user message).
        if (request.Messages.Count > 0)
        {
            request.Messages.Insert(request.Messages.Count - 1, new ChatMessage
            {
                Role = "system",
                Content = BuildCurrentTimeMessage()
            });
        }

        // When tools are available, tell the model how to request them (before the user message).
        if (ToolsEnabled && request.Messages.Count > 0)
        {
            request.Messages.Insert(request.Messages.Count - 1, new ChatMessage
            {
                Role = "system",
                Content = ToolInstruction
            });
        }

        return new PreparedChatRequest(
            provider,
            settings,
            request,
            requestId,
            compressionRequestId,
            context.ContextPackage,
            longTerm);
    }

    /// <summary>
    /// Builds the system message that provides the current local date and time to the model.
    /// </summary>
    /// <returns>The current-time system message content.</returns>
    private static string BuildCurrentTimeMessage()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        return "현재 시각: " + now.ToString("yyyy-MM-dd HH:mm:ss zzz") + " (참고용)";
    }

    /// <summary>
    /// Gets the messages of the most recent user-initiated turns.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="turnCount">The number of recent user turns to include.</param>
    /// <returns>The recent turn messages in chronological order.</returns>
    private IReadOnlyList<ConversationMessageRecord> GetRecentUserTurns(string projectRoot, int turnCount)
    {
        IReadOnlyList<ConversationMessageRecord> allMessages = conversationLogStore.GetRecentMessages(projectRoot, int.MaxValue);

        // Walk from newest to oldest and collect the request ids of the last user turns.
        HashSet<string> selectedTurnIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = allMessages.Count - 1; index >= 0 && selectedTurnIds.Count < turnCount; index--)
        {
            ConversationMessageRecord message = allMessages[index];

            if (string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                selectedTurnIds.Add(message.RequestId);
            }
        }

        // Include all messages (user and assistant) of the selected turns in chronological order.
        List<ConversationMessageRecord> window = [];

        foreach (ConversationMessageRecord message in allMessages)
        {
            if (selectedTurnIds.Contains(message.RequestId))
            {
                window.Add(message);
            }
        }

        return window;
    }

    /// <summary>
    /// Stores the request, context package, and user message.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="message">The user message.</param>
    private void StoreRequestStart(string projectRoot, PreparedChatRequest preparedRequest, string message)
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        string contextPackagePath = conversationLogStore.SaveContextPackage(projectRoot, preparedRequest.ContextPackage);

        conversationLogStore.AppendRequest(projectRoot, new ConversationRequestRecord
        {
            RequestId = preparedRequest.RequestId,
            RequestType = "chat",
            Provider = preparedRequest.Settings.Name,
            Model = preparedRequest.Request.Model,
            ContextPackagePath = contextPackagePath,
            UsedRollingContextId = preparedRequest.ContextPackage.UsedRollingContextId,
            RollingContextPath = preparedRequest.ContextPackage.RollingContextPath,
            RecentTurnCount = preparedRequest.ContextPackage.RecentTurnCount,
            CompressionRequestId = preparedRequest.CompressionRequestId,
            CompressionStatus = "pending",
            CreatedAt = createdAt
        });

        conversationLogStore.AppendMessage(projectRoot, new ConversationMessageRecord
        {
            MessageId = ConversationSequence.ToId(conversationLogStore.GetNextMessageSequence(projectRoot)),
            RequestId = preparedRequest.RequestId,
            Role = "user",
            Content = message,
            Provider = preparedRequest.Settings.Name,
            Model = preparedRequest.Request.Model,
            CreatedAt = createdAt
        });
    }

    /// <summary>
    /// Runs rolling context compression after a successful chat response.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="userMessage">The current user message.</param>
    /// <param name="assistantMessage">The assistant response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="onCompressionRequestReady">Callback invoked before compression is sent.</param>
    /// <param name="onCompressionChunk">Callback invoked for every streamed compression chunk.</param>
    private async Task<CompressionResult> CompressAfterChatAsync(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken,
        Action<ChatProviderResponse>? onCompressionRequestReady,
        Action<string>? onCompressionChunk)
    {
        string previousRollingContextId = preparedRequest.ContextPackage.UsedRollingContextId;
        string compressionRule = rollingContextStore.LoadCompressionRule(projectRoot);
        string compressionPrompt = BuildCompressionPrompt(projectRoot, preparedRequest, userMessage, assistantMessage);
        IReadOnlyList<string> activeCriteria = preparedRequest.ContextPackage.ActiveCriteria;
        string activeCriteriaMessage = BuildActiveCriteriaMessage(activeCriteria);
        List<ChatMessage> compressionMessages = [];

        // Always send active criteria with the compression request.
        if (!string.IsNullOrWhiteSpace(activeCriteriaMessage))
        {
            compressionMessages.Add(new ChatMessage
            {
                Role = "system",
                Content = activeCriteriaMessage
            });
        }

        compressionMessages.Add(new ChatMessage
        {
            Role = "system",
            Content = compressionRule
        });
        compressionMessages.Add(new ChatMessage
        {
            Role = "user",
            Content = compressionPrompt
        });
        ChatProviderRequest compressionRequest = new ChatProviderRequest
        {
            Model = preparedRequest.Request.Model,
            Messages = compressionMessages
        };
        string compressionContextPath = conversationLogStore.SaveContextPackage(
            projectRoot,
            CreateCompressionContextPackage(
                preparedRequest.CompressionRequestId,
                compressionRule,
                compressionPrompt,
                preparedRequest.ContextPackage.RollingContextSummary,
                activeCriteria));
        onCompressionRequestReady?.Invoke(CreateCompressionPreviewResponse(preparedRequest, compressionRequest));

        try
        {
            string compressionContent = await SendCompressionAsync(
                preparedRequest,
                compressionRequest,
                cancellationToken,
                onCompressionChunk);
            RollingContextSummary summary = rollingContextStore.SaveCompleted(
                projectRoot,
                preparedRequest.RequestId,
                string.IsNullOrWhiteSpace(previousRollingContextId) ? null : previousRollingContextId,
                compressionContent,
                preparedRequest.Settings.Name,
                preparedRequest.Request.Model);

            conversationLogStore.AppendRequest(projectRoot, new ConversationRequestRecord
            {
                RequestId = preparedRequest.CompressionRequestId,
                RequestType = "compression",
                SourceChatRequestId = preparedRequest.RequestId,
                Provider = preparedRequest.Settings.Name,
                Model = preparedRequest.Request.Model,
                ContextPackagePath = compressionContextPath,
                UsedRollingContextId = previousRollingContextId,
                RollingContextPath = summary.ContentPath,
                Status = "completed",
                CreatedAt = summary.CreatedAt
            });
            return new CompressionResult(
                preparedRequest.CompressionRequestId,
                compressionRequest,
                compressionContent,
                "completed",
                string.Empty);
        }
        catch (Exception ex)
        {
            RollingContextSummary failedSummary = rollingContextStore.SaveFailed(
                projectRoot,
                preparedRequest.RequestId,
                string.IsNullOrWhiteSpace(previousRollingContextId) ? null : previousRollingContextId,
                preparedRequest.Settings.Name,
                preparedRequest.Request.Model,
                ex.Message);

            conversationLogStore.AppendRequest(projectRoot, new ConversationRequestRecord
            {
                RequestId = preparedRequest.CompressionRequestId,
                RequestType = "compression",
                SourceChatRequestId = preparedRequest.RequestId,
                Provider = preparedRequest.Settings.Name,
                Model = preparedRequest.Request.Model,
                ContextPackagePath = compressionContextPath,
                UsedRollingContextId = previousRollingContextId,
                Status = "failed",
                Error = ex.Message,
                CreatedAt = failedSummary.CreatedAt
            });
            return new CompressionResult(
                preparedRequest.CompressionRequestId,
                compressionRequest,
                string.Empty,
                "failed",
                ex.Message);
        }
    }

    /// <summary>
    /// Sends compression either as a streaming request or a normal request.
    /// </summary>
    /// <param name="preparedRequest">The prepared chat request.</param>
    /// <param name="compressionRequest">The compression provider request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="onCompressionChunk">Callback invoked for streamed compression chunks.</param>
    /// <returns>The full compression content.</returns>
    private static async Task<string> SendCompressionAsync(
        PreparedChatRequest preparedRequest,
        ChatProviderRequest compressionRequest,
        CancellationToken cancellationToken,
        Action<string>? onCompressionChunk)
    {
        if (onCompressionChunk is null)
        {
            ChatProviderResponse compressionResponse = await preparedRequest.Provider.SendAsync(
                compressionRequest,
                preparedRequest.Settings,
                cancellationToken);
            return compressionResponse.Content;
        }

        StringBuilder content = new StringBuilder();

        await foreach (string chunk in preparedRequest.Provider.StreamAsync(
            compressionRequest,
            preparedRequest.Settings,
            cancellationToken))
        {
            content.Append(chunk);
            onCompressionChunk(chunk);
        }

        return content.ToString();
    }

    /// <summary>
    /// Applies compression debug information to a response.
    /// </summary>
    /// <param name="response">The chat response.</param>
    /// <param name="compressionResult">The compression result.</param>
    private static void ApplyCompressionResult(ChatProviderResponse response, CompressionResult compressionResult)
    {
        response.CompressionRequestId = compressionResult.RequestId;
        response.CompressionRequest = compressionResult.Request;
        response.CompressionContent = compressionResult.Content;
        response.CompressionStatus = compressionResult.Status;
        response.CompressionError = compressionResult.Error;
    }

    /// <summary>
    /// Applies importance metadata to the response and stored request.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="response">The provider response.</param>
    private void ApplyImportanceResult(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        ChatProviderResponse response)
    {
        ConversationImportanceParseResult importanceResult = ConversationImportanceParser.Parse(response.Content);
        response.Content = importanceResult.Content;

        // One-time conversations fix every importance weight to 0.
        response.ImportanceWeight = preparedRequest.LongTerm ? importanceResult.ImportanceWeight : 0;
        conversationLogStore.UpdateRequestImportanceWeight(
            projectRoot,
            preparedRequest.RequestId,
            response.ImportanceWeight);
    }

    /// <summary>
    /// Builds the compression prompt.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="userMessage">The current user message.</param>
    /// <param name="assistantMessage">The current assistant message.</param>
    /// <returns>The compression prompt.</returns>
    private string BuildCompressionPrompt(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        string userMessage,
        string assistantMessage)
    {
        if (string.IsNullOrWhiteSpace(preparedRequest.ContextPackage.RollingContextSummary))
        {
            return BuildBootstrapCompressionPrompt(projectRoot);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[이전 롤링 맥락 요약]");
        builder.AppendLine(preparedRequest.ContextPackage.RollingContextSummary);
        builder.AppendLine();
        builder.AppendLine("[현재 사용자 메시지]");
        builder.AppendLine(userMessage);
        builder.AppendLine();
        builder.AppendLine("[현재 AI 응답]");
        builder.AppendLine(assistantMessage);
        builder.AppendLine();
        builder.AppendLine("[작업]");
        builder.AppendLine("위 내용을 병합하여 다음 요청에 사용할 롤링 맥락 요약을 갱신하라.");
        return builder.ToString();
    }

    /// <summary>
    /// Builds the first compression prompt from the accumulated raw conversation log.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The bootstrap compression prompt.</returns>
    private string BuildBootstrapCompressionPrompt(string projectRoot)
    {
        IReadOnlyList<ConversationMessageRecord> messages = conversationLogStore.GetRecentMessages(projectRoot, int.MaxValue);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[원본 대화 로그]");

        foreach (ConversationMessageRecord message in messages)
        {
            builder.Append('[');
            builder.Append(message.CreatedAt.ToString("O"));
            builder.Append("] ");
            builder.Append(message.Role);
            builder.Append(": ");
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        builder.AppendLine("[작업]");
        builder.AppendLine("위 전체 원본 대화 로그를 다음 요청에 사용할 롤링 맥락 요약으로 압축하라.");
        return builder.ToString();
    }

    /// <summary>
    /// Creates a context package for a compression request.
    /// </summary>
    /// <param name="requestId">The compression request identifier.</param>
    /// <param name="compressionRule">The compression rule.</param>
    /// <param name="compressionPrompt">The compression prompt.</param>
    /// <param name="previousSummary">The previous rolling summary.</param>
    /// <returns>The compression context package.</returns>
    private static ContextPackage CreateCompressionContextPackage(
        string requestId,
        string compressionRule,
        string compressionPrompt,
        string previousSummary,
        IReadOnlyList<string> activeCriteria)
    {
        return new ContextPackage
        {
            RequestId = requestId,
            SystemRule = compressionRule,
            ActiveCriteria = activeCriteria.ToList(),
            RollingContextSummary = previousSummary,
            UserRequest = compressionPrompt,
            AttachedMessages =
            [
                $"system: {compressionRule}",
                $"user: {compressionPrompt}"
            ]
        };
    }

    /// <summary>
    /// Builds the active criteria system message.
    /// </summary>
    /// <param name="activeCriteria">The active criteria.</param>
    /// <returns>The active criteria system message.</returns>
    private static string BuildActiveCriteriaMessage(IReadOnlyList<string> activeCriteria)
    {
        if (activeCriteria.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("다음 활성 프로젝트 기준을 따른다.");

        foreach (string criterion in activeCriteria)
        {
            builder.Append("- ");
            builder.AppendLine(criterion);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Stores the assistant response.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="response">The provider response.</param>
    private void StoreAssistantMessage(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        ChatProviderResponse response)
    {
        conversationLogStore.AppendMessage(projectRoot, new ConversationMessageRecord
        {
            MessageId = ConversationSequence.ToId(conversationLogStore.GetNextMessageSequence(projectRoot)),
            RequestId = preparedRequest.RequestId,
            Role = "assistant",
            Content = response.Content,
            Provider = response.Provider,
            Model = response.Model,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Creates a response object for request debug output.
    /// </summary>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <returns>The preview response.</returns>
    private static ChatProviderResponse CreatePreviewResponse(PreparedChatRequest preparedRequest)
    {
        return new ChatProviderResponse
        {
            Provider = preparedRequest.Settings.Name,
            Model = preparedRequest.Request.Model,
            SentRequest = preparedRequest.Request,
            RequestId = preparedRequest.RequestId
        };
    }

    /// <summary>
    /// Creates a response object for compression request debug output.
    /// </summary>
    /// <param name="preparedRequest">The prepared chat request.</param>
    /// <param name="compressionRequest">The compression provider request.</param>
    /// <returns>The compression preview response.</returns>
    private static ChatProviderResponse CreateCompressionPreviewResponse(
        PreparedChatRequest preparedRequest,
        ChatProviderRequest compressionRequest)
    {
        return new ChatProviderResponse
        {
            Provider = preparedRequest.Settings.Name,
            Model = compressionRequest.Model,
            CompressionRequestId = preparedRequest.CompressionRequestId,
            CompressionRequest = compressionRequest,
            CompressionStatus = "streaming"
        };
    }

    /// <summary>
    /// Gets the default provider settings.
    /// </summary>
    /// <param name="settingsDocument">The settings document.</param>
    /// <returns>The default provider settings.</returns>
    private static ProviderSettings GetDefaultProviderSettings(ProviderSettingsDocument settingsDocument)
    {
        ProviderSettings? settings = settingsDocument.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Name, settingsDocument.DefaultProvider, StringComparison.OrdinalIgnoreCase));

        if (settings is null)
        {
            throw new InvalidOperationException($"Provider settings are missing: {settingsDocument.DefaultProvider}");
        }

        return settings;
    }

    /// <summary>
    /// Represents a prepared chat request with storage metadata.
    /// </summary>
    /// <param name="Provider">The chat provider.</param>
    /// <param name="Settings">The provider settings.</param>
    /// <param name="Request">The provider request.</param>
    /// <param name="RequestId">The request identifier.</param>
    /// <param name="CompressionRequestId">The compression request identifier.</param>
    /// <param name="ContextPackage">The context package.</param>
    /// <param name="LongTerm">Whether the project uses long-term conversations.</param>
    private sealed record PreparedChatRequest(
        IChatProvider Provider,
        ProviderSettings Settings,
        ChatProviderRequest Request,
        string RequestId,
        string CompressionRequestId,
        ContextPackage ContextPackage,
        bool LongTerm)
    {
    }

    /// <summary>
    /// Represents a completed compression attempt.
    /// </summary>
    /// <param name="RequestId">The compression request identifier.</param>
    /// <param name="Request">The compression provider request.</param>
    /// <param name="Content">The compression response content.</param>
    /// <param name="Status">The compression status.</param>
    /// <param name="Error">The compression error.</param>
    private sealed record CompressionResult(
        string RequestId,
        ChatProviderRequest Request,
        string Content,
        string Status,
        string Error);
}
