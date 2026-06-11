using System.Text.Json;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Artifacts;

/// <summary>
/// Stores artifacts as files below the project metadata directory.
/// </summary>
public sealed class FileArtifactStore : IArtifactStore
{
    /// <inheritdoc />
    public IReadOnlyList<Artifact> List(string projectRoot)
    {
        Log.Ins.Debug("시작");
        return LoadIndex(projectRoot)
            .OrderBy(artifact => artifact.CreatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public Artifact Get(string projectRoot, string artifactId)
    {
        Log.Ins.Debug("시작");
        return LoadIndex(projectRoot).FirstOrDefault(artifact =>
            string.Equals(artifact.ArtifactId, artifactId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"산출물을 찾을 수 없습니다: {artifactId}");
    }

    /// <inheritdoc />
    public string ReadContent(string projectRoot, Artifact artifact)
    {
        Log.Ins.Debug("시작");
        string path = GetArtifactContentPath(projectRoot, artifact.ContentPath);
        return File.ReadAllText(path);
    }

    /// <inheritdoc />
    public Artifact Save(string projectRoot, ArtifactCandidate candidate, string sourceRequestId)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        List<Artifact> artifacts = LoadIndex(projectRoot);
        int index = artifacts.FindIndex(artifact =>
            string.Equals(artifact.ArtifactId, artifactId, StringComparison.Ordinal));

        if (index < 0)
        {
            throw new InvalidOperationException($"산출물을 찾을 수 없습니다: {artifactId}");
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
        Log.Ins.Debug("시작");
        List<Artifact> artifacts = LoadIndex(projectRoot);
        Artifact artifact = artifacts.FirstOrDefault(item =>
            string.Equals(item.ArtifactId, artifactId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"산출물을 찾을 수 없습니다: {artifactId}");

        artifacts.Remove(artifact);
        DeleteContentIfExists(projectRoot, artifact.ContentPath);
        SaveIndex(projectRoot, artifacts);
    }

    /// <inheritdoc />
    public Artifact SaveImage(string projectRoot, string title, string sourceImagePath)
    {
        Log.Ins.Debug("시작");
        if (!File.Exists(sourceImagePath))
        {
            throw new InvalidOperationException($"이미지 파일을 찾을 수 없습니다: {sourceImagePath}");
        }

        List<Artifact> artifacts = LoadIndex(projectRoot);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string artifactId = $"art_{Guid.NewGuid():N}";

        // Preserve the source image extension (the artifact's type is "image").
        string extension = Path.GetExtension(sourceImagePath).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        string fileName = $"{artifactId}{extension}";
        Artifact artifact = new Artifact
        {
            ArtifactId = artifactId,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileName(sourceImagePath) : title.Trim(),
            Type = "image",
            TargetPath = string.Empty,
            ContentPath = fileName,
            SourceRequestId = string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        EnsureArtifactsDirectory(projectRoot);
        File.Copy(sourceImagePath, GetArtifactContentPath(projectRoot, fileName), true);
        artifacts.Add(artifact);
        SaveIndex(projectRoot, artifacts);
        return artifact;
    }

    /// <inheritdoc />
    public string GetContentFullPath(string projectRoot, Artifact artifact)
    {
        Log.Ins.Debug("시작");
        return GetArtifactContentPath(projectRoot, artifact.ContentPath);
    }

    private static List<Artifact> LoadIndex(string projectRoot)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        EnsureArtifactsDirectory(projectRoot);
        string json = JsonSerializer.Serialize(artifacts, JsonOptions.Default);
        File.WriteAllText(GetIndexPath(projectRoot), json);
    }

    private static void WriteContent(string projectRoot, string contentPath, string content)
    {
        Log.Ins.Debug("시작");
        EnsureArtifactsDirectory(projectRoot);
        File.WriteAllText(GetArtifactContentPath(projectRoot, contentPath), content);
    }

    private static void DeleteContentIfExists(string projectRoot, string contentPath)
    {
        Log.Ins.Debug("시작");
        string path = GetArtifactContentPath(projectRoot, contentPath);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void EnsureArtifactsDirectory(string projectRoot)
    {
        Log.Ins.Debug("시작");
        Directory.CreateDirectory(GetArtifactsRoot(projectRoot));
    }

    private static string GetIndexPath(string projectRoot)
    {
        Log.Ins.Debug("시작");
        return Path.Combine(GetArtifactsRoot(projectRoot), LlmIdeLayout.ArtifactsIndexFileName);
    }

    private static string GetArtifactsRoot(string projectRoot)
    {
        Log.Ins.Debug("시작");
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.ArtifactsDirectoryName);
    }

    private static string GetArtifactContentPath(string projectRoot, string contentPath)
    {
        Log.Ins.Debug("시작");
        string fileName = Path.GetFileName(contentPath);
        return Path.Combine(GetArtifactsRoot(projectRoot), fileName);
    }

    private static string GetExtension(string type)
    {
        Log.Ins.Debug("시작");
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
