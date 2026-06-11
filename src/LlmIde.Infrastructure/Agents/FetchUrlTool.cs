using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LlmIde.Core.Agents;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Fetches a URL and returns extracted text. URL access is performed by the IDE, not the model.
/// </summary>
public sealed class FetchUrlTool : IAgentTool
{
    /// <summary>
    /// The script element removal pattern.
    /// </summary>
    private static readonly Regex ScriptStyleRegex = new Regex(
        @"<(script|style)[^>]*>.*?</\1>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// The HTML tag removal pattern.
    /// </summary>
    private static readonly Regex TagRegex = new Regex(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// The HTML title extraction pattern.
    /// </summary>
    private static readonly Regex TitleRegex = new Regex(
        @"<title[^>]*>(?<title>.*?)</title>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// The whitespace collapse pattern.
    /// </summary>
    private static readonly Regex WhitespaceRegex = new Regex(
        @"\s+",
        RegexOptions.Compiled);

    /// <summary>
    /// The HTTP client.
    /// </summary>
    private readonly HttpClient httpClient;

    /// <summary>
    /// The default maximum extracted characters.
    /// </summary>
    private readonly int defaultMaxChars;

    /// <summary>
    /// The maximum characters read from a response body.
    /// </summary>
    private readonly int maxResponseChars;

    /// <summary>
    /// Initializes a new instance of the <see cref="FetchUrlTool"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client (configure redirect limit and timeout on it).</param>
    /// <param name="defaultMaxChars">The default maximum extracted characters.</param>
    /// <param name="maxResponseChars">The maximum characters read from a response body.</param>
    public FetchUrlTool(HttpClient httpClient, int defaultMaxChars = 12000, int maxResponseChars = 2_000_000)
    {
        Log.Ins.Debug("시작");
        this.httpClient = httpClient;
        this.defaultMaxChars = defaultMaxChars;
        this.maxResponseChars = maxResponseChars;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "fetch_url";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "주어진 URL을 가져와 본문 텍스트를 반환한다.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{\"url\":\"필수\",\"maxChars\":\"선택, 기본 12000\"}";

    /// <summary>
    /// Executes the fetch_url tool.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public async Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        string url = ReadString(request.Arguments, "url");
        int maxChars = ReadInt(request.Arguments, "maxChars", defaultMaxChars);

        if (maxChars <= 0)
        {
            maxChars = defaultMaxChars;
        }

        string? validationError = ValidateUrl(url);

        if (validationError is not null)
        {
            return Failure(request, url, validationError);
        }

        try
        {
            using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            string contentType = httpResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;
            string body = await ReadCappedBodyAsync(httpResponse, cancellationToken);
            (string text, string? title) = ExtractText(contentType, body);
            bool truncated = false;

            if (text.Length > maxChars)
            {
                text = text[..maxChars];
                truncated = true;
            }

            FetchUrlResult result = new FetchUrlResult
            {
                Ok = httpResponse.IsSuccessStatusCode,
                Url = url,
                ContentType = contentType,
                StatusCode = (int)httpResponse.StatusCode,
                Title = title,
                Text = text,
                Truncated = truncated
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
            // A tool failure must not crash the app; report it as a failed result.
            return Failure(request, url, ex.Message);
        }
    }

    /// <summary>
    /// Validates a URL against the allowed schemes and blocked address ranges.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>An error message when blocked, otherwise null.</returns>
    private static string? ValidateUrl(string url)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(url))
        {
            return "URL이 비어 있습니다.";
        }

        if (url.Length > 2048)
        {
            return "URL이 너무 깁니다.";
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return "유효한 절대 URL이 아닙니다.";
        }

        string scheme = uri.Scheme.ToLowerInvariant();

        if (scheme != "http" && scheme != "https")
        {
            return $"허용되지 않는 스킴입니다: {scheme}";
        }

        string host = uri.Host.Trim('[', ']').ToLowerInvariant();

        if (host.Length == 0)
        {
            return "호스트가 비어 있습니다.";
        }

        if (host == "localhost")
        {
            return "localhost 접근은 차단됩니다.";
        }

        if (IPAddress.TryParse(host, out IPAddress? ip) && IsBlockedIp(ip))
        {
            return "차단된 IP 대역입니다.";
        }

        return null;
    }

    /// <summary>
    /// Determines whether an IP address is in a blocked range.
    /// </summary>
    /// <param name="ip">The IP address.</param>
    /// <returns>True when the IP is blocked.</returns>
    private static bool IsBlockedIp(IPAddress ip)
    {
        Log.Ins.Debug("시작");
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        byte[] bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 0.0.0.0/8
            if (bytes[0] == 0)
            {
                return true;
            }

            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // fe80::/10 link-local
            if (ip.IsIPv6LinkLocal)
            {
                return true;
            }

            // fc00::/7 unique local
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// Reads a response body up to the configured character cap.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The capped body.</returns>
    private async Task<string> ReadCappedBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
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
    /// Extracts text and an optional title from a response body.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The extracted text and title.</returns>
    private static (string Text, string? Title) ExtractText(string contentType, string body)
    {
        Log.Ins.Debug("시작");
        if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            string? title = null;
            Match titleMatch = TitleRegex.Match(body);

            if (titleMatch.Success)
            {
                title = WebUtility.HtmlDecode(titleMatch.Groups["title"].Value).Trim();
            }

            string withoutScripts = ScriptStyleRegex.Replace(body, " ");
            string withoutTags = TagRegex.Replace(withoutScripts, " ");
            string decoded = WebUtility.HtmlDecode(withoutTags);
            string collapsed = WhitespaceRegex.Replace(decoded, " ").Trim();
            return (collapsed, title);
        }

        return (body.Trim(), null);
    }

    /// <summary>
    /// Reads a string argument.
    /// </summary>
    /// <param name="arguments">The arguments element.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The string value, or empty.</returns>
    private static string ReadString(JsonElement arguments, string name)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
    /// Builds a failed fetch result.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="url">The URL.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed tool result.</returns>
    private AgentToolResult Failure(AgentToolRequest request, string url, string errorMessage)
    {
        Log.Ins.Debug("시작");
        FetchUrlResult result = new FetchUrlResult
        {
            Ok = false,
            Url = url,
            Text = string.Empty,
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
