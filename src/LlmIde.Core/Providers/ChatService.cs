using LlmIde.Core.Agents;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Conversations;
using LlmIde.Core.Diagnostics;
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
    /// The diagnostic logger that records every actual LLM provider call (injected prompt and raw response).
    /// </summary>
    private readonly ILlmRequestLogger llmRequestLogger;

    /// <summary>
    /// The diagnostic logger that records every tool execution's full input and output.
    /// </summary>
    private readonly IToolIoLogger toolIoLogger;

    /// <summary>
    /// The prompt template store for the in-chat tool instruction and compression prompts (null falls back to built-in defaults).
    /// </summary>
    private readonly IPromptStore? promptStore;

    /// <summary>
    /// The fallback intro before the active project criteria in a compression request.
    /// </summary>
    private const string DefaultCompressionCriteriaIntro = "다음 활성 프로젝트 기준을 따른다.";

    /// <summary>
    /// The fallback compression prompt merging the previous summary with the current turn.
    /// </summary>
    private const string DefaultCompressionMerge =
        "[이전 롤링 맥락 요약]\n{previous_summary}\n\n" +
        "[현재 사용자 메시지]\n{user_message}\n\n" +
        "[현재 AI 응답]\n{assistant_message}\n\n" +
        "[작업]\n위 내용을 병합하여 다음 요청에 사용할 롤링 맥락 요약을 갱신하라.";

    /// <summary>
    /// The fallback bootstrap compression prompt summarizing the raw conversation log.
    /// </summary>
    private const string DefaultCompressionBootstrap =
        "[원본 대화 로그]\n{conversation_log}[작업]\n위 전체 원본 대화 로그를 다음 요청에 사용할 롤링 맥락 요약으로 압축하라.";

    /// <summary>
    /// The maximum number of tool resolution turns per chat request.
    /// </summary>
    private const int MaxToolTurns = 4;

    /// <summary>
    /// The fallback in-chat tool instruction used when no external prompt is available
    /// (the supervisor-editable text lives under the <c>chat_tool_instruction</c> key in policies/prompts.json).
    /// </summary>
    private const string DefaultChatToolInstruction =
        "너는 web_search(실시간 웹 검색)와 fetch_url(URL 본문 가져오기) 도구를 쓸 수 있다.\n" +
        "검색·최신 정보·가격·재고·실시간 사실 확인이 필요한 요청에는 반드시 도구를 사용하라. \"실시간 검색을 할 수 없다\"거나 \"직접 확인할 수 없다\"고 거절하지 마라.\n" +
        "도구 목록이 필요하면 다른 텍스트 없이 {\"type\":\"list_tools\"} 만 출력하라.\n" +
        "도구를 호출할 때는 반드시 아래 형식으로만 출력하라. 도구가 하나여도 requests 배열에 담고, 여러 도구가 필요하면 한 응답에 모두 배열로 담아라.\n" +
        "{\"type\":\"tool_request\",\"requests\":[{\"tool\":\"<이름>\",\"arguments\":{ ... }},{\"tool\":\"<이름>\",\"arguments\":{ ... }}]}\n" +
        "JSON 객체를 여러 개 따로 출력하지 마라. 반드시 requests 배열 하나로만 출력하라.\n" +
        "tool_result의 results 배열 각 항목만 근거로 평소 형식대로 자연스럽게 답하라. 결과 없이 추측하지 마라.\n" +
        "도구가 정말 필요 없는 일반 대화나 의견 요청이면 평소대로 바로 답하라.";

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
    /// <param name="llmRequestLogger">The diagnostic logger for actual LLM calls (defaults to a no-op logger).</param>
    /// <param name="promptStore">The prompt template store for the tool instruction and compression prompts (defaults to built-in prompts).</param>
    /// <param name="toolIoLogger">The diagnostic logger for tool execution input/output (defaults to a no-op logger).</param>
    public ChatService(
        IProviderSettingsStore providerSettingsStore,
        IReadOnlyDictionary<string, IChatProvider> providers,
        IConversationLogStore conversationLogStore,
        ContextBuilder contextBuilder,
        IRollingContextStore rollingContextStore,
        ArtifactService artifactService,
        IProjectStore projectStore,
        IAgentToolHost toolHost,
        ILlmRequestLogger? llmRequestLogger = null,
        IPromptStore? promptStore = null,
        IToolIoLogger? toolIoLogger = null)
    {
        this.providerSettingsStore = providerSettingsStore;
        this.providers = providers;
        this.conversationLogStore = conversationLogStore;
        this.contextBuilder = contextBuilder;
        this.rollingContextStore = rollingContextStore;
        this.artifactService = artifactService;
        this.projectStore = projectStore;
        this.toolHost = toolHost;
        this.llmRequestLogger = llmRequestLogger ?? NullLlmRequestLogger.Instance;
        this.promptStore = promptStore;
        this.toolIoLogger = toolIoLogger ?? NullToolIoLogger.Instance;
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
    /// Records a manually entered conversation (user + assistant) as a completed turn, without
    /// calling any provider. Useful for importing a conversation pasted from another session.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="userText">The user message text.</param>
    /// <param name="assistantText">The assistant message text.</param>
    /// <returns>The new conversation request identifier.</returns>
    public string AddManualConversation(string projectRoot, string userText, string assistantText)
    {
        string requestId = ConversationSequence.ToId(conversationLogStore.GetNextRequestSequence(projectRoot));
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        conversationLogStore.AppendRequest(projectRoot, new ConversationRequestRecord
        {
            RequestId = requestId,
            RequestType = "manual",
            CompressionStatus = "skipped",
            Status = "completed",
            CreatedAt = createdAt
        });

        conversationLogStore.AppendMessage(projectRoot, new ConversationMessageRecord
        {
            MessageId = ConversationSequence.ToId(conversationLogStore.GetNextMessageSequence(projectRoot)),
            RequestId = requestId,
            Role = "user",
            Content = userText,
            CreatedAt = createdAt
        });

        conversationLogStore.AppendMessage(projectRoot, new ConversationMessageRecord
        {
            MessageId = ConversationSequence.ToId(conversationLogStore.GetNextMessageSequence(projectRoot)),
            RequestId = requestId,
            Role = "assistant",
            Content = assistantText,
            CreatedAt = createdAt
        });

        return requestId;
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
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            IReadOnlyList<LlmRequestLogMessage> sentMessages = SnapshotMessages(preparedRequest.Request.Messages);

            try
            {
                response = await preparedRequest.Provider.SendAsync(
                    preparedRequest.Request,
                    preparedRequest.Settings,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                LogLlmCall(preparedRequest.RequestId, "chat", turn, preparedRequest, false, sentMessages, string.Empty, startedAt, ex.Message);
                throw;
            }

            LogLlmCall(preparedRequest.RequestId, "chat", turn, preparedRequest, false, sentMessages, response.Content, startedAt, null);

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
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            IReadOnlyList<LlmRequestLogMessage> sentMessages = SnapshotMessages(preparedRequest.Request.Messages);

            try
            {
                await foreach (string chunk in preparedRequest.Provider.StreamAsync(
                    preparedRequest.Request,
                    preparedRequest.Settings,
                    cancellationToken))
                {
                    content.Append(chunk);
                    onChunk(chunk);
                }
            }
            catch (Exception ex)
            {
                LogLlmCall(preparedRequest.RequestId, "chat", turn, preparedRequest, true, sentMessages, content.ToString(), startedAt, ex.Message);
                throw;
            }

            string text = content.ToString();
            LogLlmCall(preparedRequest.RequestId, "chat", turn, preparedRequest, true, sentMessages, text, startedAt, null);

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

        if (parsed.Kind != ModelMessageKind.ToolRequest)
        {
            return (false, null);
        }

        // Assign tool ids up front (stable order), run the whole batch concurrently, then collect results
        // in request order and send them back together in one tool_result message. Partial failure is
        // tolerated: an unknown or throwing tool becomes an error result without aborting the batch.
        int baseCount = toolResults.Count;
        List<Task<AgentToolResult>> executions = [];

        for (int index = 0; index < parsed.Requests.Count; index++)
        {
            string toolRequestId = $"tool-{baseCount + index + 1:000}";
            executions.Add(ExecuteToolAsync(preparedRequest.RequestId, parsed.Requests[index], toolRequestId, cancellationToken));
        }

        AgentToolResult[] batchResults = await Task.WhenAll(executions);

        foreach (AgentToolResult toolResult in batchResults)
        {
            toolResults.Add(toolResult);
            step[0]++;
            LogToolCall(projectRoot, preparedRequest.RequestId, step[0], toolResult);
        }

        preparedRequest.Request.Messages.Add(new ChatMessage { Role = "assistant", Content = content });
        preparedRequest.Request.Messages.Add(new ChatMessage { Role = "user", Content = AgentProtocol.ToolResults(batchResults) });
        return (true, DescribeBatch(batchResults));
    }

    /// <summary>
    /// Executes one tool request (tolerating unknown/throwing tools as error results) and records its
    /// full input and output to the tool I/O diagnostic log.
    /// </summary>
    /// <param name="chatRequestId">The chat request identifier.</param>
    /// <param name="request">The parsed tool request.</param>
    /// <param name="toolRequestId">The tool execution identifier (tool-NNN).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result (never throws).</returns>
    private async Task<AgentToolResult> ExecuteToolAsync(
        string chatRequestId,
        ParsedToolRequest request,
        string toolRequestId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        AgentToolResult toolResult;

        if (!toolHost.HasTool(request.Tool))
        {
            toolResult = new AgentToolResult
            {
                Tool = request.Tool,
                RequestId = toolRequestId,
                Ok = false,
                ErrorMessage = $"알 수 없는 도구입니다: {request.Tool}"
            };
        }
        else
        {
            try
            {
                toolResult = await toolHost.ExecuteAsync(
                    new AgentToolRequest
                    {
                        Tool = request.Tool,
                        RequestId = toolRequestId,
                        Arguments = request.Arguments
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                toolResult = new AgentToolResult
                {
                    Tool = request.Tool,
                    RequestId = toolRequestId,
                    Ok = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // Record the full tool input and output (no summary, no truncation) to SystemLog at [Debug].
        toolIoLogger.Log(new ToolIoLogRecord
        {
            RequestId = chatRequestId,
            ToolRequestId = toolRequestId,
            Tool = request.Tool,
            Arguments = request.Arguments,
            Result = toolResult.Result,
            Ok = toolResult.Ok,
            Error = toolResult.ErrorMessage,
            StartedAt = startedAt,
            DurationMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
        });

        return toolResult;
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
    /// Records one actual LLM provider call (the exact injected prompt and the raw response) to the diagnostic logger.
    /// </summary>
    /// <param name="requestId">The conversation request identifier (or compression request identifier).</param>
    /// <param name="purpose">The call purpose ("chat" or "compression").</param>
    /// <param name="turn">The zero-based turn index within the agent loop.</param>
    /// <param name="preparedRequest">The prepared request (provides provider and model).</param>
    /// <param name="stream">Whether the call was a streaming call.</param>
    /// <param name="messages">The snapshot of messages sent to the model.</param>
    /// <param name="response">The raw model response (an empty string is recorded verbatim).</param>
    /// <param name="startedAt">The time the call started.</param>
    /// <param name="error">The error message when the call failed, or null on success.</param>
    private void LogLlmCall(
        string requestId,
        string purpose,
        int turn,
        PreparedChatRequest preparedRequest,
        bool stream,
        IReadOnlyList<LlmRequestLogMessage> messages,
        string response,
        DateTimeOffset startedAt,
        string? error)
    {
        string safeResponse = response ?? string.Empty;
        llmRequestLogger.Log(new LlmRequestLogRecord
        {
            RequestId = requestId,
            Purpose = purpose,
            Turn = turn,
            Provider = preparedRequest.Settings.Name,
            Model = preparedRequest.Request.Model,
            Stream = stream,
            Messages = messages,
            Response = safeResponse,
            ResponseChars = safeResponse.Length,
            StartedAt = startedAt,
            DurationMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            Error = error ?? string.Empty
        });
    }

    /// <summary>
    /// Copies the current messages into an immutable snapshot for logging (so later mutation of the
    /// request's message list does not change what was recorded).
    /// </summary>
    /// <param name="messages">The messages to snapshot.</param>
    /// <returns>The message snapshot.</returns>
    private static IReadOnlyList<LlmRequestLogMessage> SnapshotMessages(IReadOnlyList<ChatMessage> messages)
    {
        List<LlmRequestLogMessage> snapshot = new List<LlmRequestLogMessage>(messages.Count);

        foreach (ChatMessage message in messages)
        {
            snapshot.Add(new LlmRequestLogMessage
            {
                Role = message.Role,
                Content = message.Content
            });
        }

        return snapshot;
    }

    /// <summary>
    /// Builds a short label describing a tool batch (one step label, or a count and tool names for many).
    /// </summary>
    /// <param name="results">The batch tool results.</param>
    /// <returns>The batch label.</returns>
    private static string DescribeBatch(IReadOnlyList<AgentToolResult> results)
    {
        if (results.Count == 1)
        {
            return DescribeStep(results[0]);
        }

        return $"도구 {results.Count}개 실행: " + string.Join(", ", results.Select(result => result.Tool));
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
                Content = ResolveToolInstruction(projectRoot)
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
    /// Resolves the in-chat tool instruction from the prompt store (chat_tool_instruction key),
    /// falling back to the built-in default when no store is configured or the key is empty.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The tool instruction text to inject.</returns>
    private string ResolveToolInstruction(string projectRoot)
    {
        IReadOnlyDictionary<string, string> prompts = promptStore?.Load(projectRoot)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return ResolvePrompt(prompts, PromptKeys.ChatToolInstruction, DefaultChatToolInstruction);
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
        IReadOnlyDictionary<string, string> prompts = promptStore?.Load(projectRoot)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        string compressionPrompt = BuildCompressionPrompt(projectRoot, preparedRequest, userMessage, assistantMessage, prompts);
        IReadOnlyList<string> activeCriteria = preparedRequest.ContextPackage.ActiveCriteria;
        string activeCriteriaMessage = BuildActiveCriteriaMessage(
            activeCriteria,
            ResolvePrompt(prompts, PromptKeys.CompressionCriteriaIntro, DefaultCompressionCriteriaIntro));
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

        DateTimeOffset compressionStartedAt = DateTimeOffset.UtcNow;
        IReadOnlyList<LlmRequestLogMessage> compressionSentMessages = SnapshotMessages(compressionRequest.Messages);

        try
        {
            string compressionContent = await SendCompressionAsync(
                preparedRequest,
                compressionRequest,
                cancellationToken,
                onCompressionChunk);
            LogLlmCall(
                preparedRequest.CompressionRequestId,
                "compression",
                0,
                preparedRequest,
                onCompressionChunk is not null,
                compressionSentMessages,
                compressionContent,
                compressionStartedAt,
                null);
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
            LogLlmCall(
                preparedRequest.CompressionRequestId,
                "compression",
                0,
                preparedRequest,
                onCompressionChunk is not null,
                compressionSentMessages,
                string.Empty,
                compressionStartedAt,
                ex.Message);
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
    /// <param name="prompts">The loaded prompt templates.</param>
    /// <returns>The compression prompt.</returns>
    private string BuildCompressionPrompt(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        string userMessage,
        string assistantMessage,
        IReadOnlyDictionary<string, string> prompts)
    {
        if (string.IsNullOrWhiteSpace(preparedRequest.ContextPackage.RollingContextSummary))
        {
            return BuildBootstrapCompressionPrompt(projectRoot, prompts);
        }

        return ResolvePrompt(prompts, PromptKeys.CompressionMerge, DefaultCompressionMerge)
            .Replace("{previous_summary}", preparedRequest.ContextPackage.RollingContextSummary)
            .Replace("{user_message}", userMessage)
            .Replace("{assistant_message}", assistantMessage);
    }

    /// <summary>
    /// Resolves a prompt template, falling back to a built-in default when missing or empty.
    /// </summary>
    /// <param name="prompts">The loaded prompt templates.</param>
    /// <param name="key">The prompt key.</param>
    /// <param name="fallback">The built-in fallback text.</param>
    /// <returns>The resolved prompt text.</returns>
    private static string ResolvePrompt(IReadOnlyDictionary<string, string> prompts, string key, string fallback)
    {
        return prompts.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    /// <summary>
    /// Builds the first compression prompt from the accumulated raw conversation log.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="prompts">The loaded prompt templates.</param>
    /// <returns>The bootstrap compression prompt.</returns>
    private string BuildBootstrapCompressionPrompt(string projectRoot, IReadOnlyDictionary<string, string> prompts)
    {
        IReadOnlyList<ConversationMessageRecord> messages = conversationLogStore.GetRecentMessages(projectRoot, int.MaxValue);
        StringBuilder logBuilder = new StringBuilder();

        foreach (ConversationMessageRecord message in messages)
        {
            logBuilder.Append('[');
            logBuilder.Append(message.CreatedAt.ToString("O"));
            logBuilder.Append("] ");
            logBuilder.Append(message.Role);
            logBuilder.Append(": ");
            logBuilder.AppendLine(message.Content);
            logBuilder.AppendLine();
        }

        return ResolvePrompt(prompts, PromptKeys.CompressionBootstrap, DefaultCompressionBootstrap)
            .Replace("{conversation_log}", logBuilder.ToString());
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
    /// <param name="intro">The intro line resolved from the prompt store.</param>
    /// <returns>The active criteria system message.</returns>
    private static string BuildActiveCriteriaMessage(IReadOnlyList<string> activeCriteria, string intro)
    {
        if (activeCriteria.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(intro);

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
