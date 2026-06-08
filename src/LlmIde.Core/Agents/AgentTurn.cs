namespace LlmIde.Core.Agents;

/// <summary>
/// Represents one turn in an agent loop run.
/// </summary>
public sealed class AgentTurn
{
    /// <summary>
    /// Gets or sets the turn role.
    /// </summary>
    public AgentTurnRole Role { get; set; }

    /// <summary>
    /// Gets or sets the turn content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
