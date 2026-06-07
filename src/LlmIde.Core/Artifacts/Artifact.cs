namespace LlmIde.Core.Artifacts;

/// <summary>
/// Represents a persisted project artifact.
/// </summary>
public sealed class Artifact
{
    /// <summary>
    /// Gets or sets the artifact identifier.
    /// </summary>
    public string ArtifactId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional intended target path.
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative content path below the artifacts directory.
    /// </summary>
    public string ContentPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source request identifier.
    /// </summary>
    public string SourceRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
