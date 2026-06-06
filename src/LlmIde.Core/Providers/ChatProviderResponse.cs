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
}
