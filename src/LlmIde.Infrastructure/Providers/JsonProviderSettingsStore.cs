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
        string settingsPath = Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.SettingsDirectoryName,
            LlmIdeLayout.ProvidersFileName);

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
}
