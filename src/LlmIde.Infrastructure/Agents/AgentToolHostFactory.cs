using System.Net.Http;
using LlmIde.Core.Agents;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Builds the agent tool host, registering all tools and applying the supervisor allowlist.
/// </summary>
public static class AgentToolHostFactory
{
    /// <summary>
    /// Creates a tool host with the tools the supervisor allows.
    /// </summary>
    /// <param name="ideProgramRoot">The IDE program root (for global allowlist).</param>
    /// <param name="httpClient">The HTTP client used by network tools.</param>
    /// <returns>The configured tool host.</returns>
    public static IAgentToolHost Create(string ideProgramRoot, HttpClient httpClient)
    {
        // All implemented tools are registered here; add new tools to this list.
        List<IAgentTool> allTools =
        [
            new FetchUrlTool(httpClient),
            new WebSearchTool(httpClient)
        ];

        AgentToolSettings settings = new JsonAgentToolSettingsStore().Load(ideProgramRoot);

        // An empty allow list means all tools are allowed.
        IEnumerable<IAgentTool> enabledTools = settings.AllowedTools.Count == 0
            ? allTools
            : allTools.Where(tool => settings.AllowedTools.Contains(tool.Name, StringComparer.Ordinal));

        return new AgentToolHost(enabledTools);
    }
}
