namespace LlmIde.Core.Agents;

/// <summary>
/// Describes a single agent loop run requested by one user input.
/// </summary>
public sealed class AgentRunRequest
{
    /// <summary>
    /// Gets or sets the user input.
    /// </summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent loop options.
    /// </summary>
    public AgentLoopOptions Options { get; set; } = new AgentLoopOptions();
}
