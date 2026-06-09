namespace LlmIde.Core.Agents;

/// <summary>
/// Describes the result of the read_file tool.
/// </summary>
public sealed class ReadFileResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the read succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the requested project-relative path (never an absolute path).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the text was truncated.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Gets or sets the optional error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
