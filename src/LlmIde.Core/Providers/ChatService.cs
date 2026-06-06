using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
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
    /// The criteria service.
    /// </summary>
    private readonly CriteriaService criteriaService;

    /// <summary>
    /// The project state service.
    /// </summary>
    private readonly ProjectStateService projectStateService;

    /// <summary>
    /// The rolling context store.
    /// </summary>
    private readonly IRollingContextStore rollingContextStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="providerSettingsStore">The provider settings store.</param>
    /// <param name="providers">The available providers.</param>
    /// <param name="conversationLogStore">The conversation log store.</param>
    /// <param name="criteriaService">The criteria service.</param>
    /// <param name="projectStateService">The project state service.</param>
    /// <param name="rollingContextStore">The rolling context store.</param>
    public ChatService(
        IProviderSettingsStore providerSettingsStore,
        IReadOnlyDictionary<string, IChatProvider> providers,
        IConversationLogStore conversationLogStore,
        CriteriaService criteriaService,
        ProjectStateService projectStateService,
        IRollingContextStore rollingContextStore)
    {
        this.providerSettingsStore = providerSettingsStore;
        this.providers = providers;
        this.conversationLogStore = conversationLogStore;
        this.criteriaService = criteriaService;
        this.projectStateService = projectStateService;
        this.rollingContextStore = rollingContextStore;
    }

    /// <summary>
    /// Sends a single user message to the project's default provider.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The user message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider response.</returns>
    public async Task<ChatProviderResponse> SendAsync(
        string projectRoot,
        string message,
        CancellationToken cancellationToken)
    {
        PreparedChatRequest preparedRequest = PrepareRequest(projectRoot, message);
        StoreRequestStart(projectRoot, preparedRequest, message);
        ChatProviderResponse response = await preparedRequest.Provider.SendAsync(
            preparedRequest.Request,
            preparedRequest.Settings,
            cancellationToken);
        response.SentRequest = preparedRequest.Request;
        response.RequestId = preparedRequest.RequestId;

        StoreAssistantMessage(projectRoot, preparedRequest, response);
        await CompressAfterChatAsync(projectRoot, preparedRequest, message, response.Content, cancellationToken);

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
    /// <returns>The completed provider response.</returns>
    public async Task<ChatProviderResponse> StreamAsync(
        string projectRoot,
        string message,
        Action<ChatProviderResponse>? onRequestReady,
        Action<string> onChunk,
        CancellationToken cancellationToken)
    {
        PreparedChatRequest preparedRequest = PrepareRequest(projectRoot, message);
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
            RequestId = preparedRequest.RequestId
        };

        StoreAssistantMessage(projectRoot, preparedRequest, response);
        await CompressAfterChatAsync(projectRoot, preparedRequest, message, response.Content, cancellationToken);
        return response;
    }

    /// <summary>
    /// Prepares a provider request and associated metadata.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The user message.</param>
    /// <returns>The prepared request.</returns>
    private PreparedChatRequest PrepareRequest(string projectRoot, string message)
    {
        ProviderSettingsDocument settingsDocument = providerSettingsStore.Load(projectRoot);
        ProviderSettings settings = GetDefaultProviderSettings(settingsDocument);

        if (!providers.TryGetValue(settings.Name, out IChatProvider? provider))
        {
            throw new InvalidOperationException($"Provider is not available: {settings.Name}");
        }

        rollingContextStore.EnsureInitialized(projectRoot);
        RollingContextSummary? rollingContext = rollingContextStore.LoadCurrent(projectRoot);
        IReadOnlyList<Criterion> activeCriteria = criteriaService.ListActive(projectRoot);
        ProjectState projectState = projectStateService.Get(projectRoot);

        if (activeCriteria.Count == 0)
        {
            throw new InvalidOperationException("No active criteria. Add or activate at least one criterion before chat.");
        }

        ChatProviderRequest request = new ChatProviderRequest
        {
            Model = settings.Model,
            Messages = BuildProviderMessages(activeCriteria, projectState, rollingContext, message)
        };
        string requestId = $"req_{Guid.NewGuid():N}";
        ContextPackage contextPackage = CreateContextPackage(requestId, message, activeCriteria, projectState, rollingContext);

        return new PreparedChatRequest(provider, settings, request, requestId, contextPackage);
    }

    /// <summary>
    /// Builds the provider messages for a chat request.
    /// </summary>
    /// <param name="activeCriteria">The active criteria.</param>
    /// <param name="projectState">The project state.</param>
    /// <param name="rollingContext">The rolling context summary.</param>
    /// <param name="message">The current user message.</param>
    /// <returns>The provider messages.</returns>
    private static List<ChatMessage> BuildProviderMessages(
        IReadOnlyList<Criterion> activeCriteria,
        ProjectState projectState,
        RollingContextSummary? rollingContext,
        string message)
    {
        List<ChatMessage> messages = [];
        string criteriaMessage = BuildCriteriaMessage(activeCriteria);
        string stateMessage = BuildProjectStateMessage(projectState);
        string rollingContextMessage = BuildRollingContextMessage(rollingContext);

        if (!string.IsNullOrWhiteSpace(criteriaMessage))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = criteriaMessage
            });
        }

        if (!string.IsNullOrWhiteSpace(stateMessage))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = stateMessage
            });
        }

        if (!string.IsNullOrWhiteSpace(rollingContextMessage))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = rollingContextMessage
            });
        }

        messages.Add(new ChatMessage
        {
            Role = "user",
            Content = message
        });

        return messages;
    }

    /// <summary>
    /// Builds the criteria system message.
    /// </summary>
    /// <param name="activeCriteria">The active criteria.</param>
    /// <returns>The system message content.</returns>
    private static string BuildCriteriaMessage(IReadOnlyList<Criterion> activeCriteria)
    {
        if (activeCriteria.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Follow these active project criteria for this response:");

        foreach (Criterion criterion in activeCriteria)
        {
            builder.Append("- ");
            builder.Append(criterion.Title);

            if (!string.IsNullOrWhiteSpace(criterion.Priority))
            {
                builder.Append(" [");
                builder.Append(criterion.Priority);
                builder.Append(']');
            }

            if (!string.IsNullOrWhiteSpace(criterion.Description))
            {
                builder.Append(": ");
                builder.Append(criterion.Description);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds the rolling context system message.
    /// </summary>
    /// <param name="rollingContext">The rolling context summary.</param>
    /// <returns>The system message content.</returns>
    private static string BuildRollingContextMessage(RollingContextSummary? rollingContext)
    {
        if (rollingContext is null || string.IsNullOrWhiteSpace(rollingContext.Content))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Use this compressed prior conversation context:");
        builder.AppendLine(rollingContext.Content);
        return builder.ToString();
    }

    /// <summary>
    /// Builds the project state system message.
    /// </summary>
    /// <param name="projectState">The project state.</param>
    /// <returns>The system message content.</returns>
    private static string BuildProjectStateMessage(ProjectState projectState)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Use this current project state as context:");
        AppendStateValue(builder, "Stage", projectState.Stage);
        AppendStateValue(builder, "Current task", projectState.CurrentTask);
        AppendStateList(builder, "Completed items", projectState.CompletedItems);
        AppendStateList(builder, "In-progress items", projectState.InProgressItems);
        AppendStateList(builder, "Next actions", projectState.NextActions);
        AppendStateList(builder, "Blockers", projectState.Blockers);
        AppendStateValue(builder, "Last decision", projectState.LastDecision);
        return builder.ToString();
    }

    /// <summary>
    /// Appends a scalar project state value.
    /// </summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="label">The state label.</param>
    /// <param name="value">The state value.</param>
    private static void AppendStateValue(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append("- ");
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(value);
    }

    /// <summary>
    /// Appends a project state list.
    /// </summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="label">The state label.</param>
    /// <param name="items">The state items.</param>
    private static void AppendStateList(StringBuilder builder, string label, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        builder.Append("- ");
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(string.Join("; ", items));
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
            RecentTurnCount = 0,
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
    private async Task CompressAfterChatAsync(
        string projectRoot,
        PreparedChatRequest preparedRequest,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken)
    {
        string previousRollingContextId = preparedRequest.ContextPackage.UsedRollingContextId;

        try
        {
            ChatProviderRequest compressionRequest = new ChatProviderRequest
            {
                Model = preparedRequest.Request.Model,
                Messages =
                [
                    new ChatMessage
                    {
                        Role = "system",
                        Content = rollingContextStore.LoadCompressionRule(projectRoot)
                    },
                    new ChatMessage
                    {
                        Role = "user",
                        Content = BuildCompressionPrompt(
                            preparedRequest.ContextPackage.RollingContextSummary,
                            userMessage,
                            assistantMessage)
                    }
                ]
            };
            ChatProviderResponse compressionResponse = await preparedRequest.Provider.SendAsync(
                compressionRequest,
                preparedRequest.Settings,
                cancellationToken);
            RollingContextSummary summary = rollingContextStore.SaveCompleted(
                projectRoot,
                preparedRequest.RequestId,
                string.IsNullOrWhiteSpace(previousRollingContextId) ? null : previousRollingContextId,
                compressionResponse.Content,
                preparedRequest.Settings.Name,
                preparedRequest.Request.Model);

            conversationLogStore.AppendRequest(projectRoot, new ConversationRequestRecord
            {
                RequestId = preparedRequest.CompressionRequestId,
                RequestType = "compression",
                SourceChatRequestId = preparedRequest.RequestId,
                Provider = preparedRequest.Settings.Name,
                Model = preparedRequest.Request.Model,
                ContextPackagePath = summary.ContentPath,
                UsedRollingContextId = previousRollingContextId,
                RollingContextPath = summary.ContentPath,
                Status = "completed",
                CreatedAt = summary.CreatedAt
            });
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
                ContextPackagePath = failedSummary.ContentPath,
                UsedRollingContextId = previousRollingContextId,
                Status = "failed",
                Error = ex.Message,
                CreatedAt = failedSummary.CreatedAt
            });
        }
    }

    /// <summary>
    /// Builds the compression prompt.
    /// </summary>
    /// <param name="previousSummary">The previous rolling summary.</param>
    /// <param name="userMessage">The current user message.</param>
    /// <param name="assistantMessage">The current assistant message.</param>
    /// <returns>The compression prompt.</returns>
    private static string BuildCompressionPrompt(
        string previousSummary,
        string userMessage,
        string assistantMessage)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[Previous Rolling Context Summary]");
        builder.AppendLine(string.IsNullOrWhiteSpace(previousSummary) ? "(none)" : previousSummary);
        builder.AppendLine();
        builder.AppendLine("[Current User Message]");
        builder.AppendLine(userMessage);
        builder.AppendLine();
        builder.AppendLine("[Current Assistant Response]");
        builder.AppendLine(assistantMessage);
        builder.AppendLine();
        builder.AppendLine("[Task]");
        builder.AppendLine("위 내용을 병합하여 다음 요청에 사용할 Rolling Context Summary를 갱신하라.");
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
    /// Creates a context package for the current Phase 3 request.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="message">The user message.</param>
    /// <param name="activeCriteria">The active criteria.</param>
    /// <param name="projectState">The project state.</param>
    /// <param name="rollingContext">The rolling context summary.</param>
    /// <returns>The context package.</returns>
    private static ContextPackage CreateContextPackage(
        string requestId,
        string message,
        IReadOnlyList<Criterion> activeCriteria,
        ProjectState projectState,
        RollingContextSummary? rollingContext)
    {
        return new ContextPackage
        {
            RequestId = requestId,
            UserRequest = message,
            ProjectState = projectState,
            UsedRollingContextId = rollingContext?.RollingContextId ?? string.Empty,
            RollingContextPath = rollingContext?.ContentPath ?? string.Empty,
            RollingContextSummary = rollingContext?.Content ?? string.Empty,
            ActiveCriteria = activeCriteria
                .Select(criterion => $"{criterion.Title}: {criterion.Description}")
                .ToList()
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
}
