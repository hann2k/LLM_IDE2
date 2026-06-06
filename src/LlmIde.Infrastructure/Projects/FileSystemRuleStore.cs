using LlmIde.Core.Projects;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores the system rule in policies/system-rule.md.
/// </summary>
public sealed class FileSystemRuleStore : ISystemRuleStore
{
    /// <summary>
    /// Loads the system rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The system rule content.</returns>
    public string Load(string projectRoot)
    {
        string path = GetSystemRulePath(projectRoot);

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, string.Empty);
            return string.Empty;
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Gets the system rule file path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The system rule file path.</returns>
    private static string GetSystemRulePath(string projectRoot)
    {
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.PoliciesDirectoryName,
            LlmIdeLayout.SystemRuleFileName);
    }
}
