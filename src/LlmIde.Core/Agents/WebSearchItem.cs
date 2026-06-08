namespace LlmIde.Core.Agents;

/// <summary>
/// Describes one web search result item.
/// </summary>
public sealed class WebSearchItem
{
    /// <summary>
    /// Gets or sets the result title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the result URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the result snippet.
    /// </summary>
    public string Snippet { get; set; } = string.Empty;
}
