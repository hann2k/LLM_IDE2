namespace LlmIde.Core.Projects;

/// <summary>
/// Stores the current state of a project outside the model session.
/// </summary>
public sealed class ProjectState
{
    /// <summary>
    /// Gets or sets the current project stage.
    /// </summary>
    public string Stage { get; set; } = "initialized";

    /// <summary>
    /// Gets or sets the current task summary.
    /// </summary>
    public string CurrentTask { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets completed work items.
    /// </summary>
    public List<string> CompletedItems { get; set; } = [];

    /// <summary>
    /// Gets or sets in-progress work items.
    /// </summary>
    public List<string> InProgressItems { get; set; } = [];

    /// <summary>
    /// Gets or sets next action items.
    /// </summary>
    public List<string> NextActions { get; set; } = [];

    /// <summary>
    /// Gets or sets current blockers.
    /// </summary>
    public List<string> Blockers { get; set; } = [];

    /// <summary>
    /// Gets or sets the last accepted decision.
    /// </summary>
    public string LastDecision { get; set; } = string.Empty;
}
