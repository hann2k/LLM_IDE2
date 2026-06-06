namespace LlmIde.Core.Projects;

/// <summary>
/// Provides criteria persistence for a project.
/// </summary>
public interface ICriteriaStore
{
    /// <summary>
    /// Loads criteria for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project criteria.</returns>
    IReadOnlyList<Criterion> Load(string projectRoot);

    /// <summary>
    /// Saves criteria for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="criteria">The project criteria.</param>
    void Save(string projectRoot, IReadOnlyList<Criterion> criteria);
}
