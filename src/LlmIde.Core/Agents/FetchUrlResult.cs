namespace LlmIde.Core.Agents;

/// <summary>
/// Describes the result of the fetch_url tool.
/// </summary>
public sealed class FetchUrlResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the fetch succeeded.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the fetched URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the optional document title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the extracted text.
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
