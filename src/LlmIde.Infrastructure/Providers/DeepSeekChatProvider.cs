using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using LlmIde.Core.Providers;

namespace LlmIde.Infrastructure.Providers;

/// <summary>
/// Sends chat requests to the DeepSeek API.
/// </summary>
public sealed class DeepSeekChatProvider : IChatProvider, IModelProvider
{
    /// <summary>
    /// The DeepSeek model list endpoint.
    /// </summary>
    private const string ModelsEndpoint = "https://api.deepseek.com/v1/models";

    /// <summary>
    /// The HTTP client.
    /// </summary>
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepSeekChatProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    public DeepSeekChatProvider(HttpClient httpClient)
    {
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
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("DeepSeek API key is empty. Set api_key in .llmide/settings/providers.json.");
        }

        string endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? "https://api.deepseek.com/chat/completions"
            : settings.Endpoint;

        string model = string.IsNullOrWhiteSpace(request.Model) ? settings.Model : request.Model;
        DeepSeekRequest deepSeekRequest = new DeepSeekRequest
        {
            Model = model,
            Messages = request.Messages.Select(message => new DeepSeekMessage
            {
                Role = message.Role,
                Content = message.Content
            }).ToList(),
            Stream = false
        };

        using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = JsonContent.Create(deepSeekRequest);

        using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        string responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"DeepSeek API request failed: {(int)httpResponse.StatusCode} {responseBody}");
        }

        DeepSeekResponse? deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseBody);
        string content = deepSeekResponse?.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;

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
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("DeepSeek API key is empty. Set api_key in .llmide/settings/providers.json.");
        }

        string endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? "https://api.deepseek.com/chat/completions"
            : settings.Endpoint;

        string model = string.IsNullOrWhiteSpace(request.Model) ? settings.Model : request.Model;
        DeepSeekRequest deepSeekRequest = new DeepSeekRequest
        {
            Model = model,
            Messages = request.Messages.Select(message => new DeepSeekMessage
            {
                Role = message.Role,
                Content = message.Content
            }).ToList(),
            Stream = true
        };

        using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = JsonContent.Create(deepSeekRequest);

        using HttpResponseMessage httpResponse = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            string errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"DeepSeek API request failed: {(int)httpResponse.StatusCode} {errorBody}");
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

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            string data = line["data:".Length..].Trim();

            if (data == "[DONE]")
            {
                yield break;
            }

            DeepSeekStreamResponse? response = JsonSerializer.Deserialize<DeepSeekStreamResponse>(data);
            string? content = response?.Choices.FirstOrDefault()?.Delta.Content;

            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    /// <summary>
    /// Lists models available from DeepSeek.
    /// </summary>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available models.</returns>
    public async Task<IReadOnlyList<ProviderModel>> ListModelsAsync(
        ProviderSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("DeepSeek API key is empty. Set api_key in .llmide/settings/providers.json.");
        }

        using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        string responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"DeepSeek model list request failed: {(int)httpResponse.StatusCode} {responseBody}");
        }

        DeepSeekModelsResponse? modelsResponse = JsonSerializer.Deserialize<DeepSeekModelsResponse>(responseBody);

        return modelsResponse?.Data
            .Select(model => new ProviderModel
            {
                Id = model.Id
            })
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .ToList() ?? [];
    }

    /// <summary>
    /// Represents a DeepSeek request payload.
    /// </summary>
    private sealed class DeepSeekRequest
    {
        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request messages.
        /// </summary>
        [JsonPropertyName("messages")]
        public List<DeepSeekMessage> Messages { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether streaming is enabled.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    /// <summary>
    /// Represents a DeepSeek message payload.
    /// </summary>
    private sealed class DeepSeekMessage
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
    /// Represents a DeepSeek response payload.
    /// </summary>
    private sealed class DeepSeekResponse
    {
        /// <summary>
        /// Gets or sets the response choices.
        /// </summary>
        [JsonPropertyName("choices")]
        public List<DeepSeekChoice> Choices { get; set; } = [];
    }

    /// <summary>
    /// Represents one DeepSeek response choice.
    /// </summary>
    private sealed class DeepSeekChoice
    {
        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        [JsonPropertyName("message")]
        public DeepSeekMessage Message { get; set; } = new DeepSeekMessage();
    }

    /// <summary>
    /// Represents a streaming DeepSeek response payload.
    /// </summary>
    private sealed class DeepSeekStreamResponse
    {
        /// <summary>
        /// Gets or sets the streamed choices.
        /// </summary>
        [JsonPropertyName("choices")]
        public List<DeepSeekStreamChoice> Choices { get; set; } = [];
    }

    /// <summary>
    /// Represents one streaming DeepSeek response choice.
    /// </summary>
    private sealed class DeepSeekStreamChoice
    {
        /// <summary>
        /// Gets or sets the streamed delta.
        /// </summary>
        [JsonPropertyName("delta")]
        public DeepSeekMessage Delta { get; set; } = new DeepSeekMessage();
    }

    /// <summary>
    /// Represents a DeepSeek model list response.
    /// </summary>
    private sealed class DeepSeekModelsResponse
    {
        /// <summary>
        /// Gets or sets the returned model data.
        /// </summary>
        [JsonPropertyName("data")]
        public List<DeepSeekModel> Data { get; set; } = [];
    }

    /// <summary>
    /// Represents a DeepSeek model entry.
    /// </summary>
    private sealed class DeepSeekModel
    {
        /// <summary>
        /// Gets or sets the model identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }
}
