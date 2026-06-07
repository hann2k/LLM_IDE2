namespace LlmIde.Core.Projects;

/// <summary>
/// Loads artifact tagging rules.
/// </summary>
public interface IArtifactRuleStore
{
    /// <summary>
    /// Loads the artifact rule text for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The artifact rule text.</returns>
    string Load(string projectRoot);
}
