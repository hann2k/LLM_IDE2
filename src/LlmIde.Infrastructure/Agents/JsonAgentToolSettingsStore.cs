using System.Text.Json;
using LlmIde.Core.Agents;
using LlmIde.Infrastructure.Json;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Reads the global supervisor-controlled agent tool settings from the IDE program root.
/// </summary>
public sealed class JsonAgentToolSettingsStore
{
    /// <summary>
    /// The settings directory name.
    /// </summary>
    private const string SettingsDirectoryName = "settings";

    /// <summary>
    /// The agent tool settings file name.
    /// </summary>
    private const string FileName = "agent-tools.json";

    /// <summary>
    /// Loads the agent tool settings. Returns empty (all tools allowed) when the file is missing.
    /// </summary>
    /// <param name="ideProgramRoot">The IDE program root.</param>
    /// <returns>The agent tool settings.</returns>
    public AgentToolSettings Load(string ideProgramRoot)
    {
        string path = Path.Combine(Path.GetFullPath(ideProgramRoot), SettingsDirectoryName, FileName);

        if (!File.Exists(path))
        {
            return new AgentToolSettings();
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AgentToolSettings>(json, JsonOptions.Default) ?? new AgentToolSettings();
    }
}
