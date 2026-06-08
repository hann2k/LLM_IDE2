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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
