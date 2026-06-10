namespace LlmIde.Core.Artifacts;

/// <summary>
/// Stores project artifacts.
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Lists stored artifacts.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The stored artifacts.</returns>
    IReadOnlyList<Artifact> List(string projectRoot);

    /// <summary>
    /// Gets a stored artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <returns>The stored artifact.</returns>
    Artifact Get(string projectRoot, string artifactId);

    /// <summary>
    /// Reads artifact content.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifact">The artifact metadata.</param>
    /// <returns>The artifact content.</returns>
    string ReadContent(string projectRoot, Artifact artifact);

    /// <summary>
    /// Saves a new artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="candidate">The artifact candidate.</param>
    /// <param name="sourceRequestId">The source request identifier.</param>
    /// <returns>The stored artifact.</returns>
    Artifact Save(string projectRoot, ArtifactCandidate candidate, string sourceRequestId);

    /// <summary>
    /// Updates a stored artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="candidate">The updated artifact candidate.</param>
    /// <returns>The updated artifact.</returns>
    Artifact Update(string projectRoot, string artifactId, ArtifactCandidate candidate);

    /// <summary>
    /// Removes a stored artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    void Remove(string projectRoot, string artifactId);

    /// <summary>
    /// Saves an image file as a binary (type="image") artifact, copying its bytes into the
    /// artifacts directory and preserving the source extension.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="title">The artifact title (defaults to the source file name when empty).</param>
    /// <param name="sourceImagePath">The absolute path of the source image file.</param>
    /// <returns>The stored image artifact.</returns>
    Artifact SaveImage(string projectRoot, string title, string sourceImagePath);

    /// <summary>
    /// Gets the absolute path of an artifact's content file (used to display images and to build
    /// body image references).
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifact">The artifact metadata.</param>
    /// <returns>The absolute content file path.</returns>
    string GetContentFullPath(string projectRoot, Artifact artifact);
}
