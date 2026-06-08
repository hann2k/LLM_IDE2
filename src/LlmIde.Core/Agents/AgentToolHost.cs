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
    /// Gets a value indicating whether any tool is registered.
    /// </summary>
    public bool HasAnyTool => tools.Count > 0;

    /// <summary>
    /// Lists the available tool descriptors.
    /// </summary>
    /// <returns>The available tool descriptors.</returns>
    public IReadOnlyList<AgentToolDescriptor> ListTools()
    {
        List<AgentToolDescriptor> descriptors = [];

        foreach (IAgentTool tool in tools.Values)
        {
            descriptors.Add(new AgentToolDescriptor
            {
                Name = tool.Name,
                Description = tool.Description,
                Arguments = tool.Arguments
            });
        }

        return descriptors;
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
