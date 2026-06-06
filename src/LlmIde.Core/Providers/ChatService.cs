using LlmIde.Core.Conversations;

namespace LlmIde.Core.Providers;

/// <summary>
/// Coordinates chat requests with provider settings.
/// </summary>
public sealed class ChatService
{
    /// <summary>
    /// The number of previous messages to inject into a provider request.
    /// </summary>
    private const int MaxRecentMessages = 20;

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
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="providerSettingsStore">The provider settings store.</param>
    /// <param name="providers">The available providers.</param>
    /// <param name="conversationLogStore">The conversation log store.</param>
    public ChatService(
        IProviderSettingsStore providerSettingsStore,
        IReadOnlyDictionary<string, IChatProvider> providers,
        IConversationLogStore conversationLogStore)
    {
        this.providerSettingsStore = providerSettingsStore;
        this.providers = providers;
        this.conversationLogStore = conversationLogStore;
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
        ProviderSettingsDocument settingsDocument = providerSettingsStore.Load(projectRoot);
        ProviderSettings settings = GetDefaultProviderSettings(settingsDocument);

        if (!providers.TryGetValue(settings.Name, out IChatProvider? provider))
        {
            throw new InvalidOperationException($"Provider is not available: {settings.Name}");
        }

        IReadOnlyList<ConversationMessageRecord> recentMessages = conversationLogStore.GetRecentMessages(projectRoot, MaxRecentMessages);
        ChatProviderRequest request = new ChatProviderRequest
        {
            Model = settings.Model,
            Messages = recentMessages
                .Select(storedMessage => new ChatMessage
                {
                    Role = storedMessage.Role,
                    Content = storedMessage.Content
                })
                .Append(new ChatMessage
                {
                    Role = "user",
                    Content = message
                })
                .ToList()
        };

        string requestId = $"req_{Guid.NewGuid():N}";
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        ContextPackage contextPackage = CreateContextPackage(requestId, message, recentMessages);
        string contextPackagePath = conversationLogStore.SaveContextPackage(projectRoot, contextPackage);

        conversationLogStore.AppendRequest(projectRoot, new ConversationRequestRecord
        {
            RequestId = requestId,
            Provider = settings.Name,
            Model = settings.Model,
            ContextPackagePath = contextPackagePath,
            CreatedAt = createdAt
        });

        conversationLogStore.AppendMessage(projectRoot, new ConversationMessageRecord
        {
            MessageId = $"msg_{Guid.NewGuid():N}",
            RequestId = requestId,
            Role = "user",
            Content = message,
            Provider = settings.Name,
            Model = settings.Model,
            CreatedAt = createdAt
        });

        ChatProviderResponse response = await provider.SendAsync(request, settings, cancellationToken);
        response.SentRequest = request;
        response.RequestId = requestId;

        conversationLogStore.AppendMessage(projectRoot, new ConversationMessageRecord
        {
            MessageId = $"msg_{Guid.NewGuid():N}",
            RequestId = requestId,
            Role = "assistant",
            Content = response.Content,
            Provider = response.Provider,
            Model = response.Model,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return response;
    }

    /// <summary>
    /// Creates a context package for the current Phase 3 request.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="message">The user message.</param>
    /// <param name="recentMessages">The recent conversation messages.</param>
    /// <returns>The context package.</returns>
    private static ContextPackage CreateContextPackage(
        string requestId,
        string message,
        IReadOnlyList<ConversationMessageRecord> recentMessages)
    {
        return new ContextPackage
        {
            RequestId = requestId,
            UserRequest = message,
            RecentTurns = recentMessages
                .Select(recentMessage => $"{recentMessage.Role}: {recentMessage.Content}")
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
}
