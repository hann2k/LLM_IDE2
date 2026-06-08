namespace LlmIde.Core.Agents;

/// <summary>
/// Describes the result of the web_search tool.
/// </summary>
public sealed class WebSearchResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the search succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the search result items.
    /// </summary>
    public List<WebSearchItem> Results { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
