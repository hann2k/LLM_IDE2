namespace LlmIde.Core.Providers;

/// <summary>
/// Stores settings for one provider.
/// </summary>
public sealed class ProviderSettings
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider endpoint.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default model.
    /// </summary>
    public string Model { get; set; } = string.Empty;
}
