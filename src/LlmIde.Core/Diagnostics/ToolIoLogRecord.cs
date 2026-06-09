using System.Text.Json;

namespace LlmIde.Core.Diagnostics;

/// <summary>
/// Captures one tool execution's full input and output (the raw data exchanged with an external
/// service), for the diagnostic log. Nothing is summarized or truncated here.
/// </summary>
public sealed class ToolIoLogRecord
{
    /// <summary>
    /// Gets or sets the chat request identifier this tool execution belongs to.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool execution identifier (tool-NNN) within the request.
    /// </summary>
    public string ToolRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full tool arguments sent to the tool.
    /// </summary>
    public JsonElement Arguments { get; set; }

    /// <summary>
    /// Gets or sets the full structured tool result (null when the tool failed).
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tool succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the error message when the tool failed (empty on success).
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time the tool execution started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the tool execution duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }
}
