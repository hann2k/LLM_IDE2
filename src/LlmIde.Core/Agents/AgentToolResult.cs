namespace LlmIde.Core.Agents;

/// <summary>
/// Represents the result of a tool execution.
/// </summary>
public sealed class AgentToolResult
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tool succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the structured tool result payload.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets the error message when the tool failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
