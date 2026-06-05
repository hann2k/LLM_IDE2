using System.Text.Json;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores recent projects in the user application data directory.
/// </summary>
public sealed class JsonRecentProjectStore : IRecentProjectStore
{
    /// <summary>
    /// The recent project file path.
    /// </summary>
    private readonly string recentProjectsPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRecentProjectStore"/> class.
    /// </summary>
    public JsonRecentProjectStore()
        : this(GetDefaultApplicationDataRoot())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRecentProjectStore"/> class.
    /// </summary>
    /// <param name="applicationDataRoot">The application data root path.</param>
    public JsonRecentProjectStore(string applicationDataRoot)
    {
        recentProjectsPath = Path.Combine(applicationDataRoot, "LlmIde", "recent-projects.json");
    }

    /// <summary>
    /// Loads recent projects.
    /// </summary>
    /// <returns>The recent project entries.</returns>
    public IReadOnlyList<RecentProjectEntry> Load()
    {
        if (!File.Exists(recentProjectsPath))
        {
            return [];
        }

        string json = File.ReadAllText(recentProjectsPath);
        List<RecentProjectEntry>? projects = JsonSerializer.Deserialize<List<RecentProjectEntry>>(json, JsonOptions.Default);
        return projects ?? [];
    }

    /// <summary>
    /// Saves recent projects.
    /// </summary>
    /// <param name="projects">The recent project entries.</param>
    public void Save(IReadOnlyList<RecentProjectEntry> projects)
    {
        string? directory = Path.GetDirectoryName(recentProjectsPath);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(projects, JsonOptions.Default);
        File.WriteAllText(recentProjectsPath, json);
    }

    /// <summary>
    /// Gets the default application data root path.
    /// </summary>
    /// <returns>The application data root path.</returns>
    private static string GetDefaultApplicationDataRoot()
    {
        string applicationDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!string.IsNullOrWhiteSpace(applicationDataRoot))
        {
            return applicationDataRoot;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
