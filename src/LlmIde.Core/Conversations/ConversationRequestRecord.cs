namespace LlmIde.Core.Conversations;

/// <summary>
/// Represents a stored provider request summary.
/// </summary>
public sealed class ConversationRequestRecord
{
    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request type.
    /// </summary>
    public string RequestType { get; set; } = "chat";

    /// <summary>
    /// Gets or sets the source chat request identifier for internal requests.
    /// </summary>
    public string SourceChatRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the context package path.
    /// </summary>
    public string ContextPackagePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rolling context identifier used by this request.
    /// </summary>
    public string UsedRollingContextId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rolling context path used by this request.
    /// </summary>
    public string RollingContextPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recent turn count used by this request.
    /// </summary>
    public int RecentTurnCount { get; set; }

    /// <summary>
    /// Gets or sets the conversation importance weight from 0 to 10.
    /// </summary>
    public int ImportanceWeight { get; set; }

    /// <summary>
    /// Gets or sets the compression request identifier.
    /// </summary>
    public string CompressionRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression status.
    /// </summary>
    public string CompressionStatus { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression error.
    /// </summary>
    public string CompressionError { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request status.
    /// </summary>
    public string Status { get; set; } = "completed";

    /// <summary>
    /// Gets or sets the request error.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
