namespace LlmIde.Core.Projects;

/// <summary>
/// Stores all project registry entries.
/// </summary>
public sealed class ProjectRegistryDocument
{
    /// <summary>
    /// Gets or sets the registered projects.
    /// </summary>
    public List<ProjectRegistryEntry> Projects { get; set; } = [];
}
