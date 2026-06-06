namespace LlmIde.Core.Conversations;

/// <summary>
/// Represents one rolling context summary snapshot.
/// </summary>
public sealed class RollingContextSummary
{
    /// <summary>
    /// Gets or sets the rolling context identifier.
    /// </summary>
    public string RollingContextId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source chat request identifier.
    /// </summary>
    public string SourceRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the previous rolling context identifier.
    /// </summary>
    public string? PreviousRollingContextId { get; set; }

    /// <summary>
    /// Gets or sets the summary content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative content path.
    /// </summary>
    public string ContentPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the summary status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the failure error.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the current summary.
    /// </summary>
    public bool IsCurrent { get; set; }
}
