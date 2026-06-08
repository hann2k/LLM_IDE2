using LlmIde.Core.Agents;
using LlmIde.Core.Providers;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Adapts an existing chat provider to the agent loop model client.
/// </summary>
public sealed class DeepSeekChatModelClient : IChatModelClient
{
    /// <summary>
    /// The underlying chat provider.
    /// </summary>
    private readonly IChatProvider chatProvider;

    /// <summary>
    /// The provider settings.
    /// </summary>
    private readonly ProviderSettings settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepSeekChatModelClient"/> class.
    /// </summary>
    /// <param name="chatProvider">The chat provider.</param>
    /// <param name="settings">The provider settings.</param>
    public DeepSeekChatModelClient(IChatProvider chatProvider, ProviderSettings settings)
    {
        this.chatProvider = chatProvider;
        this.settings = settings;
    }

    /// <summary>
    /// Completes a model request through the wrapped provider.
    /// </summary>
    /// <param name="request">The model request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model response.</returns>
    public async Task<ChatModelResponse> CompleteAsync(ChatModelRequest request, CancellationToken cancellationToken)
    {
        ChatProviderRequest providerRequest = new ChatProviderRequest
        {
            Model = settings.Model,
            Messages = request.Messages
        };

        ChatProviderResponse response = await chatProvider.SendAsync(providerRequest, settings, cancellationToken);
        return new ChatModelResponse { Content = response.Content };
    }
}
