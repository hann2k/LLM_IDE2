using System.Net.Http;
using LlmIde.Core.Agents;
using LlmIde.Core.Artifacts;

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
    /// <param name="artifactService">The artifact service used by artifact tools.</param>
    /// <param name="bodyBridge">The writing editor body bridge; when provided, the body editing tools are registered.</param>
    /// <returns>The configured tool host.</returns>
    public static IAgentToolHost Create(string ideProgramRoot, HttpClient httpClient, ArtifactService artifactService, IDocumentBodyBridge? bodyBridge = null)
    {
        // All implemented tools are registered here; add new tools to this list.
        List<IAgentTool> allTools =
        [
            new FetchUrlTool(httpClient),
            new WebSearchTool(httpClient),
            new ReadFileTool(),
            new ListFilesTool(),
            new ArtifactsListTool(artifactService),
            new ReadArtifactTool(artifactService)
        ];

        // The writing app supplies a body bridge so the model can read/propose body edits (in-memory, not files).
        if (bodyBridge is not null)
        {
            allTools.Add(new GetBodyTool(bodyBridge));
            allTools.Add(new ProposeBodyEditTool(bodyBridge));
        }

        AgentToolSettings settings = new JsonAgentToolSettingsStore().Load(ideProgramRoot);

        // An empty allow list means all tools are allowed.
        IEnumerable<IAgentTool> enabledTools = settings.AllowedTools.Count == 0
            ? allTools
            : allTools.Where(tool => settings.AllowedTools.Contains(tool.Name, StringComparer.Ordinal));

        return new AgentToolHost(enabledTools);
    }
}
