using System.Text.Json;
using Framework.Common.Logger;

namespace LlmIde.Core.Agents;

/// <summary>
/// Identifies the kind of a parsed model message.
/// </summary>
public enum ModelMessageKind
{
    /// <summary>
    /// A final answer message.
    /// </summary>
    Final,

    /// <summary>
    /// A tool request message.
    /// </summary>
    ToolRequest,

    /// <summary>
    /// A tool list (discovery) request message.
    /// </summary>
    ListTools,

    /// <summary>
    /// A message with an unknown type value.
    /// </summary>
    UnknownType,

    /// <summary>
    /// A message that could not be parsed as JSON.
    /// </summary>
    ParseError
}

/// <summary>
/// Represents a parsed model message.
/// </summary>
public sealed class ParsedModelMessage
{
    /// <summary>
    /// Gets or sets the message kind.
    /// </summary>
    public ModelMessageKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the final answer.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested tool name (the first request; kept for single-request consumers).
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested tool arguments (the first request; kept for single-request consumers).
    /// </summary>
    public JsonElement Arguments { get; set; }

    /// <summary>
    /// Gets or sets the full batch of tool requests (always at least one item for a tool_request).
    /// </summary>
    public IReadOnlyList<ParsedToolRequest> Requests { get; set; } = [];

    /// <summary>
    /// Gets or sets the parse error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Represents one tool request within a tool_request batch.
/// </summary>
public sealed class ParsedToolRequest
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool arguments.
    /// </summary>
    public JsonElement Arguments { get; set; }
}

/// <summary>
/// Parses model responses into final answers or tool requests.
/// </summary>
public static class AgentMessageParser
{
    /// <summary>
    /// Parses a model response.
    /// </summary>
    /// <param name="content">The model response content.</param>
    /// <returns>The parsed message.</returns>
    public static ParsedModelMessage Parse(string content)
    {
        Log.Ins.Debug("시작");
        string? json = ExtractJson(content);

        if (json is null)
        {
            return new ParsedModelMessage { Kind = ModelMessageKind.ParseError, Error = "응답에서 JSON을 찾을 수 없습니다." };
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out JsonElement typeElement))
            {
                return new ParsedModelMessage { Kind = ModelMessageKind.ParseError, Error = "type 필드가 없습니다." };
            }

            string type = typeElement.GetString() ?? string.Empty;

            if (string.Equals(type, "final", StringComparison.Ordinal))
            {
                string answer = root.TryGetProperty("answer", out JsonElement answerElement)
                    ? answerElement.GetString() ?? string.Empty
                    : string.Empty;
                return new ParsedModelMessage { Kind = ModelMessageKind.Final, Answer = answer };
            }

            if (string.Equals(type, "list_tools", StringComparison.Ordinal))
            {
                return new ParsedModelMessage { Kind = ModelMessageKind.ListTools };
            }

            if (string.Equals(type, "tool_request", StringComparison.Ordinal))
            {
                List<ParsedToolRequest> requests = ParseToolRequests(root);
                ParsedToolRequest first = requests.Count > 0 ? requests[0] : new ParsedToolRequest();
                return new ParsedModelMessage
                {
                    Kind = ModelMessageKind.ToolRequest,
                    Tool = first.Tool,
                    Arguments = first.Arguments,
                    Requests = requests
                };
            }

            return new ParsedModelMessage { Kind = ModelMessageKind.UnknownType, Error = $"알 수 없는 type: {type}" };
        }
        catch (JsonException ex)
        {
            return new ParsedModelMessage { Kind = ModelMessageKind.ParseError, Error = ex.Message };
        }
    }

    /// <summary>
    /// Parses the tool requests from a tool_request envelope, accepting both the batch form
    /// ("requests": [...]) and the legacy single form ("tool"/"arguments"); always returns at least one.
    /// </summary>
    /// <param name="root">The tool_request JSON object.</param>
    /// <returns>The parsed tool requests.</returns>
    private static List<ParsedToolRequest> ParseToolRequests(JsonElement root)
    {
        Log.Ins.Debug("시작");
        List<ParsedToolRequest> requests = [];

        if (root.TryGetProperty("requests", out JsonElement requestsElement)
            && requestsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in requestsElement.EnumerateArray())
            {
                requests.Add(ReadToolRequest(item));
            }
        }

        // Fall back to the legacy single form (also covers a model that ignores the array contract).
        if (requests.Count == 0)
        {
            requests.Add(ReadToolRequest(root));
        }

        return requests;
    }

    /// <summary>
    /// Reads one tool request (tool name and arguments) from a JSON object.
    /// </summary>
    /// <param name="element">The tool request JSON object.</param>
    /// <returns>The parsed tool request.</returns>
    private static ParsedToolRequest ReadToolRequest(JsonElement element)
    {
        Log.Ins.Debug("시작");
        string tool = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("tool", out JsonElement toolElement)
            ? toolElement.GetString() ?? string.Empty
            : string.Empty;
        JsonElement arguments = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("arguments", out JsonElement argumentsElement)
            ? argumentsElement.Clone()
            : default;
        return new ParsedToolRequest { Tool = tool, Arguments = arguments };
    }

    /// <summary>
    /// Extracts the first complete, balance-matched JSON object from model content. Handles code
    /// fences, surrounding prose, wrapper tags (e.g. &lt;tool_call&gt;), and trailing junk such as a
    /// stray extra closing brace or a second object — anything after the first balanced object is ignored.
    /// </summary>
    /// <param name="content">The model content.</param>
    /// <returns>The JSON substring, or null when no balanced object is found.</returns>
    private static string? ExtractJson(string content)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        string trimmed = content.Trim();

        // Remove a leading/trailing markdown code fence.
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewLine = trimmed.IndexOf('\n');

            if (firstNewLine >= 0)
            {
                trimmed = trimmed[(firstNewLine + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }

            trimmed = trimmed.Trim();
        }

        int start = trimmed.IndexOf('{');

        if (start < 0)
        {
            return null;
        }

        // Walk from the first '{' to its matching '}', tracking brace depth while ignoring braces
        // inside string literals. Returns at the first complete object so trailing junk cannot break parsing.
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int index = start; index < trimmed.Length; index++)
        {
            char current = trimmed[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
            }
            else if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return trimmed.Substring(start, index - start + 1);
                }
            }
        }

        return null;
    }
}
