namespace LlmIde.Core.Projects;

/// <summary>
/// Stores the project system rule.
/// </summary>
public interface ISystemRuleStore
{
    /// <summary>
    /// Loads the system rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The system rule content.</returns>
    string Load(string projectRoot);
}
