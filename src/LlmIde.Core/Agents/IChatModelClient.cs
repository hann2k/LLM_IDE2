namespace LlmIde.Core.Agents;

/// <summary>
/// Abstracts a chat model so the agent loop does not depend on a specific provider.
/// </summary>
public interface IChatModelClient
{
    /// <summary>
    /// Completes a model request.
    /// </summary>
    /// <param name="request">The model request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model response.</returns>
    Task<ChatModelResponse> CompleteAsync(ChatModelRequest request, CancellationToken cancellationToken);
}
