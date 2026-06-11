using System.Text.Json;
using Framework.Common.Logger;

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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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

    /// <summary>
    /// Builds the batched tool_result envelope returned after a tool_request batch runs. Each entry
    /// carries its own ok/result/error so partial failures are reported without aborting the batch.
    /// </summary>
    /// <param name="toolResults">The tool results in execution order.</param>
    /// <returns>The tool_result JSON with a results array.</returns>
    public static string ToolResults(IReadOnlyList<AgentToolResult> toolResults)
    {
        Log.Ins.Debug("시작");
        var results = toolResults.Select(toolResult => new
        {
            tool = toolResult.Tool,
            requestId = toolResult.RequestId,
            ok = toolResult.Ok,
            result = toolResult.Result,
            errorMessage = string.IsNullOrEmpty(toolResult.ErrorMessage) ? null : toolResult.ErrorMessage
        });
        var envelope = new
        {
            type = "tool_result",
            results
        };
        return JsonSerializer.Serialize(envelope, AgentJson.Options);
    }
}
