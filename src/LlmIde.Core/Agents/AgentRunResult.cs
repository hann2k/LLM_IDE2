namespace LlmIde.Core.Agents;

/// <summary>
/// Describes the outcome of an agent loop run.
/// </summary>
public sealed class AgentRunResult
{
    /// <summary>
    /// Gets or sets the final answer text shown to the user.
    /// </summary>
    public string FinalText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the turns produced during the run.
    /// </summary>
    public List<AgentTurn> Turns { get; set; } = [];

    /// <summary>
    /// Gets or sets the tool results produced during the run.
    /// </summary>
    public List<AgentToolResult> ToolResults { get; set; } = [];

    /// <summary>
    /// Gets or sets the stop reason.
    /// </summary>
    public StopReason StopReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the run succeeded.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the error message when the run failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
