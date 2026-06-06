namespace LlmIde.Core.Providers;

/// <summary>
/// Describes a provider chat request.
/// </summary>
public sealed class ChatProviderRequest
{
    /// <summary>
    /// Gets or sets the provider model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chat messages.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];
}
