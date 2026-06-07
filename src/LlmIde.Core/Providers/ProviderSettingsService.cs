namespace LlmIde.Core.Providers;

/// <summary>
/// Coordinates provider settings and model management.
/// </summary>
public sealed class ProviderSettingsService
{
    /// <summary>
    /// The provider settings store.
    /// </summary>
    private readonly IProviderSettingsStore providerSettingsStore;

    /// <summary>
    /// The available model providers.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IModelProvider> modelProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderSettingsService"/> class.
    /// </summary>
    /// <param name="providerSettingsStore">The provider settings store.</param>
    /// <param name="modelProviders">The available model providers.</param>
    public ProviderSettingsService(
        IProviderSettingsStore providerSettingsStore,
        IReadOnlyDictionary<string, IModelProvider> modelProviders)
    {
        this.providerSettingsStore = providerSettingsStore;
        this.modelProviders = modelProviders;
    }

    /// <summary>
    /// Lists models for the project's default provider.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available models.</returns>
    public Task<IReadOnlyList<ProviderModel>> ListModelsAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        ProviderSettingsDocument settingsDocument = providerSettingsStore.Load(projectRoot);
        ProviderSettings settings = GetDefaultProviderSettings(settingsDocument);

        if (!modelProviders.TryGetValue(settings.Name, out IModelProvider? modelProvider))
        {
            throw new InvalidOperationException($"Model provider is not available: {settings.Name}");
        }

        return modelProvider.ListModelsAsync(settings, cancellationToken);
    }

    /// <summary>
    /// Sets the default model for the project's default provider.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="model">The model identifier.</param>
    public void SetDefaultModel(string projectRoot, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("모델은 필수입니다.");
        }

        ProviderSettingsDocument settingsDocument = providerSettingsStore.Load(projectRoot);
        ProviderSettings settings = GetDefaultProviderSettings(settingsDocument);
        settings.Model = model;
        providerSettingsStore.Save(projectRoot, settingsDocument);
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
