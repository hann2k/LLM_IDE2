using System.Text.Encodings.Web;
using System.Text.Json;

namespace LlmIde.Infrastructure.Json;

/// <summary>
/// Provides shared JSON serializer options.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Gets the default JSON serializer options.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Gets compact JSON serializer options for JSONL records.
    /// </summary>
    public static JsonSerializerOptions Compact { get; } = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
