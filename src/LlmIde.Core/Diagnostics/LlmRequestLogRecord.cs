namespace LlmIde.Core.Diagnostics;

/// <summary>
/// Captures one actual LLM provider call (the exact injected prompt and the raw response),
/// for verifying what information is sent to the model.
/// </summary>
public sealed class LlmRequestLogRecord
{
    /// <summary>
    /// Gets or sets the conversation request identifier this call belongs to.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the call purpose ("chat" or "compression").
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zero-based turn index within the agent loop (0 for the first call,
    /// incremented for each follow-up call after a tool result is injected).
    /// </summary>
    public int Turn { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the call was a streaming call.
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>
    /// Gets or sets the exact messages sent to the model, in order (the full injected prompt).
    /// </summary>
    public IReadOnlyList<LlmRequestLogMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the raw model response content (an empty string is recorded verbatim).
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the character length of the raw response.
    /// </summary>
    public int ResponseChars { get; set; }

    /// <summary>
    /// Gets or sets the time the call started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the call duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the error message when the call failed (empty on success).
    /// </summary>
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Represents one role/content message captured in an <see cref="LlmRequestLogRecord"/>.
/// </summary>
public sealed class LlmRequestLogMessage
{
    /// <summary>
    /// Gets or sets the message role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
