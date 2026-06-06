namespace LlmIde.Core.Providers;

/// <summary>
/// Sends chat requests to an LLM provider.
/// </summary>
public interface IChatProvider
{
    /// <summary>
    /// Sends a chat request.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider response.</returns>
    Task<ChatProviderResponse> SendAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streams a chat response.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The streamed response chunks.</returns>
    IAsyncEnumerable<string> StreamAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        CancellationToken cancellationToken);
}
