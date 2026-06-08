using LlmIde.Core.Providers;

namespace LlmIde.Core.Agents;

/// <summary>
/// Describes a model completion request used by the agent loop.
/// </summary>
public sealed class ChatModelRequest
{
    /// <summary>
    /// Gets or sets the conversation messages.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];
}
