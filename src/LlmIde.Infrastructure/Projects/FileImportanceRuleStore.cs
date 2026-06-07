using LlmIde.Core.Projects;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Loads importance scoring rules from project policy files.
/// </summary>
public sealed class FileImportanceRuleStore : IImportanceRuleStore
{
    /// <summary>
    /// The IDE program root containing policy templates.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileImportanceRuleStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public FileImportanceRuleStore(string? programRoot = null)
    {
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <summary>
    /// Loads the importance scoring rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The importance scoring rule content.</returns>
    public string Load(string projectRoot)
    {
        string path = Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.PoliciesDirectoryName,
            LlmIdeLayout.ImportanceRuleFileName);

        if (!File.Exists(path))
        {
            EnsureProjectPolicyFile(path);
        }

        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    /// <summary>
    /// Ensures the project policy file exists from the program template.
    /// </summary>
    /// <param name="projectPolicyPath">The project policy file path.</param>
    private void EnsureProjectPolicyFile(string projectPolicyPath)
    {
        string templatePath = Path.Combine(
            programRoot,
            LlmIdeLayout.PoliciesDirectoryName,
            LlmIdeLayout.ImportanceRuleFileName);

        if (!File.Exists(templatePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(projectPolicyPath) ?? string.Empty);
        File.Copy(templatePath, projectPolicyPath, false);
    }
}
