using System.Text.Json;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;

namespace LlmIde.Infrastructure.Artifacts;

/// <summary>
/// Stores artifacts as files below the project metadata directory.
/// </summary>
public sealed class FileArtifactStore : IArtifactStore
{
    /// <inheritdoc />
    public IReadOnlyList<Artifact> List(string projectRoot)
    {
        return LoadIndex(projectRoot)
            .OrderBy(artifact => artifact.CreatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public Artifact Get(string projectRoot, string artifactId)
    {
        return LoadIndex(projectRoot).FirstOrDefault(artifact =>
            string.Equals(artifact.ArtifactId, artifactId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Artifact was not found: {artifactId}");
    }

    /// <inheritdoc />
    public string ReadContent(string projectRoot, Artifact artifact)
    {
        string path = GetArtifactContentPath(projectRoot, artifact.ContentPath);
        return File.ReadAllText(path);
    }

    /// <inheritdoc />
    public Artifact Save(string projectRoot, ArtifactCandidate candidate, string sourceRequestId)
    {
        List<Artifact> artifacts = LoadIndex(projectRoot);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string artifactId = $"art_{Guid.NewGuid():N}";
        string fileName = $"{artifactId}{GetExtension(candidate.Type)}";
        Artifact artifact = new Artifact
        {
            ArtifactId = artifactId,
            Title = candidate.Title.Trim(),
            Type = candidate.Type.Trim(),
            TargetPath = candidate.TargetPath.Trim(),
            ContentPath = fileName,
            SourceRequestId = sourceRequestId.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        WriteContent(projectRoot, artifact.ContentPath, candidate.Content);
        artifacts.Add(artifact);
        SaveIndex(projectRoot, artifacts);
        return artifact;
    }

    /// <inheritdoc />
    public Artifact Update(string projectRoot, string artifactId, ArtifactCandidate candidate)
    {
        List<Artifact> artifacts = LoadIndex(projectRoot);
        int index = artifacts.FindIndex(artifact =>
            string.Equals(artifact.ArtifactId, artifactId, StringComparison.Ordinal));

        if (index < 0)
        {
            throw new InvalidOperationException($"Artifact was not found: {artifactId}");
        }

        Artifact artifact = artifacts[index];
        string extension = GetExtension(candidate.Type);
        string newContentPath = Path.ChangeExtension(artifact.ContentPath, extension);

        if (!string.Equals(artifact.ContentPath, newContentPath, StringComparison.Ordinal))
        {
            DeleteContentIfExists(projectRoot, artifact.ContentPath);
            artifact.ContentPath = Path.GetFileName(newContentPath);
        }

        artifact.Title = candidate.Title.Trim();
        artifact.Type = candidate.Type.Trim();
        artifact.TargetPath = candidate.TargetPath.Trim();
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        WriteContent(projectRoot, artifact.ContentPath, candidate.Content);
        artifacts[index] = artifact;
        SaveIndex(projectRoot, artifacts);
        return artifact;
    }

    /// <inheritdoc />
    public void Remove(string projectRoot, string artifactId)
    {
        List<Artifact> artifacts = LoadIndex(projectRoot);
        Artifact artifact = artifacts.FirstOrDefault(item =>
            string.Equals(item.ArtifactId, artifactId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Artifact was not found: {artifactId}");

        artifacts.Remove(artifact);
        DeleteContentIfExists(projectRoot, artifact.ContentPath);
        SaveIndex(projectRoot, artifacts);
    }

    private static List<Artifact> LoadIndex(string projectRoot)
    {
        EnsureArtifactsDirectory(projectRoot);
        string path = GetIndexPath(projectRoot);

        if (!File.Exists(path))
        {
            SaveIndex(projectRoot, []);
            return [];
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<Artifact>>(json, JsonOptions.Default) ?? [];
    }

    private static void SaveIndex(string projectRoot, List<Artifact> artifacts)
    {
        EnsureArtifactsDirectory(projectRoot);
        string json = JsonSerializer.Serialize(artifacts, JsonOptions.Default);
        File.WriteAllText(GetIndexPath(projectRoot), json);
    }

    private static void WriteContent(string projectRoot, string contentPath, string content)
    {
        EnsureArtifactsDirectory(projectRoot);
        File.WriteAllText(GetArtifactContentPath(projectRoot, contentPath), content);
    }

    private static void DeleteContentIfExists(string projectRoot, string contentPath)
    {
        string path = GetArtifactContentPath(projectRoot, contentPath);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void EnsureArtifactsDirectory(string projectRoot)
    {
        Directory.CreateDirectory(GetArtifactsRoot(projectRoot));
    }

    private static string GetIndexPath(string projectRoot)
    {
        return Path.Combine(GetArtifactsRoot(projectRoot), LlmIdeLayout.ArtifactsIndexFileName);
    }

    private static string GetArtifactsRoot(string projectRoot)
    {
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.ArtifactsDirectoryName);
    }

    private static string GetArtifactContentPath(string projectRoot, string contentPath)
    {
        string fileName = Path.GetFileName(contentPath);
        return Path.Combine(GetArtifactsRoot(projectRoot), fileName);
    }

    private static string GetExtension(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "csharp" => ".cs",
            "cs" => ".cs",
            "json" => ".json",
            "markdown" => ".md",
            "md" => ".md",
            "command" => ".txt",
            "plan" => ".md",
            "note" => ".md",
            _ => ".txt"
        };
    }
}
