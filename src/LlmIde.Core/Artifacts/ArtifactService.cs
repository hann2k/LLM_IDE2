using Framework.Common.Logger;
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
        Log.Ins.Debug("시작");
        this.artifactStore = artifactStore;
    }

    /// <summary>
    /// Extracts explicit artifact candidates from response text.
    /// </summary>
    /// <param name="text">The response text.</param>
    /// <returns>The artifact candidates.</returns>
    public IReadOnlyList<ArtifactCandidate> ExtractCandidates(string text)
    {
        Log.Ins.Debug("시작");
        return ArtifactTagParser.Extract(text);
    }

    /// <summary>
    /// Lists stored artifacts.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The stored artifacts.</returns>
    public IReadOnlyList<Artifact> List(string projectRoot)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        artifactStore.Remove(projectRoot, artifactId);
    }

    /// <summary>
    /// Saves an image file as a binary (type="image") artifact.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="title">The artifact title (defaults to the source file name when empty).</param>
    /// <param name="sourceImagePath">The absolute path of the source image file.</param>
    /// <returns>The stored image artifact.</returns>
    public Artifact SaveImage(string projectRoot, string title, string sourceImagePath)
    {
        Log.Ins.Debug("시작");
        return artifactStore.SaveImage(projectRoot, title, sourceImagePath);
    }

    /// <summary>
    /// Gets the absolute path of an artifact's content file.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifact">The artifact metadata.</param>
    /// <returns>The absolute content file path.</returns>
    public string GetContentFullPath(string projectRoot, Artifact artifact)
    {
        Log.Ins.Debug("시작");
        return artifactStore.GetContentFullPath(projectRoot, artifact);
    }

    /// <summary>
    /// Determines whether an artifact is a binary image.
    /// </summary>
    /// <param name="artifact">The artifact.</param>
    /// <returns>True when the artifact type is "image".</returns>
    public static bool IsImage(Artifact artifact)
    {
        Log.Ins.Debug("시작");
        return string.Equals(artifact.Type, "image", StringComparison.OrdinalIgnoreCase);
    }

    private static void Validate(ArtifactCandidate candidate)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(candidate.Title))
        {
            throw new InvalidOperationException("산출물 제목은 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(candidate.Type))
        {
            throw new InvalidOperationException("산출물 유형은 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(candidate.Content))
        {
            throw new InvalidOperationException("산출물 내용은 필수입니다.");
        }
    }
}
