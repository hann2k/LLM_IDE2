namespace LlmIde.Core.Providers;

/// <summary>
/// Stores provider settings for a project.
/// </summary>
public sealed class ProviderSettingsDocument
{
    /// <summary>
    /// Gets or sets the default provider name.
    /// </summary>
    public string DefaultProvider { get; set; } = "deepseek";

    /// <summary>
    /// Gets or sets all provider settings.
    /// </summary>
    public List<ProviderSettings> Providers { get; set; } = [];
}
