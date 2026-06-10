namespace LlmIde.Core.Agents;

/// <summary>
/// Result of the get_body tool (current section body).
/// </summary>
public sealed class GetBodyResult
{
    /// <summary>Gets or sets a value indicating whether the read succeeded.</summary>
    public bool Ok { get; set; }

    /// <summary>Gets or sets a value indicating whether a section is currently selected.</summary>
    public bool HasSelection { get; set; }

    /// <summary>Gets or sets the section title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the section body text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the error message when the read failed.</summary>
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Result of the propose_body_edit tool (a pending, user-approvable body revision).
/// </summary>
public sealed class ProposeBodyEditResult
{
    /// <summary>Gets or sets a value indicating whether the proposal was recorded.</summary>
    public bool Ok { get; set; }

    /// <summary>Gets or sets the length of the proposed body.</summary>
    public int Length { get; set; }

    /// <summary>Gets or sets the error message when the proposal failed.</summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
