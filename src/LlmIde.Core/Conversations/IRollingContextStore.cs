namespace LlmIde.Core.Conversations;

/// <summary>
/// Stores rolling context summaries.
/// </summary>
public interface IRollingContextStore
{
    /// <summary>
    /// Ensures rolling context files and directories exist.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    void EnsureInitialized(string projectRoot);

    /// <summary>
    /// Loads the current rolling context summary.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The current summary, or null when none exists.</returns>
    RollingContextSummary? LoadCurrent(string projectRoot);

    /// <summary>
    /// Loads the compression rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The compression rule.</returns>
    string LoadCompressionRule(string projectRoot);

    /// <summary>
    /// Saves a completed rolling context summary.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="sourceRequestId">The source chat request identifier.</param>
    /// <param name="previousRollingContextId">The previous rolling context identifier.</param>
    /// <param name="content">The summary content.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="model">The model name.</param>
    /// <returns>The saved summary.</returns>
    RollingContextSummary SaveCompleted(
        string projectRoot,
        string sourceRequestId,
        string? previousRollingContextId,
        string content,
        string provider,
        string model);

    /// <summary>
    /// Saves a failed rolling context summary attempt.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="sourceRequestId">The source chat request identifier.</param>
    /// <param name="previousRollingContextId">The previous rolling context identifier.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="model">The model name.</param>
    /// <param name="error">The error message.</param>
    /// <returns>The failed summary record.</returns>
    RollingContextSummary SaveFailed(
        string projectRoot,
        string sourceRequestId,
        string? previousRollingContextId,
        string provider,
        string model,
        string error);
}
