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
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
