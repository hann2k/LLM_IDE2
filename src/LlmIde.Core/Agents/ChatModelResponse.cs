namespace LlmIde.Core.Agents;

/// <summary>
/// Describes a model completion response used by the agent loop.
/// </summary>
public sealed class ChatModelResponse
{
    /// <summary>
    /// Gets or sets the model response content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
