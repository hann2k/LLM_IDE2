namespace LlmIde.Core.Agents;

/// <summary>
/// Represents an executable agent tool.
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the tool.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken);
}
