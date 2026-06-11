using System.Text.Json;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores project state in a project's project-state.json file.
/// </summary>
public sealed class JsonProjectStateStore : IProjectStateStore
{
    /// <summary>
    /// Loads project state.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project state.</returns>
    public ProjectState Load(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string path = GetProjectStatePath(projectRoot);

        if (!File.Exists(path))
        {
            return new ProjectState();
        }

        string json = File.ReadAllText(path);
        ProjectState? state = JsonSerializer.Deserialize<ProjectState>(json, JsonOptions.Default);
        return state ?? new ProjectState();
    }

    /// <summary>
    /// Saves project state.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="state">The project state.</param>
    public void Save(string projectRoot, ProjectState state)
    {
        Log.Ins.Debug("시작");
        string path = GetProjectStatePath(projectRoot);
        string json = JsonSerializer.Serialize(state, JsonOptions.Default);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Gets the project state file path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project state file path.</returns>
    private static string GetProjectStatePath(string projectRoot)
    {
        Log.Ins.Debug("시작");
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.ProjectStateFileName);
    }
}
