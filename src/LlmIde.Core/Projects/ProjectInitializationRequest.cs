namespace LlmIde.Core.Projects;

/// <summary>
/// Describes a project initialization request.
/// </summary>
public sealed class ProjectInitializationRequest
{
    /// <summary>
    /// Gets or sets the English project identifier.
    /// </summary>
    public string PId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-visible project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional project root path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the user explicitly supplied a project identifier.
    /// </summary>
    public bool HasExplicitPId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user explicitly supplied a display name.
    /// </summary>
    public bool HasExplicitName { get; set; }
}
