using System.Text.Json;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores the IDE-level project registry below the program root.
/// </summary>
public sealed class JsonProjectRegistryStore : IProjectRegistryStore
{
    /// <summary>
    /// The registry file path.
    /// </summary>
    private readonly string registryPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonProjectRegistryStore"/> class.
    /// </summary>
    /// <param name="ideProgramRoot">The IDE program root path.</param>
    public JsonProjectRegistryStore(string ideProgramRoot)
    {
        Log.Ins.Debug("시작");
        registryPath = Path.Combine(Path.GetFullPath(ideProgramRoot), "project", "projects.json");
    }

    /// <summary>
    /// Loads the project registry.
    /// </summary>
    /// <returns>The project registry document.</returns>
    public ProjectRegistryDocument Load()
    {
        Log.Ins.Debug("시작");
        if (!File.Exists(registryPath))
        {
            return new ProjectRegistryDocument();
        }

        string json = File.ReadAllText(registryPath);
        ProjectRegistryDocument? registry = JsonSerializer.Deserialize<ProjectRegistryDocument>(json, JsonOptions.Default);
        return registry ?? new ProjectRegistryDocument();
    }

    /// <summary>
    /// Saves the project registry.
    /// </summary>
    /// <param name="registry">The registry to save.</param>
    public void Save(ProjectRegistryDocument registry)
    {
        Log.Ins.Debug("시작");
        string? directory = Path.GetDirectoryName(registryPath);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(registry, JsonOptions.Default);
        File.WriteAllText(registryPath, json);
    }
}
