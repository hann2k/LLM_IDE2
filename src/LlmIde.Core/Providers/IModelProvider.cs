namespace LlmIde.Core.Providers;

/// <summary>
/// Lists models available from an LLM provider.
/// </summary>
public interface IModelProvider
{
    /// <summary>
    /// Lists models available for the supplied provider settings.
    /// </summary>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available models.</returns>
    Task<IReadOnlyList<ProviderModel>> ListModelsAsync(
        ProviderSettings settings,
        CancellationToken cancellationToken);
}
