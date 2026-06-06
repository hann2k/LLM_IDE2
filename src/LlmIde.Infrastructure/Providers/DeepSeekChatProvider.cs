using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LlmIde.Core.Providers;

namespace LlmIde.Infrastructure.Providers;

/// <summary>
/// Sends chat requests to the DeepSeek API.
/// </summary>
public sealed class DeepSeekChatProvider : IChatProvider
{
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
            }).ToList()
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
}
