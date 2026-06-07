namespace LlmIde.Core.Projects;

/// <summary>
/// Manages the IDE-level project registry.
/// </summary>
public sealed class ProjectRegistryService
{
    /// <summary>
    /// The default project name.
    /// </summary>
    public const string DefaultProjectName = "DefaultProject";

    /// <summary>
    /// The project registry store.
    /// </summary>
    private readonly IProjectRegistryStore projectRegistryStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRegistryService"/> class.
    /// </summary>
    /// <param name="projectRegistryStore">The project registry store.</param>
    public ProjectRegistryService(IProjectRegistryStore projectRegistryStore)
    {
        this.projectRegistryStore = projectRegistryStore;
    }

    /// <summary>
    /// Gets all registered projects.
    /// </summary>
    /// <returns>The registered projects.</returns>
    public IReadOnlyList<ProjectRegistryEntry> List()
    {
        return projectRegistryStore.Load().Projects;
    }

    /// <summary>
    /// Gets a project by name.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The project entry.</returns>
    public ProjectRegistryEntry GetRequired(string name)
    {
        ProjectRegistryEntry? project = Find(name);

        if (project is null)
        {
            throw new InvalidOperationException($"Project does not exist: {name}");
        }

        return project;
    }

    /// <summary>
    /// Adds a project entry.
    /// </summary>
    /// <param name="entry">The project entry.</param>
    public void Add(ProjectRegistryEntry entry)
    {
        ProjectRegistryDocument registry = projectRegistryStore.Load();

        if (ContainsName(registry, entry.Name))
        {
            throw new InvalidOperationException($"Project already exists: {entry.Name}");
        }

        registry.Projects.Add(entry);
        projectRegistryStore.Save(registry);
    }

    /// <summary>
    /// Removes a project entry by name.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>True when a project was removed.</returns>
    public bool Remove(string name)
    {
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        int removedCount = registry.Projects.RemoveAll(project => IsSameName(project.Name, name));
        projectRegistryStore.Save(registry);
        return removedCount > 0;
    }

    /// <summary>
    /// Renames a project entry.
    /// </summary>
    /// <param name="name">The current project name.</param>
    /// <param name="newName">The new project name.</param>
    public void Rename(string name, string newName)
    {
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        ProjectRegistryEntry project = GetRequiredFrom(registry, name);

        if (ContainsName(registry, newName))
        {
            throw new InvalidOperationException($"Project already exists: {newName}");
        }

        project.Name = NormalizeRequiredName(newName);
        projectRegistryStore.Save(registry);
    }

    /// <summary>
    /// Updates a project path.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <param name="newPath">The new project root path.</param>
    public void Move(string name, string newPath)
    {
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        ProjectRegistryEntry project = GetRequiredFrom(registry, name);
        project.Path = Path.GetFullPath(newPath);
        projectRegistryStore.Save(registry);
    }

    /// <summary>
    /// Normalizes an initialization name.
    /// </summary>
    /// <param name="name">The requested name.</param>
    /// <returns>The normalized name.</returns>
    public static string NormalizeInitializationName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return DefaultProjectName;
        }

        return name.Trim();
    }

    /// <summary>
    /// Finds a project by name.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The project entry, or null.</returns>
    private ProjectRegistryEntry? Find(string name)
    {
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        return registry.Projects.FirstOrDefault(project => IsSameName(project.Name, name));
    }

    /// <summary>
    /// Finds a required project in a registry.
    /// </summary>
    /// <param name="registry">The registry document.</param>
    /// <param name="name">The project name.</param>
    /// <returns>The project entry.</returns>
    private static ProjectRegistryEntry GetRequiredFrom(ProjectRegistryDocument registry, string name)
    {
        ProjectRegistryEntry? project = registry.Projects.FirstOrDefault(entry => IsSameName(entry.Name, name));

        if (project is null)
        {
            throw new InvalidOperationException($"Project does not exist: {name}");
        }

        return project;
    }

    /// <summary>
    /// Determines whether a registry contains a project name.
    /// </summary>
    /// <param name="registry">The registry document.</param>
    /// <param name="name">The project name.</param>
    /// <returns>True when the project name exists.</returns>
    private static bool ContainsName(ProjectRegistryDocument registry, string name)
    {
        return registry.Projects.Any(project => IsSameName(project.Name, name));
    }

    /// <summary>
    /// Normalizes a required project name.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The normalized project name.</returns>
    private static string NormalizeRequiredName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("프로젝트 이름은 필수입니다.");
        }

        return name.Trim();
    }

    /// <summary>
    /// Compares project names.
    /// </summary>
    /// <param name="left">The first name.</param>
    /// <param name="right">The second name.</param>
    /// <returns>True when names are equal.</returns>
    private static bool IsSameName(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
