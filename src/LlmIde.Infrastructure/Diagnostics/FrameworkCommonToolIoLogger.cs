using System.Text.Json;
using Framework.Common.Enum;
using LlmIde.Core.Diagnostics;
using LlmIde.Infrastructure.Json;

namespace LlmIde.Infrastructure.Diagnostics;

/// <summary>
/// Writes every tool execution's full input and output to the supervisor-supplied Framework.Common
/// file logger as one compact JSON line per call at the <c>[Debug]</c> level (alongside the LLM
/// request log, which uses <c>[Info]</c>, in the same SystemLog file).
/// </summary>
public sealed class FrameworkCommonToolIoLogger : IToolIoLogger
{
    /// <summary>
    /// Guards one-time logger initialization across threads.
    /// </summary>
    private static readonly object InitializationGate = new object();

    /// <summary>
    /// Indicates whether the global logger has been initialized.
    /// </summary>
    private static bool initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameworkCommonToolIoLogger"/> class.
    /// </summary>
    /// <param name="logDirectory">An optional directory to ensure exists (the library still writes to its fixed Log folder).</param>
    public FrameworkCommonToolIoLogger(string? logDirectory = null)
    {
        Framework.Common.Logger.Log.Ins.Debug("시작");
        EnsureInitialized(logDirectory);
    }

    /// <summary>
    /// Logs a single tool execution as one compact JSON line at the Debug level.
    /// </summary>
    /// <param name="record">The tool I/O record to log.</param>
    public void Log(ToolIoLogRecord record)
    {
        Framework.Common.Logger.Log.Ins.Debug("시작");
        try
        {
            Framework.Common.Logger.Log.Ins.Debug(JsonSerializer.Serialize(record, JsonOptions.Compact));
        }
        catch
        {
            // Diagnostic logging must never disrupt the chat flow.
        }
    }

    /// <summary>
    /// Initializes the global Framework.Common logger once (Debug level so tool I/O is emitted).
    /// </summary>
    /// <param name="logDirectory">An optional directory to ensure exists.</param>
    private static void EnsureInitialized(string? logDirectory)
    {
        Framework.Common.Logger.Log.Ins.Debug("시작");
        if (initialized)
        {
            return;
        }

        lock (InitializationGate)
        {
            if (initialized)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    Framework.Common.Logger.Log.Ins.CheckCreateLogDir(logDirectory);
                }

                Framework.Common.Logger.Log.Ins.SetLogLevel(LogType.Debug);
            }
            catch
            {
                // Ignore initialization failures; logging is best-effort.
            }

            initialized = true;
        }
    }
}
