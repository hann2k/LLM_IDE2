namespace LlmIde.Core.Agents;

/// <summary>
/// Describes the result of the list_files tool.
/// </summary>
public sealed class ListFilesResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the listing succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the requested project-relative directory (empty for the project root).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the listed entries (project-relative paths).
    /// </summary>
    public IReadOnlyList<ListFilesEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the listing was truncated at the entry cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Gets or sets the optional error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Describes one entry (file or directory) returned by the list_files tool.
/// </summary>
public sealed class ListFilesEntry
{
    /// <summary>
    /// Gets or sets the project-relative path (forward slashes).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the entry is a directory.
    /// </summary>
    public bool IsDirectory { get; set; }
}
