namespace LlmIde.Core.Artifacts;

/// <summary>
/// Coordinates artifact extraction and storage.
/// </summary>
public sealed class ArtifactService
{
    private readonly IArtifactStore artifactStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactService"/> class.
    /// </summary>
    /// <param name="artifactStore">The artifact store.</param>
    public ArtifactService(IArtifactStore artifactStore)
    {
        this.artifactStore = artifactStore;
    }

    /// <summary>
    /// Extracts explicit artifact candidates from response text.
    /// </summary>
    /// <param name="text">The response text.</param>
    /// <returns>The artifact candidates.</returns>
    public IReadOnlyList<ArtifactCandidate> ExtractCandidates(string text)
    {
        return ArtifactTagParser.Extract(text);
    }

    /// <summary>
    /// Lists stored artifacts.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The stored artifacts.</returns>
    public IReadOnlyList<Artifact> List(string projectRoot)
    {
        return artifactStore.List(projectRoot);
    }

    /// <summary>
    /// Gets a stored artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <returns>The stored artifact.</returns>
    public Artifact Get(string projectRoot, string artifactId)
    {
        return artifactStore.Get(projectRoot, artifactId);
    }

    /// <summary>
    /// Reads artifact content.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifact">The artifact metadata.</param>
    /// <returns>The artifact content.</returns>
    public string ReadContent(string projectRoot, Artifact artifact)
    {
        return artifactStore.ReadContent(projectRoot, artifact);
    }

    /// <summary>
    /// Saves a user-approved artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="candidate">The artifact candidate.</param>
    /// <param name="sourceRequestId">The source request identifier.</param>
    /// <returns>The stored artifact.</returns>
    public Artifact Save(string projectRoot, ArtifactCandidate candidate, string sourceRequestId)
    {
        Validate(candidate);
        return artifactStore.Save(projectRoot, candidate, sourceRequestId);
    }

    /// <summary>
    /// Updates a user-approved artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="candidate">The updated artifact candidate.</param>
    /// <returns>The updated artifact.</returns>
    public Artifact Update(string projectRoot, string artifactId, ArtifactCandidate candidate)
    {
        Validate(candidate);
        return artifactStore.Update(projectRoot, artifactId, candidate);
    }

    /// <summary>
    /// Removes an artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    public void Remove(string projectRoot, string artifactId)
    {
        artifactStore.Remove(projectRoot, artifactId);
    }

    private static void Validate(ArtifactCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Title))
        {
            throw new InvalidOperationException("Artifact title is required.");
        }

        if (string.IsNullOrWhiteSpace(candidate.Type))
        {
            throw new InvalidOperationException("Artifact type is required.");
        }

        if (string.IsNullOrWhiteSpace(candidate.Content))
        {
            throw new InvalidOperationException("Artifact content is required.");
        }
    }
}
