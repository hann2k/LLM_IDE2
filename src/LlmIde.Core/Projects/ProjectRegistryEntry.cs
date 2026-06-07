using System.Text.Json.Serialization;

namespace LlmIde.Core.Projects;

/// <summary>
/// Represents one project entry in the IDE-level project registry.
/// </summary>
public sealed class ProjectRegistryEntry
{
    /// <summary>
    /// Gets or sets the English project identifier.
    /// </summary>
    [JsonPropertyName("pID")]
    public string PId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-visible project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project root path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
