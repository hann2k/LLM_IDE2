using LlmIde.Core.Providers;

namespace LlmIde.Core.Conversations;

/// <summary>
/// Represents a built provider context.
/// </summary>
public sealed class ContextBuildResult
{
    /// <summary>
    /// Gets or sets the context package.
    /// </summary>
    public ContextPackage ContextPackage { get; set; } = new ContextPackage();

    /// <summary>
    /// Gets or sets the provider messages.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];
}
