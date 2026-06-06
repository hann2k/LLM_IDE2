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
    /// Gets or sets the request timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
