using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using LlmIde.Core.Providers;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Providers;

/// <summary>
/// Sends chat requests to the Anthropic Claude Messages API (DEC-086).
/// Same interface-adapter pattern as <see cref="DeepSeekChatProvider"/>: raw HTTP with an
/// injectable <see cref="HttpClient"/> so tests use a fake handler and never call the real API.
/// </summary>
public sealed class ClaudeChatProvider : IChatProvider, IModelProvider
{
    /// <summary>
    /// The default Claude messages endpoint (override via provider settings endpoint).
    /// </summary>
    private const string DefaultMessagesEndpoint = "https://api.anthropic.com/v1/messages";

    /// <summary>
    /// The Claude model list endpoint.
    /// </summary>
    private const string ModelsEndpoint = "https://api.anthropic.com/v1/models";

    /// <summary>
    /// The required anthropic-version header value.
    /// </summary>
    private const string ApiVersion = "2023-06-01";

    /// <summary>
    /// The max_tokens for non-streaming requests (kept moderate to stay under HTTP timeouts).
    /// </summary>
    private const int SendMaxTokens = 16000;

    /// <summary>
    /// The max_tokens for streaming requests.
    /// </summary>
    private const int StreamMaxTokens = 64000;

    /// <summary>
    /// The HTTP client.
    /// </summary>
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeChatProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    public ClaudeChatProvider(HttpClient httpClient)
    {
        Log.Ins.Debug("시작");
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Sends a chat request.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider response.</returns>
    public async Task<ChatProviderResponse> SendAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        EnsureApiKey(settings);
        string model = string.IsNullOrWhiteSpace(request.Model) ? settings.Model : request.Model;
        ClaudeRequest claudeRequest = BuildRequest(request, model, SendMaxTokens, stream: false);

        using HttpRequestMessage httpRequest = CreateHttpRequest(settings, claudeRequest);
        using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        string responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Claude API request failed: {(int)httpResponse.StatusCode} {responseBody}");
        }

        ClaudeResponse? claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseBody);
        // content는 블록 배열이다. text 블록만 이어 붙인다. (refusal 등은 빈 본문으로 귀결)
        string content = string.Concat(
            (claudeResponse?.Content ?? [])
                .Where(block => block.Type == "text")
                .Select(block => block.Text));

        return new ChatProviderResponse
        {
            Content = content,
            Provider = settings.Name,
            Model = model
        };
    }

    /// <summary>
    /// Streams a chat response.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The streamed response chunks.</returns>
    public async IAsyncEnumerable<string> StreamAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        EnsureApiKey(settings);
        string model = string.IsNullOrWhiteSpace(request.Model) ? settings.Model : request.Model;
        ClaudeRequest claudeRequest = BuildRequest(request, model, StreamMaxTokens, stream: true);

        using HttpRequestMessage httpRequest = CreateHttpRequest(settings, claudeRequest);
        using HttpResponseMessage httpResponse = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            string errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Claude API request failed: {(int)httpResponse.StatusCode} {errorBody}");
        }

        await using Stream stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                yield break;
            }

            // SSE 형식: "event: <이름>" 줄과 "data: {json}" 줄이 번갈아 온다. data만 파싱한다.
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            string data = line["data:".Length..].Trim();
            ClaudeStreamEvent? streamEvent = JsonSerializer.Deserialize<ClaudeStreamEvent>(data);

            if (streamEvent is null)
            {
                continue;
            }

            if (streamEvent.Type == "message_stop")
            {
                yield break;
            }

            // 본문 청크는 content_block_delta의 text_delta만이다. (thinking_delta 등은 표시하지 않음)
            if (streamEvent.Type == "content_block_delta" && streamEvent.Delta?.Type == "text_delta")
            {
                string? text = streamEvent.Delta.Text;

                if (!string.IsNullOrEmpty(text))
                {
                    yield return text;
                }
            }
        }
    }

    /// <summary>
    /// Lists models available from Anthropic.
    /// </summary>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available models.</returns>
    public async Task<IReadOnlyList<ProviderModel>> ListModelsAsync(
        ProviderSettings settings,
        CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        EnsureApiKey(settings);
        using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
        AddAuthHeaders(httpRequest, settings);

        using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        string responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Claude model list request failed: {(int)httpResponse.StatusCode} {responseBody}");
        }

        ClaudeModelsResponse? modelsResponse = JsonSerializer.Deserialize<ClaudeModelsResponse>(responseBody);

        return modelsResponse?.Data
            .Select(model => new ProviderModel
            {
                Id = model.Id
            })
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .ToList() ?? [];
    }

    /// <summary>
    /// Throws when the API key is missing.
    /// </summary>
    /// <param name="settings">The provider settings.</param>
    private static void EnsureApiKey(ProviderSettings settings)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Claude API key is empty. Set api_key in settings/providers.json (편집>LLM).");
        }
    }

    /// <summary>
    /// Creates the messages request with Anthropic auth headers and a JSON body.
    /// </summary>
    /// <param name="settings">The provider settings.</param>
    /// <param name="claudeRequest">The request payload.</param>
    /// <returns>The HTTP request.</returns>
    private static HttpRequestMessage CreateHttpRequest(ProviderSettings settings, ClaudeRequest claudeRequest)
    {
        Log.Ins.Debug("시작");
        string endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? DefaultMessagesEndpoint
            : settings.Endpoint;

        HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        AddAuthHeaders(httpRequest, settings);
        httpRequest.Content = JsonContent.Create(claudeRequest);
        return httpRequest;
    }

    /// <summary>
    /// Adds the Anthropic authentication headers (x-api-key + anthropic-version).
    /// </summary>
    /// <param name="httpRequest">The HTTP request.</param>
    /// <param name="settings">The provider settings.</param>
    private static void AddAuthHeaders(HttpRequestMessage httpRequest, ProviderSettings settings)
    {
        Log.Ins.Debug("시작");
        httpRequest.Headers.Add("x-api-key", settings.ApiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);
    }

    /// <summary>
    /// Builds the Claude request payload. Anthropic rejects the system role inside messages,
    /// so system messages are merged (in order) into the top-level system field.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="model">The resolved model.</param>
    /// <param name="maxTokens">The max_tokens value (required by Anthropic).</param>
    /// <param name="stream">Whether to stream the response.</param>
    /// <returns>The Claude request payload.</returns>
    private static ClaudeRequest BuildRequest(ChatProviderRequest request, string model, int maxTokens, bool stream)
    {
        Log.Ins.Debug("시작");
        List<string> systemParts = [];
        List<ClaudeMessage> messages = [];

        foreach (ChatMessage message in request.Messages)
        {
            if (string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
            {
                systemParts.Add(message.Content);
            }
            else
            {
                messages.Add(new ClaudeMessage
                {
                    Role = message.Role,
                    Content = message.Content
                });
            }
        }

        return new ClaudeRequest
        {
            Model = model,
            MaxTokens = maxTokens,
            System = systemParts.Count > 0 ? string.Join("\n\n", systemParts) : null,
            Messages = messages,
            Stream = stream
        };
    }

    /// <summary>
    /// Represents a Claude messages request payload.
    /// </summary>
    private sealed class ClaudeRequest
    {
        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum output tokens (required by Anthropic).
        /// </summary>
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        /// <summary>
        /// Gets or sets the merged system prompt (omitted when null).
        /// </summary>
        [JsonPropertyName("system")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? System { get; set; }

        /// <summary>
        /// Gets or sets the request messages (user/assistant only).
        /// </summary>
        [JsonPropertyName("messages")]
        public List<ClaudeMessage> Messages { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether streaming is enabled.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    /// <summary>
    /// Represents a Claude message payload.
    /// </summary>
    private sealed class ClaudeMessage
    {
        /// <summary>
        /// Gets or sets the message role.
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a Claude messages response payload.
    /// </summary>
    private sealed class ClaudeResponse
    {
        /// <summary>
        /// Gets or sets the response content blocks.
        /// </summary>
        [JsonPropertyName("content")]
        public List<ClaudeContentBlock> Content { get; set; } = [];

        /// <summary>
        /// Gets or sets the stop reason (e.g. end_turn, refusal).
        /// </summary>
        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
    }

    /// <summary>
    /// Represents one Claude response content block.
    /// </summary>
    private sealed class ClaudeContentBlock
    {
        /// <summary>
        /// Gets or sets the block type (text, thinking, ...).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the block text (text blocks only).
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents one Claude SSE stream event.
    /// </summary>
    private sealed class ClaudeStreamEvent
    {
        /// <summary>
        /// Gets or sets the event type (content_block_delta, message_stop, ...).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the delta payload (content_block_delta events only).
        /// </summary>
        [JsonPropertyName("delta")]
        public ClaudeStreamDelta? Delta { get; set; }
    }

    /// <summary>
    /// Represents the delta payload of a content_block_delta event.
    /// </summary>
    private sealed class ClaudeStreamDelta
    {
        /// <summary>
        /// Gets or sets the delta type (text_delta, thinking_delta, ...).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the delta text (text_delta only).
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    /// <summary>
    /// Represents a Claude model list response.
    /// </summary>
    private sealed class ClaudeModelsResponse
    {
        /// <summary>
        /// Gets or sets the returned model data.
        /// </summary>
        [JsonPropertyName("data")]
        public List<ClaudeModel> Data { get; set; } = [];
    }

    /// <summary>
    /// Represents a Claude model entry.
    /// </summary>
    private sealed class ClaudeModel
    {
        /// <summary>
        /// Gets or sets the model identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }
}
