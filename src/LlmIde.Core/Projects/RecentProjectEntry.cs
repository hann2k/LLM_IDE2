namespace LlmIde.Core.Projects;

/// <summary>
/// Represents one project in the application-level recent project list.
/// </summary>
public sealed class RecentProjectEntry
{
    /// <summary>
    /// Gets or sets the project path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the project was last opened.
    /// </summary>
    public DateTimeOffset LastOpenedAt { get; set; }
}
