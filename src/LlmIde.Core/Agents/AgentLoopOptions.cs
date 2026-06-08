namespace LlmIde.Core.Agents;

/// <summary>
/// Configures agent loop behavior.
/// </summary>
public sealed class AgentLoopOptions
{
    /// <summary>
    /// Gets or sets the maximum number of model turns.
    /// </summary>
    public int MaxTurns { get; set; } = 6;

    /// <summary>
    /// Gets or sets the maximum tool result characters sent back to the model.
    /// </summary>
    public int MaxToolResultChars { get; set; } = 12000;
}
