using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LlmIde.Core.Agents;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Searches the web via a keyless search endpoint and parses the results.
/// </summary>
public sealed class WebSearchTool : IAgentTool
{
    /// <summary>
    /// The keyless DuckDuckGo HTML search endpoint.
    /// </summary>
    private const string SearchEndpoint = "https://html.duckduckgo.com/html/";

    /// <summary>
    /// The result anchor pattern (title and redirect href).
    /// </summary>
    private static readonly Regex ResultAnchorRegex = new Regex(
        @"<a[^>]*class=""[^""]*result__a[^""]*""[^>]*href=""(?<href>[^""]+)""[^>]*>(?<title>.*?)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// The result snippet pattern.
    /// </summary>
    private static readonly Regex SnippetRegex = new Regex(
        @"<a[^>]*class=""[^""]*result__snippet[^""]*""[^>]*>(?<snippet>.*?)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// The HTML tag removal pattern.
    /// </summary>
    private static readonly Regex TagRegex = new Regex("<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// The DuckDuckGo redirect target pattern.
    /// </summary>
    private static readonly Regex UddgRegex = new Regex(@"[?&]uddg=(?<u>[^&]+)", RegexOptions.Compiled);

    /// <summary>
    /// The HTTP client.
    /// </summary>
    private readonly HttpClient httpClient;

    /// <summary>
    /// The default maximum number of results.
    /// </summary>
    private readonly int defaultMaxResults;

    /// <summary>
    /// The maximum characters read from a search response.
    /// </summary>
    private readonly int maxResponseChars;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSearchTool"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="defaultMaxResults">The default maximum number of results.</param>
    /// <param name="maxResponseChars">The maximum characters read from a search response.</param>
    public WebSearchTool(HttpClient httpClient, int defaultMaxResults = 5, int maxResponseChars = 2_000_000)
    {
        this.httpClient = httpClient;
        this.defaultMaxResults = defaultMaxResults;
        this.maxResponseChars = maxResponseChars;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "web_search";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "검색어로 웹을 검색하고 제목/URL/요약 목록을 반환한다.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{\"query\":\"필수\",\"maxResults\":\"선택, 기본 5\"}";

    /// <summary>
    /// Executes the web_search tool.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public async Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        string query = ReadString(request.Arguments, "query");
        int maxResults = ReadInt(request.Arguments, "maxResults", defaultMaxResults);

        if (maxResults <= 0)
        {
            maxResults = defaultMaxResults;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Failure(request, query, "검색어가 비어 있습니다.");
        }

        if (query.Length > 512)
        {
            return Failure(request, query, "검색어가 너무 깁니다.");
        }

        try
        {
            // The DuckDuckGo HTML endpoint blocks GET (returns a 202 challenge page with no results);
            // only a form-urlencoded POST returns the actual result markup.
            using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, SearchEndpoint)
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("q", query)
                ])
            };

            // Some search frontends require a browser-like user agent.
            httpRequest.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (compatible; LlmIde/1.0)");

            using HttpResponseMessage httpResponse = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            string html = await ReadCappedBodyAsync(httpResponse, cancellationToken);
            List<WebSearchItem> items = ParseResults(html, maxResults);

            WebSearchResult result = new WebSearchResult
            {
                Ok = httpResponse.IsSuccessStatusCode,
                Query = query,
                Results = items
            };

            return new AgentToolResult
            {
                Tool = Name,
                RequestId = request.RequestId,
                Ok = result.Ok,
                Result = result
            };
        }
        catch (Exception ex)
        {
            return Failure(request, query, ex.Message);
        }
    }

    /// <summary>
    /// Parses search results from the search HTML.
    /// </summary>
    /// <param name="html">The search HTML.</param>
    /// <param name="maxResults">The maximum number of results.</param>
    /// <returns>The parsed results.</returns>
    private static List<WebSearchItem> ParseResults(string html, int maxResults)
    {
        List<WebSearchItem> items = [];
        MatchCollection anchors = ResultAnchorRegex.Matches(html);
        MatchCollection snippets = SnippetRegex.Matches(html);

        for (int index = 0; index < anchors.Count && items.Count < maxResults; index++)
        {
            Match anchor = anchors[index];
            string title = CleanText(anchor.Groups["title"].Value);
            string url = ResolveHref(anchor.Groups["href"].Value);
            string snippet = index < snippets.Count ? CleanText(snippets[index].Groups["snippet"].Value) : string.Empty;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            items.Add(new WebSearchItem
            {
                Title = title,
                Url = url,
                Snippet = snippet
            });
        }

        return items;
    }

    /// <summary>
    /// Resolves a DuckDuckGo redirect href to the real URL.
    /// </summary>
    /// <param name="href">The raw href.</param>
    /// <returns>The resolved URL.</returns>
    private static string ResolveHref(string href)
    {
        string decodedHref = WebUtility.HtmlDecode(href);
        Match uddg = UddgRegex.Match(decodedHref);

        if (uddg.Success)
        {
            return WebUtility.UrlDecode(uddg.Groups["u"].Value);
        }

        if (decodedHref.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + decodedHref;
        }

        return decodedHref;
    }

    /// <summary>
    /// Removes HTML tags and decodes entities from a fragment.
    /// </summary>
    /// <param name="value">The HTML fragment.</param>
    /// <returns>The cleaned text.</returns>
    private static string CleanText(string value)
    {
        string withoutTags = TagRegex.Replace(value, string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    /// <summary>
    /// Reads a response body up to the configured character cap.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The capped body.</returns>
    private async Task<string> ReadCappedBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new StreamReader(stream);
        StringBuilder builder = new StringBuilder();
        char[] buffer = new char[8192];

        while (true)
        {
            int read = await reader.ReadAsync(buffer, cancellationToken);

            if (read <= 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);

            if (builder.Length >= maxResponseChars)
            {
                break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads a string argument.
    /// </summary>
    /// <param name="arguments">The arguments element.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The string value, or empty.</returns>
    private static string ReadString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads an integer argument.
    /// </summary>
    /// <param name="arguments">The arguments element.</param>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The fallback value.</param>
    /// <returns>The integer value.</returns>
    private static int ReadInt(JsonElement arguments, string name, int fallback)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int parsed))
        {
            return parsed;
        }

        return fallback;
    }

    /// <summary>
    /// Builds a failed search result.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="query">The query.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed tool result.</returns>
    private AgentToolResult Failure(AgentToolRequest request, string query, string errorMessage)
    {
        WebSearchResult result = new WebSearchResult
        {
            Ok = false,
            Query = query,
            ErrorMessage = errorMessage
        };

        return new AgentToolResult
        {
            Tool = Name,
            RequestId = request.RequestId,
            Ok = false,
            Result = result,
            ErrorMessage = errorMessage
        };
    }
}
