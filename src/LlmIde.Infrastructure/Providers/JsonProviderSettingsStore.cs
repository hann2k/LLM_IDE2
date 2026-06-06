using System.Text.Json;
using LlmIde.Core.Providers;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;

namespace LlmIde.Infrastructure.Providers;

/// <summary>
/// Loads provider settings from a project's metadata folder.
/// </summary>
public sealed class JsonProviderSettingsStore : IProviderSettingsStore
{
    /// <summary>
    /// Loads provider settings for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The provider settings document.</returns>
    public ProviderSettingsDocument Load(string projectRoot)
    {
        string settingsPath = GetSettingsPath(projectRoot);

        if (!File.Exists(settingsPath))
        {
            throw new InvalidOperationException($"Provider settings file does not exist: {settingsPath}");
        }

        string json = File.ReadAllText(settingsPath);
        ProviderSettingsDocument? settings = JsonSerializer.Deserialize<ProviderSettingsDocument>(json, JsonOptions.Default);

        if (settings is null)
        {
            throw new InvalidOperationException($"Provider settings could not be read: {settingsPath}");
        }

        return settings;
    }

    /// <summary>
    /// Saves provider settings for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="settings">The provider settings document.</param>
    public void Save(string projectRoot, ProviderSettingsDocument settings)
    {
        string settingsPath = GetSettingsPath(projectRoot);
        string json = JsonSerializer.Serialize(settings, JsonOptions.Default);
        File.WriteAllText(settingsPath, json);
    }

    /// <summary>
    /// Gets the provider settings path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The provider settings path.</returns>
    private static string GetSettingsPath(string projectRoot)
    {
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.SettingsDirectoryName,
            LlmIdeLayout.ProvidersFileName);
    }
}
