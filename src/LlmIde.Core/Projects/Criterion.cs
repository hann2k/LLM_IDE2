namespace LlmIde.Core.Projects;

/// <summary>
/// Represents one persistent project criterion.
/// </summary>
public sealed class Criterion
{
    /// <summary>
    /// Gets or sets the criterion identifier.
    /// </summary>
    public string CriterionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the criterion title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the criterion description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the criterion status.
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Gets or sets the criterion priority.
    /// </summary>
    public string Priority { get; set; } = "normal";
}
