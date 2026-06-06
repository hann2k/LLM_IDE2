using LlmIde.Core.Conversations;
using System.Text;

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
        PreparedChatRequest preparedRequest = PrepareRequest(projectRoot, message);
        StoreRequestStart(projectRoot, preparedRequest, message);
        ChatProviderResponse response = await preparedRequest.Provider.SendAsync(
            preparedRequest.Request,
            preparedRequest.Settings,
            cancellationToken);
        response.SentRequest = preparedRequest.Request;
        response.RequestId = preparedRequest.RequestId;

        StoreAssistantMessage(projectRoot, preparedRequest, response);

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
        ContextPackage contextPackage = CreateContextPackage(requestId, message, recentMessages);

        return new PreparedChatRequest(provider, settings, request, requestId, contextPackage);
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
            Provider = preparedRequest.Settings.Name,
            Model = preparedRequest.Request.Model,
            ContextPackagePath = contextPackagePath,
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
        ContextPackage ContextPackage);
}
