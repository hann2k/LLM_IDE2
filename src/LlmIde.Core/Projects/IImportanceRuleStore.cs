namespace LlmIde.Core.Projects;

/// <summary>
/// Loads importance scoring rules from project policy files.
/// </summary>
public interface IImportanceRuleStore
{
    /// <summary>
    /// Loads the importance scoring rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The importance scoring rule content.</returns>
    string Load(string projectRoot);
}
