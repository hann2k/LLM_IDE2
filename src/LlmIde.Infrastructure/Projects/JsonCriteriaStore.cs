using System.Text.Json;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores criteria in a project's criteria.json file.
/// </summary>
public sealed class JsonCriteriaStore : ICriteriaStore
{
    /// <summary>
    /// Loads criteria for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project criteria.</returns>
    public IReadOnlyList<Criterion> Load(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string path = GetCriteriaPath(projectRoot);

        if (!File.Exists(path))
        {
            return [];
        }

        string json = File.ReadAllText(path);
        List<Criterion>? criteria = JsonSerializer.Deserialize<List<Criterion>>(json, JsonOptions.Default);
        return criteria ?? [];
    }

    /// <summary>
    /// Saves criteria for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="criteria">The project criteria.</param>
    public void Save(string projectRoot, IReadOnlyList<Criterion> criteria)
    {
        Log.Ins.Debug("시작");
        string path = GetCriteriaPath(projectRoot);
        string json = JsonSerializer.Serialize(criteria, JsonOptions.Default);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Gets the criteria file path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The criteria file path.</returns>
    private static string GetCriteriaPath(string projectRoot)
    {
        Log.Ins.Debug("시작");
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.CriteriaFileName);
    }
}
