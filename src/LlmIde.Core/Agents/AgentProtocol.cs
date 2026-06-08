using System.Text.Json;

namespace LlmIde.Core.Agents;

/// <summary>
/// Builds the IDE→model protocol envelopes (tool list, tool result).
/// </summary>
public static class AgentProtocol
{
    /// <summary>
    /// Builds the tool_list envelope returned for a list_tools request.
    /// </summary>
    /// <param name="tools">The available tool descriptors.</param>
    /// <returns>The tool_list JSON.</returns>
    public static string ToolList(IReadOnlyList<AgentToolDescriptor> tools)
    {
        var envelope = new
        {
            type = "tool_list",
            tools
        };
        return JsonSerializer.Serialize(envelope, AgentJson.Options);
    }

    /// <summary>
    /// Builds the tool_result envelope returned after a tool runs.
    /// </summary>
    /// <param name="toolResult">The tool result.</param>
    /// <returns>The tool_result JSON.</returns>
    public static string ToolResult(AgentToolResult toolResult)
    {
        object payload = toolResult.Result ?? new { ok = toolResult.Ok, errorMessage = toolResult.ErrorMessage };
        var envelope = new
        {
            type = "tool_result",
            tool = toolResult.Tool,
            requestId = toolResult.RequestId,
            result = payload
        };
        return JsonSerializer.Serialize(envelope, AgentJson.Options);
    }
}
