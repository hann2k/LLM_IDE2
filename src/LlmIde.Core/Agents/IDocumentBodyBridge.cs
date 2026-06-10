namespace LlmIde.Core.Agents;

/// <summary>
/// A snapshot of the currently selected document section (title + body) read from the editor.
/// </summary>
public sealed class DocumentBodySnapshot
{
    /// <summary>
    /// Gets or sets the section title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the section body text.
    /// </summary>
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Bridges agent tools to the writing editor's in-memory body content. The LLM never touches files for
/// body editing: it reads the current body on demand (<see cref="GetCurrentBody"/>) and proposes a full
/// revision (<see cref="ProposeBodyRevision"/>) that the user must approve before it is applied.
/// Implementations marshal to the UI thread as needed.
/// </summary>
public interface IDocumentBodyBridge
{
    /// <summary>
    /// Returns the currently selected section's title and body, or null when no section is selected.
    /// </summary>
    /// <returns>The body snapshot, or null.</returns>
    DocumentBodySnapshot? GetCurrentBody();

    /// <summary>
    /// Records a proposed full-body revision for the selected section. The revision is not applied until
    /// the user approves it; this only stores the proposal for the UI to present.
    /// </summary>
    /// <param name="revisedBody">The full revised body text.</param>
    void ProposeBodyRevision(string revisedBody);
}
