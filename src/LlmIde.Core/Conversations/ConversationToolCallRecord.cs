using System.Linq;
using LlmIde.Core.Agents;
using Framework.Common.Logger;

namespace LlmIde.Core.Conversations;

/// <summary>
/// Represents a single agent tool execution recorded during a chat request.
/// </summary>
public sealed class ConversationToolCallRecord
{
    /// <summary>
    /// Creates a sanitized tool call record from a tool result (host/query only, never secrets).
    /// </summary>
    /// <param name="requestId">The request identifier this tool call belongs to.</param>
    /// <param name="sequence">The execution order within the request (1-based).</param>
    /// <param name="toolResult">The tool result to record.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <returns>The tool call record.</returns>
    public static ConversationToolCallRecord Create(
        string requestId,
        int sequence,
        AgentToolResult toolResult,
        DateTimeOffset createdAt)
    {
        Log.Ins.Debug("시작");
        string target = string.Empty;
        string summary = string.Empty;

        if (toolResult.Result is IReadOnlyList<AgentToolDescriptor> tools)
        {
            // Tool discovery (list_tools): record which tools were offered, to reconstruct intent later.
            target = string.Join(",", tools.Select(tool => tool.Name));
            summary = $"tools={tools.Count}";
        }
        else if (toolResult.Result is FetchUrlResult fetchResult)
        {
            // Log host only, never the full URL (avoids leaking query-string secrets).
            target = Uri.TryCreate(fetchResult.Url, UriKind.Absolute, out Uri? uri) ? uri.Host : string.Empty;
            summary = $"status={fetchResult.StatusCode}, chars={fetchResult.Text.Length}, truncated={fetchResult.Truncated}";
        }
        else if (toolResult.Result is WebSearchResult searchResult)
        {
            target = searchResult.Query;
            summary = $"results={searchResult.Results.Count}";
        }
        else if (toolResult.Result is ReadFileResult readResult)
        {
            // The path is already project-relative (never absolute), so it is safe to record.
            target = readResult.Path;
            summary = $"chars={readResult.Text.Length}, truncated={readResult.Truncated}";
        }
        else if (toolResult.Result is ListFilesResult listResult)
        {
            target = listResult.Path;
            summary = $"entries={listResult.Entries.Count}, truncated={listResult.Truncated}";
        }
        else if (toolResult.Result is ArtifactsListResult artifactsList)
        {
            summary = $"count={artifactsList.Items.Count}";
        }
        else if (toolResult.Result is ReadArtifactResult artifactRead)
        {
            target = artifactRead.ArtifactId;
            summary = $"type={artifactRead.Type}, chars={artifactRead.Content.Length}";
        }
        else if (toolResult.Result is GetBodyResult getBody)
        {
            summary = $"hasSelection={getBody.HasSelection}, chars={getBody.Body.Length}";
        }
        else if (toolResult.Result is ProposeBodyEditResult proposeBody)
        {
            summary = $"chars={proposeBody.Length}";
        }

        return new ConversationToolCallRecord
        {
            RequestId = requestId,
            Sequence = sequence,
            Tool = toolResult.Tool,
            Target = target,
            Ok = toolResult.Ok,
            ResultSummary = summary,
            ErrorMessage = toolResult.ErrorMessage,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Gets or sets the chat request identifier this tool call belongs to.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution order within the request (1-based).
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sanitized target (host for fetch_url, query for web_search). Never contains secrets.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tool execution succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets a short, non-sensitive summary of the tool result.
    /// </summary>
    public string ResultSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message when the tool execution failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
