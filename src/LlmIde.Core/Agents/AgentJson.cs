using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmIde.Core.Agents;

/// <summary>
/// Provides JSON options for the agent protocol (camelCase, matching the model contract).
/// </summary>
public static class AgentJson
{
    /// <summary>
    /// Gets the agent protocol JSON options.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
    {
        // Keep non-ASCII (Korean) readable instead of \uXXXX escapes, both in the payload sent to the
        // model and in the diagnostic log. The output is JSON for an LLM, never HTML, so relaxed escaping is safe.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
