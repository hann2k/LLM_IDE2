namespace LlmIde.Core.Projects;

/// <summary>
/// Records project initialization progress.
/// </summary>
public sealed class ProjectInitProgress
{
    /// <summary>
    /// Gets or sets the latest initialization step.
    /// </summary>
    public string Step { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the initialization status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
