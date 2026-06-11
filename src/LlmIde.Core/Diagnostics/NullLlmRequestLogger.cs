namespace LlmIde.Core.Diagnostics;

/// <summary>
/// An <see cref="ILlmRequestLogger"/> that discards all records (used by tests and headless contexts).
/// </summary>
public sealed class NullLlmRequestLogger : ILlmRequestLogger
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static NullLlmRequestLogger Instance { get; } = new NullLlmRequestLogger();

    /// <summary>
    /// Discards the record.
    /// </summary>
    /// <param name="record">The call record (ignored).</param>
    public void Log(LlmRequestLogRecord record)
    {
        Framework.Common.Logger.Log.Ins.Debug("시작");
    }
}
