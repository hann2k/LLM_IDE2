using LlmIde.Core.Artifacts;
using LlmIde.Core.Conversations;
using System.Text;

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
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="providerSettingsStore">The provider settings store.</param>
    /// <param name="providers">The available providers.</param>
    /// <param name="conversationLogStore">The conversation log store.</param>
    /// <param name="contextBuilder">The context builder.</param>
    /// <param name="rollingContextStore">The rolling context store.</param>
    /// <param name="artifactService">The artifact service.</param>
    public ChatService(
        IProviderSettingsStore providerSettingsStore,
        IReadOnlyDictionary<string, IChatProvider> providers,
        IConversationLogStore conversationLogStore,
        ContextBuilder contextBuilder,
        IRollingContextStore rollingContextStore,
        ArtifactService artifactService)
    {
        this.providerSettingsStore = providerSettingsStore;
        this.providers = providers;
        this.conversationLogStore = conversationLogStore;
        this.contextBuilder = contextBuilder;
        this.rollingContextStore = rollingContextStore;
        this.artifactService = artifactService;
    }

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
        ChatProviderResponse response = await preparedRequest.Provider.SendAsync(
            preparedRequest.Request,
            preparedRequest.Settings,
            cancellationToken);
        response.SentRequest = preparedRequest.Request;
        response.RequestId = preparedRequest.RequestId;
        response.ArtifactCandidates = artifactService.ExtractCandidates(response.Content).ToList();

        StoreAssistantMessage(projectRoot, preparedRequest, response);
        onChatResponseReady?.Invoke(response);
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
    /// <returns>The completed provider response.</returns>
    public async Task<ChatProviderResponse> StreamAsync(
        string projectRoot,
        string message,
        Action<ChatProviderResponse>? onRequestReady,
        Action<string> onChunk,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? artifactIds = null,
        Action<ChatProviderResponse>? onCompressionRequestReady = null,
        Action<string>? onCompressionChunk = null)
    {
        PreparedChatRequest preparedRequest = PrepareRequest(projectRoot, message, artifactIds ?? []);
        StoreRequestStart(projectRoot, preparedRequest, message);
        onRequestReady?.Invoke(CreatePreviewResponse(preparedRequest));

        StringBuilder content = new StringBuilder();

        await foreach (string chunk in preparedRequest.Provider.StreamAsync(
            preparedRequest.Request,
            preparedRequest.Settings,
            cancellationToken))
        {
            content.Append(chunk);
            onChunk(chunk);
        }

        ChatProviderResponse response = new ChatProviderResponse
        {
            Content = content.ToString(),
            Provider = preparedRequest.Settings.Name,
            Model = preparedRequest.Request.Model,
            SentRequest = preparedRequest.Request,
            RequestId = preparedRequest.RequestId,
            ArtifactCandidates = artifactService.ExtractCandidates(content.ToString()).ToList()
        };

        StoreAssistantMessage(projectRoot, preparedRequest, response);
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
        return response;
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

        string requestId = $"req_{Guid.NewGuid():N}";
        ContextBuildResult context = contextBuilder.Build(projectRoot, requestId, message, artifactIds);

        ChatProviderRequest request = new ChatProviderRequest
        {
            Model = settings.Model,
            Messages = context.Messages
        };

        return new PreparedChatRequest(provider, settings, request, requestId, context.ContextPackage);
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
            MessageId = $"msg_{Guid.NewGuid():N}",
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
        ChatProviderRequest compressionRequest = new ChatProviderRequest
        {
            Model = preparedRequest.Request.Model,
            Messages =
            [
                new ChatMessage
                {
                    Role = "system",
                    Content = compressionRule
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = compressionPrompt
                }
            ]
        };
        string compressionContextPath = conversationLogStore.SaveContextPackage(
            projectRoot,
            CreateCompressionContextPackage(
                preparedRequest.CompressionRequestId,
                compressionRule,
                compressionPrompt,
                preparedRequest.ContextPackage.RollingContextSummary));
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
        string previousSummary)
    {
        return new ContextPackage
        {
            RequestId = requestId,
            SystemRule = compressionRule,
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
            MessageId = $"msg_{Guid.NewGuid():N}",
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
    /// <param name="ContextPackage">The context package.</param>
    private sealed record PreparedChatRequest(
        IChatProvider Provider,
        ProviderSettings Settings,
        ChatProviderRequest Request,
        string RequestId,
        ContextPackage ContextPackage)
    {
        /// <summary>
        /// Gets the compression request identifier.
        /// </summary>
        public string CompressionRequestId { get; } = $"req_{Guid.NewGuid():N}";
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
