namespace LlmIde.Core.Providers;

/// <summary>
/// Describes a provider chat response.
/// </summary>
public sealed class ChatProviderResponse
{
    /// <summary>
    /// Gets or sets the response content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request that was sent to the provider.
    /// </summary>
    public ChatProviderRequest? SentRequest { get; set; }

    /// <summary>
    /// Gets or sets the stored request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression request identifier.
    /// </summary>
    public string CompressionRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression request sent after chat.
    /// </summary>
    public ChatProviderRequest? CompressionRequest { get; set; }

    /// <summary>
    /// Gets or sets the compression response content.
    /// </summary>
    public string CompressionContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression status.
    /// </summary>
    public string CompressionStatus { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression error.
    /// </summary>
    public string CompressionError { get; set; } = string.Empty;
}
