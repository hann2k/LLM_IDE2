namespace LlmIde.Core.Projects;

/// <summary>
/// Initializes LLM IDE projects and registers them as recent projects.
/// </summary>
public sealed class ProjectInitializer
{
    /// <summary>
    /// The project metadata store.
    /// </summary>
    private readonly IProjectStore projectStore;

    /// <summary>
    /// The recent project service.
    /// </summary>
    private readonly RecentProjectService recentProjectService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectInitializer"/> class.
    /// </summary>
    /// <param name="projectStore">The project metadata store.</param>
    /// <param name="recentProjectService">The recent project service.</param>
    public ProjectInitializer(IProjectStore projectStore, RecentProjectService recentProjectService)
    {
        this.projectStore = projectStore;
        this.recentProjectService = recentProjectService;
    }

    /// <summary>
    /// Initializes the project and registers it in the recent project list.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The initialization result.</returns>
    public ProjectInitializationResult Initialize(string projectRoot)
    {
        ProjectInitializationResult result = projectStore.Initialize(projectRoot);
        recentProjectService.RecordOpened(result.ProjectRoot, result.ProjectInfo.Title);
        return result;
    }
}
