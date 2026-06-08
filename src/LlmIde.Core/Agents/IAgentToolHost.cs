namespace LlmIde.Core.Agents;

/// <summary>
/// Resolves and executes agent tools by name.
/// </summary>
public interface IAgentToolHost
{
    /// <summary>
    /// Determines whether a tool is registered.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>True when the tool is registered.</returns>
    bool HasTool(string toolName);

    /// <summary>
    /// Executes a tool request.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken);
}
