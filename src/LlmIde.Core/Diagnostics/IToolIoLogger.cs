namespace LlmIde.Core.Diagnostics;

/// <summary>
/// Records every tool execution's full input and output to a diagnostic log.
/// </summary>
public interface IToolIoLogger
{
    /// <summary>
    /// Logs a single tool execution. Implementations must never throw.
    /// </summary>
    /// <param name="record">The tool I/O record to log.</param>
    void Log(ToolIoLogRecord record);
}
