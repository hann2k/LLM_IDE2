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
    /// The IDE program root containing the common provider settings.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonProviderSettingsStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public JsonProviderSettingsStore(string? programRoot = null)
    {
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <summary>
    /// Loads the common provider settings (shared by all projects), seeding defaults on first use.
    /// </summary>
    /// <param name="projectRoot">Ignored; provider settings are common.</param>
    /// <returns>The provider settings document.</returns>
    public ProviderSettingsDocument Load(string projectRoot)
    {
        string settingsPath = GetSettingsPath();

        if (!File.Exists(settingsPath))
        {
            ProviderSettingsDocument defaults = CreateDefault();
            Save(projectRoot, defaults);
            return defaults;
        }

        string json = File.ReadAllText(settingsPath);
        ProviderSettingsDocument? settings = JsonSerializer.Deserialize<ProviderSettingsDocument>(json, JsonOptions.Default);
        return settings ?? CreateDefault();
    }

    /// <summary>
    /// Saves the common provider settings.
    /// </summary>
    /// <param name="projectRoot">Ignored; provider settings are common.</param>
    /// <param name="settings">The provider settings document.</param>
    public void Save(string projectRoot, ProviderSettingsDocument settings)
    {
        string settingsPath = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath) ?? programRoot);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonOptions.Default));
    }

    /// <summary>
    /// Gets the common provider settings path (&lt;programRoot&gt;/settings/providers.json).
    /// </summary>
    /// <returns>The provider settings path.</returns>
    private string GetSettingsPath()
    {
        return Path.Combine(programRoot, LlmIdeLayout.SettingsDirectoryName, LlmIdeLayout.ProvidersFileName);
    }

    /// <summary>
    /// Creates the default provider settings (one DeepSeek entry without a key).
    /// </summary>
    /// <returns>The default document.</returns>
    private static ProviderSettingsDocument CreateDefault()
    {
        return new ProviderSettingsDocument
        {
            DefaultProvider = "deepseek",
            Providers = [new ProviderSettings { Name = "deepseek", Model = "deepseek-chat" }]
        };
    }
}
