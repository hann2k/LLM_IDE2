namespace LlmIde.Core.Conversations;

/// <summary>
/// Represents an importance parsing result.
/// </summary>
public sealed class ConversationImportanceParseResult
{
    /// <summary>
    /// Gets or sets the response content without importance metadata.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parsed importance weight from 0 to 10.
    /// </summary>
    public int ImportanceWeight { get; set; }
}
