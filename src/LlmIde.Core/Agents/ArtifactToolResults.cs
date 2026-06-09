namespace LlmIde.Core.Agents;

/// <summary>
/// Describes the result of the artifacts_list tool.
/// </summary>
public sealed class ArtifactsListResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the listing succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the listed artifact summaries.
    /// </summary>
    public IReadOnlyList<ArtifactSummary> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Describes one artifact in an artifacts_list result (metadata only, no content).
/// </summary>
public sealed class ArtifactSummary
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
    /// Gets or sets the source request identifier.
    /// </summary>
    public string SourceRequestId { get; set; } = string.Empty;
}

/// <summary>
/// Describes the result of the read_artifact tool.
/// </summary>
public sealed class ReadArtifactResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the read succeeded.
    /// </summary>
    public bool Ok { get; set; }

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
    /// Gets or sets the source request identifier.
    /// </summary>
    public string SourceRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
