namespace LlmIde.Core.Projects;

/// <summary>
/// Describes an initialized LLM IDE project.
/// </summary>
public sealed class ProjectInfo
{
    /// <summary>
    /// Gets or sets the stable project identifier.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the default provider name.
    /// </summary>
    public string DefaultProvider { get; set; } = "deepseek";

    /// <summary>
    /// Gets or sets the default model name.
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;
}
