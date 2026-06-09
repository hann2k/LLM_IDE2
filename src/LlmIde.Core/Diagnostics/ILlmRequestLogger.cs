namespace LlmIde.Core.Diagnostics;

/// <summary>
/// Records every actual LLM provider call (injected prompt and raw response) to a diagnostic log.
/// </summary>
public interface ILlmRequestLogger
{
    /// <summary>
    /// Logs a single LLM provider call. Implementations must never throw.
    /// </summary>
    /// <param name="record">The call record to log.</param>
    void Log(LlmRequestLogRecord record);
}
