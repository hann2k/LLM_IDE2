namespace LlmIde.Core.Projects;

/// <summary>
/// Manages the application-level recent project list.
/// </summary>
public sealed class RecentProjectService
{
    /// <summary>
    /// The recent project store.
    /// </summary>
    private readonly IRecentProjectStore recentProjectStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecentProjectService"/> class.
    /// </summary>
    /// <param name="recentProjectStore">The recent project store.</param>
    public RecentProjectService(IRecentProjectStore recentProjectStore)
    {
        this.recentProjectStore = recentProjectStore;
    }

    /// <summary>
    /// Gets all recent projects.
    /// </summary>
    /// <returns>The recent project entries.</returns>
    public IReadOnlyList<RecentProjectEntry> List()
    {
        return recentProjectStore.Load();
    }

    /// <summary>
    /// Records a project as recently opened.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="title">The project title.</param>
    public void RecordOpened(string projectRoot, string title)
    {
        string normalizedPath = Path.GetFullPath(projectRoot);
        List<RecentProjectEntry> projects = recentProjectStore.Load().ToList();
        projects.RemoveAll(project => string.Equals(project.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
        projects.Insert(0, new RecentProjectEntry
        {
            Path = normalizedPath,
            Title = title,
            LastOpenedAt = DateTimeOffset.UtcNow
        });
        recentProjectStore.Save(projects);
    }

    /// <summary>
    /// Removes a project from the recent project list.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>True when an entry was removed.</returns>
    public bool Remove(string projectRoot)
    {
        string normalizedPath = Path.GetFullPath(projectRoot);
        List<RecentProjectEntry> projects = recentProjectStore.Load().ToList();
        int removedCount = projects.RemoveAll(project => string.Equals(project.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
        recentProjectStore.Save(projects);
        return removedCount > 0;
    }

    /// <summary>
    /// Removes entries whose project directory no longer exists.
    /// </summary>
    /// <returns>The number of removed entries.</returns>
    public int RemoveMissing()
    {
        List<RecentProjectEntry> projects = recentProjectStore.Load().ToList();
        int originalCount = projects.Count;
        projects.RemoveAll(project => !Directory.Exists(project.Path));
        recentProjectStore.Save(projects);
        return originalCount - projects.Count;
    }
}
