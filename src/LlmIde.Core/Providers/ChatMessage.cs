namespace LlmIde.Core.Providers;

/// <summary>
/// Represents a chat message sent to or received from a provider.
/// </summary>
public sealed class ChatMessage
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
