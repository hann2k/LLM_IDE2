namespace LlmIde.Core.Conversations;

/// <summary>
/// Represents a stored conversation message.
/// </summary>
public sealed class ConversationMessageRecord
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
