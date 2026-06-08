namespace LlmIde.Core.Agents;

/// <summary>
/// Describes why an agent loop stopped.
/// </summary>
public enum StopReason
{
    /// <summary>
    /// The model returned a final answer.
    /// </summary>
    FinalAnswer,

    /// <summary>
    /// The maximum number of turns was exceeded.
    /// </summary>
    MaxTurnsExceeded,

    /// <summary>
    /// A tool host execution failed unexpectedly.
    /// </summary>
    ToolError,

    /// <summary>
    /// The model call failed or returned unparseable content.
    /// </summary>
    ModelError,

    /// <summary>
    /// The model returned an unknown tool or message type.
    /// </summary>
    InvalidToolRequest
}
