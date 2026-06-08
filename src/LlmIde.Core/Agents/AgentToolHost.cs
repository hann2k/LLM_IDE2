namespace LlmIde.Core.Agents;

/// <summary>
/// Hosts agent tools and dispatches requests by tool name.
/// </summary>
public sealed class AgentToolHost : IAgentToolHost
{
    /// <summary>
    /// The registered tools by name.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IAgentTool> tools;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentToolHost"/> class.
    /// </summary>
    /// <param name="tools">The tools to register.</param>
    public AgentToolHost(IEnumerable<IAgentTool> tools)
    {
        Dictionary<string, IAgentTool> map = new Dictionary<string, IAgentTool>(StringComparer.Ordinal);

        foreach (IAgentTool tool in tools)
        {
            map[tool.Name] = tool;
        }

        this.tools = map;
    }

    /// <summary>
    /// Determines whether a tool is registered.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>True when the tool is registered.</returns>
    public bool HasTool(string toolName)
    {
        return !string.IsNullOrWhiteSpace(toolName) && tools.ContainsKey(toolName);
    }

    /// <summary>
    /// Executes a tool request.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        if (!tools.TryGetValue(request.Tool, out IAgentTool? tool))
        {
            throw new InvalidOperationException($"Unknown tool: {request.Tool}");
        }

        return tool.ExecuteAsync(request, cancellationToken);
    }
}
