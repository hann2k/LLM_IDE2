using LlmIde.Core.Projects;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores the system rule in policies/system-rule.md.
/// </summary>
public sealed class FileSystemRuleStore : ISystemRuleStore
{
    /// <summary>
    /// The IDE program root containing the common policy files.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemRuleStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public FileSystemRuleStore(string? programRoot = null)
    {
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <summary>
    /// Loads the system rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The system rule content.</returns>
    public string Load(string projectRoot)
    {
        // Policies are common to all projects: read from the program's /policies folder.
        string path = Path.Combine(programRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.SystemRuleFileName);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
