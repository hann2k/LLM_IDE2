using System.Text.Json;

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
    /// Gets or sets the requested tool name.
    /// </summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested tool arguments.
    /// </summary>
    public JsonElement Arguments { get; set; }

    /// <summary>
    /// Gets or sets the parse error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;
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
                string tool = root.TryGetProperty("tool", out JsonElement toolElement)
                    ? toolElement.GetString() ?? string.Empty
                    : string.Empty;
                JsonElement arguments = root.TryGetProperty("arguments", out JsonElement argumentsElement)
                    ? argumentsElement.Clone()
                    : default;
                return new ParsedModelMessage { Kind = ModelMessageKind.ToolRequest, Tool = tool, Arguments = arguments };
            }

            return new ParsedModelMessage { Kind = ModelMessageKind.UnknownType, Error = $"알 수 없는 type: {type}" };
        }
        catch (JsonException ex)
        {
            return new ParsedModelMessage { Kind = ModelMessageKind.ParseError, Error = ex.Message };
        }
    }

    /// <summary>
    /// Extracts a JSON object substring from model content (handles code fences and surrounding text).
    /// </summary>
    /// <param name="content">The model content.</param>
    /// <returns>The JSON substring, or null when none is found.</returns>
    private static string? ExtractJson(string content)
    {
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
        int end = trimmed.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed.Substring(start, end - start + 1);
    }
}
