using System.Text.Json;

namespace LlmIde.Core.Agents;

/// <summary>
/// Represents a tool execution request issued by the model.
/// </summary>
public sealed class AgentToolRequest
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw tool arguments.
    /// </summary>
    public JsonElement Arguments { get; set; }
}
