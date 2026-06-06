namespace LlmIde.Core.Conversations;

/// <summary>
/// Captures the context sent for one provider request.
/// </summary>
public sealed class ContextPackage
{
    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system rule.
    /// </summary>
    public string SystemRule { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets active criteria.
    /// </summary>
    public List<string> ActiveCriteria { get; set; } = [];

    /// <summary>
    /// Gets or sets the project state snapshot.
    /// </summary>
    public object ProjectState { get; set; } = new object();

    /// <summary>
    /// Gets or sets context notes.
    /// </summary>
    public List<string> ContextNotes { get; set; } = [];

    /// <summary>
    /// Gets or sets attached messages.
    /// </summary>
    public List<string> AttachedMessages { get; set; } = [];

    /// <summary>
    /// Gets or sets attached artifacts.
    /// </summary>
    public List<string> AttachedArtifacts { get; set; } = [];

    /// <summary>
    /// Gets or sets attached files.
    /// </summary>
    public List<string> AttachedFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets recent turns.
    /// </summary>
    public List<string> RecentTurns { get; set; } = [];

    /// <summary>
    /// Gets or sets the user request.
    /// </summary>
    public string UserRequest { get; set; } = string.Empty;
}
