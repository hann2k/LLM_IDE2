namespace LlmIde.Core.Providers;

/// <summary>
/// Provides project provider settings persistence.
/// </summary>
public interface IProviderSettingsStore
{
    /// <summary>
    /// Loads provider settings for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The provider settings document.</returns>
    ProviderSettingsDocument Load(string projectRoot);

    /// <summary>
    /// Saves provider settings for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="settings">The provider settings document.</param>
    void Save(string projectRoot, ProviderSettingsDocument settings);
}
