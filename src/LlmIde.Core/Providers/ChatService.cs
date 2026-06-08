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
        "외부 정보가 필요하면 다른 텍스트 없이 아래 JSON 중 하나만 출력하라.\n" +
        "- 웹 검색: {\"type\":\"tool_request\",\"tool\":\"web_search\",\"arguments\":{\"query\":\"검색어\",\"maxResults\":5}}\n" +
        "- URL 본문: {\"type\":\"tool_request\",\"tool\":\"fetch_url\",\"arguments\":{\"url\":\"https://...\",\"maxChars\":12000}}\n" +
        "무엇을 찾아 달라는 요청은 보통 먼저 web_search로 검색하고, 필요하면 fetch_url로 본문을 가져온다.\n" +
        "도구 결과(tool_result)가 제공되면 그 내용만 근거로 평소 형식대로 답하라.\n" +
        "도구 결과 없이 추측하지 마라. 외부 정보가 필요 없으면 평소대로 바로 답하라.";

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
        ChatProviderResponse response = await ResolveWithToolsAsync(preparedRequest, toolResults, null, cancellationToken);
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
    /// <param name="onToolExecuted">Callback invoked after each in-chat tool runs.</param>
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
        Action<AgentToolResult>? onToolExecuted = null)
    {
        PreparedChatRequest preparedRequest = PrepareRequest(projectRoot, message, artifactIds ?? []);
        StoreRequestStart(projectRoot, preparedRequest, message);
        onRequestReady?.Invoke(CreatePreviewResponse(preparedRequest));

        List<AgentToolResult> toolResults = [];
        string finalContent = await StreamWithToolsAsync(
            preparedRequest,
            onChunk,
            toolResults,
            onToolExecuted,
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
    /// Sends the request, resolving any tool requests, until a final (non-tool) answer is produced.
    /// </summary>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="toolResults">The collected tool results.</param>
    /// <param name="onToolExecuted">Callback invoked after each tool runs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The final provider response.</returns>
    private async Task<ChatProviderResponse> ResolveWithToolsAsync(
        PreparedChatRequest preparedRequest,
        List<AgentToolResult> toolResults,
        Action<AgentToolResult>? onToolExecuted,
        CancellationToken cancellationToken)
    {
        ChatProviderResponse response = new ChatProviderResponse();

        for (int turn = 0; turn <= MaxToolTurns; turn++)
        {
            response = await preparedRequest.Provider.SendAsync(
                preparedRequest.Request,
                preparedRequest.Settings,
                cancellationToken);

            if (!await TryRunToolAsync(preparedRequest, response.Content, toolResults, onToolExecuted, cancellationToken))
            {
                return response;
            }
        }

        return response;
    }

    /// <summary>
    /// Streams the request, resolving any tool requests, until a final (non-tool) answer is produced.
    /// </summary>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="onChunk">Callback invoked for every streamed chunk.</param>
    /// <param name="toolResults">The collected tool results.</param>
    /// <param name="onToolExecuted">Callback invoked after each tool runs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The final answer content.</returns>
    private async Task<string> StreamWithToolsAsync(
        PreparedChatRequest preparedRequest,
        Action<string> onChunk,
        List<AgentToolResult> toolResults,
        Action<AgentToolResult>? onToolExecuted,
        CancellationToken cancellationToken)
    {
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

            if (!await TryRunToolAsync(preparedRequest, text, toolResults, onToolExecuted, cancellationToken))
            {
                return text;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Detects a tool request, runs it, and appends the tool result to the conversation messages.
    /// </summary>
    /// <param name="preparedRequest">The prepared request.</param>
    /// <param name="content">The model response content.</param>
    /// <param name="toolResults">The collected tool results.</param>
    /// <param name="onToolExecuted">Callback invoked after the tool runs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when a tool was run; false when the content is a final answer.</returns>
    private async Task<bool> TryRunToolAsync(
        PreparedChatRequest preparedRequest,
        string content,
        List<AgentToolResult> toolResults,
        Action<AgentToolResult>? onToolExecuted,
        CancellationToken cancellationToken)
    {
        if (!ToolsEnabled)
        {
            return false;
        }

        ParsedModelMessage parsed = AgentMessageParser.Parse(content);

        if (parsed.Kind != ModelMessageKind.ToolRequest || !toolHost.HasTool(parsed.Tool))
        {
            return false;
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
        preparedRequest.Request.Messages.Add(new ChatMessage { Role = "assistant", Content = content });
        preparedRequest.Request.Messages.Add(new ChatMessage { Role = "user", Content = BuildToolResultEnvelope(toolResult) });
        onToolExecuted?.Invoke(toolResult);
        return true;
    }

    /// <summary>
    /// Builds the tool_result envelope JSON fed back to the model.
    /// </summary>
    /// <param name="toolResult">The tool result.</param>
    /// <returns>The tool_result JSON.</returns>
    private static string BuildToolResultEnvelope(AgentToolResult toolResult)
    {
        object payload = toolResult.Result ?? new { ok = toolResult.Ok, errorMessage = toolResult.ErrorMessage };
        var envelope = new
        {
            type = "tool_result",
            tool = toolResult.Tool,
            requestId = toolResult.RequestId,
            result = payload
        };
        return JsonSerializer.Serialize(envelope, AgentJson.Options);
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
