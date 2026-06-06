namespace LlmIde.Core.Projects;

/// <summary>
/// Provides persistence for the IDE-level project registry.
/// </summary>
public interface IProjectRegistryStore
{
    /// <summary>
    /// Loads the project registry.
    /// </summary>
    /// <returns>The project registry document.</returns>
    ProjectRegistryDocument Load();

    /// <summary>
    /// Saves the project registry.
    /// </summary>
    /// <param name="registry">The registry to save.</param>
    void Save(ProjectRegistryDocument registry);
}
