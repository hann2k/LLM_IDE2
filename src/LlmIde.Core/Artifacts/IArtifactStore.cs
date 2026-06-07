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
}
