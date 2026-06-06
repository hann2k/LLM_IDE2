namespace LlmIde.Core.Projects;

/// <summary>
/// Stores project state snapshots.
/// </summary>
public interface IProjectStateStore
{
    /// <summary>
    /// Loads a project state.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project state.</returns>
    ProjectState Load(string projectRoot);

    /// <summary>
    /// Saves a project state.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="state">The project state.</param>
    void Save(string projectRoot, ProjectState state);
}
