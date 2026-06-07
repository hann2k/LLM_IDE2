using LlmIde.Core.Projects;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Loads artifact tagging rules from project policy files.
/// </summary>
public sealed class FileArtifactRuleStore : IArtifactRuleStore
{
    /// <inheritdoc />
    public string Load(string projectRoot)
    {
        string path = GetArtifactRulePath(projectRoot);
        EnsureFile(path);
        return File.ReadAllText(path);
    }

    private static void EnsureFile(string path)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(path))
        {
            File.WriteAllText(path, string.Empty);
        }
    }

    private static string GetArtifactRulePath(string projectRoot)
    {
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.PoliciesDirectoryName,
            LlmIdeLayout.ArtifactRuleFileName);
    }
}
