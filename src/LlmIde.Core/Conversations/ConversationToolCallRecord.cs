namespace LlmIde.Core.Conversations;

/// <summary>
/// Represents a single agent tool execution recorded during a chat request.
/// </summary>
public sealed class ConversationToolCallRecord
{
    /// <summary>
    /// Gets or sets the chat request identifier this tool call belongs to.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution order within the request (1-based).
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sanitized target (host for fetch_url, query for web_search). Never contains secrets.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tool execution succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets a short, non-sensitive summary of the tool result.
    /// </summary>
    public string ResultSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message when the tool execution failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
