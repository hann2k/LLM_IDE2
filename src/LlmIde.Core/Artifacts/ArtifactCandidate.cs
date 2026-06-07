namespace LlmIde.Core.Artifacts;

/// <summary>
/// Represents an artifact candidate extracted from an AI response.
/// </summary>
public sealed class ArtifactCandidate
{
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
    /// Gets or sets the artifact content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
