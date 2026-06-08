namespace LlmIde.Core.Agents;

/// <summary>
/// Describes an available tool for the model's tool discovery (list_tools).
/// </summary>
public sealed class AgentToolDescriptor
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool argument summary.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;
}
