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
        // Policies are common to all projects: read from the program's /policies folder.
        string path = Path.Combine(programRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.ImportanceRuleFileName);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
