namespace LlmIde.Core.Projects;

/// <summary>
/// Initializes LLM IDE projects and registers them in the project registry.
/// </summary>
public sealed class ProjectInitializer
{
    /// <summary>
    /// The project metadata store.
    /// </summary>
    private readonly IProjectStore projectStore;

    /// <summary>
    /// The project registry service.
    /// </summary>
    private readonly ProjectRegistryService projectRegistryService;

    /// <summary>
    /// The IDE program root path.
    /// </summary>
    private readonly string ideProgramRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectInitializer"/> class.
    /// </summary>
    /// <param name="projectStore">The project metadata store.</param>
    /// <param name="projectRegistryService">The project registry service.</param>
    /// <param name="ideProgramRoot">The IDE program root path.</param>
    public ProjectInitializer(
        IProjectStore projectStore,
        ProjectRegistryService projectRegistryService,
        string ideProgramRoot)
    {
        this.projectStore = projectStore;
        this.projectRegistryService = projectRegistryService;
        this.ideProgramRoot = Path.GetFullPath(ideProgramRoot);
    }

    /// <summary>
    /// Initializes the project and registers it in the project registry.
    /// </summary>
    /// <param name="request">The initialization request.</param>
    /// <returns>The initialization result.</returns>
    public ProjectInitializationResult Initialize(ProjectInitializationRequest request)
    {
        string projectName = ProjectRegistryService.NormalizeInitializationName(request.Name);
        bool defaultNameRequested = !request.HasExplicitName || string.IsNullOrWhiteSpace(request.Name);

        if (defaultNameRequested && ProjectExists(projectName))
        {
            throw new InvalidOperationException("DefaultProject already exists.");
        }

        if (ProjectExists(projectName))
        {
            throw new InvalidOperationException($"Project already exists: {projectName}");
        }

        string projectRoot = ResolveProjectRoot(projectName, request.Path);
        ProjectInitializationResult result = projectStore.Initialize(projectRoot, projectName);
        projectRegistryService.Add(new ProjectRegistryEntry
        {
            Name = projectName,
            Path = result.ProjectRoot,
            CreatedAt = result.ProjectInfo.CreatedAt
        });

        return result;
    }

    /// <summary>
    /// Resolves the project root path.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <param name="requestedPath">The requested project path.</param>
    /// <returns>The project root path.</returns>
    private string ResolveProjectRoot(string projectName, string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return Path.Combine(ideProgramRoot, projectName);
        }

        return Path.GetFullPath(requestedPath);
    }

    /// <summary>
    /// Determines whether a project exists by name.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <returns>True when the project exists.</returns>
    private bool ProjectExists(string projectName)
    {
        return projectRegistryService.List().Any(project =>
            string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase));
    }
}
