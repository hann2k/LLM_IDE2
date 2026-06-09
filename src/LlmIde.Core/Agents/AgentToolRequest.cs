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

    /// <summary>
    /// Gets or sets the working directory (the project root) that file tools resolve paths against and
    /// must not escape. Empty for tools that do not access the file system.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;
}
