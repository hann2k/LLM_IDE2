namespace LlmIde.Core.Agents;

/// <summary>
/// Stores the supervisor-controlled list of tools the agent may use.
/// </summary>
public sealed class AgentToolSettings
{
    /// <summary>
    /// Gets or sets the allowed tool names. An empty list means all tools are allowed.
    /// </summary>
    public List<string> AllowedTools { get; set; } = [];
}
