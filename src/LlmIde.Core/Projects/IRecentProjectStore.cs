namespace LlmIde.Core.Projects;

/// <summary>
/// Provides application-level recent project persistence.
/// </summary>
public interface IRecentProjectStore
{
    /// <summary>
    /// Loads recent projects.
    /// </summary>
    /// <returns>The recent project entries.</returns>
    IReadOnlyList<RecentProjectEntry> Load();

    /// <summary>
    /// Saves recent projects.
    /// </summary>
    /// <param name="projects">The recent project entries.</param>
    void Save(IReadOnlyList<RecentProjectEntry> projects);
}
