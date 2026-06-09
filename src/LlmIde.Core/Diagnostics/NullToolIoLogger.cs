namespace LlmIde.Core.Diagnostics;

/// <summary>
/// An <see cref="IToolIoLogger"/> that discards all records (used by tests and headless contexts).
/// </summary>
public sealed class NullToolIoLogger : IToolIoLogger
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static NullToolIoLogger Instance { get; } = new NullToolIoLogger();

    /// <summary>
    /// Discards the record.
    /// </summary>
    /// <param name="record">The tool I/O record (ignored).</param>
    public void Log(ToolIoLogRecord record)
    {
    }
}
