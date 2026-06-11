using Framework.Common.Logger;
namespace LlmIde.Core.Projects;

/// <summary>
/// Manages the IDE-level project registry.
/// </summary>
public sealed class ProjectRegistryService
{
    /// <summary>
    /// The default project identifier.
    /// </summary>
    public const string DefaultProjectId = "DefaultProject";

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
        Log.Ins.Debug("시작");
        this.projectRegistryStore = projectRegistryStore;
    }

    /// <summary>
    /// Gets all registered projects.
    /// </summary>
    /// <returns>The registered projects.</returns>
    public IReadOnlyList<ProjectRegistryEntry> List()
    {
        Log.Ins.Debug("시작");
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        NormalizeRegistryEntries(registry);
        return registry.Projects;
    }

    /// <summary>
    /// Gets the next numeric project identifier.
    /// </summary>
    /// <returns>The next numeric project identifier.</returns>
    public string NextProjectPId()
    {
        Log.Ins.Debug("시작");
        long max = 0;

        foreach (ProjectRegistryEntry project in List())
        {
            if (long.TryParse(project.PId, out long value) && value > max)
            {
                max = value;
            }
        }

        return (max + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a project by project identifier.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    /// <returns>The project entry.</returns>
    public ProjectRegistryEntry GetRequired(string pId)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryEntry? project = Find(pId);

        if (project is null)
        {
            throw new InvalidOperationException($"Project does not exist: {pId}");
        }

        return project;
    }

    /// <summary>
    /// Adds a project entry.
    /// </summary>
    /// <param name="entry">The project entry.</param>
    public void Add(ProjectRegistryEntry entry)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        NormalizeRegistryEntries(registry);

        entry.PId = NormalizeRequiredPId(entry.PId);
        entry.Name = NormalizeDisplayName(entry.Name, entry.PId);

        if (ContainsPId(registry, entry.PId))
        {
            throw new InvalidOperationException($"Project already exists: {entry.PId}");
        }

        registry.Projects.Add(entry);
        projectRegistryStore.Save(registry);
    }

    /// <summary>
    /// Removes a project entry by project identifier.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    /// <returns>True when a project was removed.</returns>
    public bool Remove(string pId)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        NormalizeRegistryEntries(registry);
        int removedCount = registry.Projects.RemoveAll(project => IsSamePId(project.PId, pId));
        projectRegistryStore.Save(registry);
        return removedCount > 0;
    }

    /// <summary>
    /// Renames the user-visible project name.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    /// <param name="newName">The new user-visible project name.</param>
    public void Rename(string pId, string newName)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        NormalizeRegistryEntries(registry);
        ProjectRegistryEntry project = GetRequiredFrom(registry, pId);
        project.Name = NormalizeDisplayName(newName, project.PId);
        projectRegistryStore.Save(registry);
    }

    /// <summary>
    /// Changes a project identifier.
    /// </summary>
    /// <param name="pId">The current project identifier.</param>
    /// <param name="newPId">The new project identifier.</param>
    public void ChangePId(string pId, string newPId)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        NormalizeRegistryEntries(registry);
        ProjectRegistryEntry project = GetRequiredFrom(registry, pId);
        string normalizedNewPId = NormalizeRequiredPId(newPId);

        if (ContainsPId(registry, normalizedNewPId))
        {
            throw new InvalidOperationException($"Project already exists: {normalizedNewPId}");
        }

        project.PId = normalizedNewPId;
        projectRegistryStore.Save(registry);
    }

    /// <summary>
    /// Updates a project path.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    /// <param name="newPath">The new project root path.</param>
    public void Move(string pId, string newPath)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        NormalizeRegistryEntries(registry);
        ProjectRegistryEntry project = GetRequiredFrom(registry, pId);
        project.Path = Path.GetFullPath(newPath);
        projectRegistryStore.Save(registry);
    }

    /// <summary>
    /// Normalizes an initialization project identifier.
    /// </summary>
    /// <param name="pId">The requested project identifier.</param>
    /// <returns>The normalized project identifier.</returns>
    public static string NormalizeInitializationPId(string pId)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(pId))
        {
            return DefaultProjectId;
        }

        return NormalizeRequiredPId(pId);
    }

    /// <summary>
    /// Normalizes an initialization display name.
    /// </summary>
    /// <param name="name">The requested display name.</param>
    /// <param name="pId">The project identifier.</param>
    /// <returns>The normalized display name.</returns>
    public static string NormalizeInitializationDisplayName(string name, string pId)
    {
        Log.Ins.Debug("시작");
        return NormalizeDisplayName(name, pId);
    }

    /// <summary>
    /// Finds a project by project identifier.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    /// <returns>The project entry, or null.</returns>
    private ProjectRegistryEntry? Find(string pId)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryDocument registry = projectRegistryStore.Load();
        NormalizeRegistryEntries(registry);
        return registry.Projects.FirstOrDefault(project => IsSamePId(project.PId, pId));
    }

    /// <summary>
    /// Finds a required project in a registry.
    /// </summary>
    /// <param name="registry">The registry document.</param>
    /// <param name="pId">The project identifier.</param>
    /// <returns>The project entry.</returns>
    private static ProjectRegistryEntry GetRequiredFrom(ProjectRegistryDocument registry, string pId)
    {
        Log.Ins.Debug("시작");
        ProjectRegistryEntry? project = registry.Projects.FirstOrDefault(entry => IsSamePId(entry.PId, pId));

        if (project is null)
        {
            throw new InvalidOperationException($"Project does not exist: {pId}");
        }

        return project;
    }

    /// <summary>
    /// Determines whether a registry contains a project identifier.
    /// </summary>
    /// <param name="registry">The registry document.</param>
    /// <param name="pId">The project identifier.</param>
    /// <returns>True when the project identifier exists.</returns>
    private static bool ContainsPId(ProjectRegistryDocument registry, string pId)
    {
        Log.Ins.Debug("시작");
        return registry.Projects.Any(project => IsSamePId(project.PId, pId));
    }

    /// <summary>
    /// Normalizes a required project identifier.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    /// <returns>The normalized project identifier.</returns>
    private static string NormalizeRequiredPId(string pId)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(pId))
        {
            throw new InvalidOperationException("프로젝트 pID는 필수입니다.");
        }

        string normalizedPId = pId.Trim();

        if (!IsEnglishPId(normalizedPId))
        {
            throw new InvalidOperationException("프로젝트 pID는 영문자로 시작하고 영문자, 숫자, '-', '_'만 사용할 수 있습니다.");
        }

        return normalizedPId;
    }

    /// <summary>
    /// Normalizes a display name.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <param name="fallbackPId">The fallback project identifier.</param>
    /// <returns>The normalized display name.</returns>
    private static string NormalizeDisplayName(string name, string fallbackPId)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallbackPId.Trim();
        }

        return name.Trim();
    }

    /// <summary>
    /// Compares project identifiers.
    /// </summary>
    /// <param name="left">The first project identifier.</param>
    /// <param name="right">The second project identifier.</param>
    /// <returns>True when project identifiers are equal.</returns>
    private static bool IsSamePId(string left, string right)
    {
        Log.Ins.Debug("시작");
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a project identifier uses allowed English characters.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    /// <returns>True when the project identifier is valid.</returns>
    private static bool IsEnglishPId(string pId)
    {
        Log.Ins.Debug("시작");
        if (pId.Length == 0 || !(IsAsciiLetter(pId[0]) || IsAsciiDigit(pId[0])))
        {
            return false;
        }

        foreach (char character in pId)
        {
            if (IsAsciiLetter(character) || IsAsciiDigit(character) || character == '-' || character == '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a character is an ASCII letter.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>True when the character is an ASCII letter.</returns>
    private static bool IsAsciiLetter(char character)
    {
        Log.Ins.Debug("시작");
        return (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
    }

    /// <summary>
    /// Determines whether a character is an ASCII digit.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>True when the character is an ASCII digit.</returns>
    private static bool IsAsciiDigit(char character)
    {
        Log.Ins.Debug("시작");
        return character >= '0' && character <= '9';
    }

    /// <summary>
    /// Normalizes registry entries loaded from older registry files.
    /// </summary>
    /// <param name="registry">The registry document.</param>
    private static void NormalizeRegistryEntries(ProjectRegistryDocument registry)
    {
        Log.Ins.Debug("시작");
        foreach (ProjectRegistryEntry project in registry.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.PId))
            {
                project.PId = project.Name;
            }

            if (string.IsNullOrWhiteSpace(project.Name))
            {
                project.Name = project.PId;
            }
        }
    }
}
