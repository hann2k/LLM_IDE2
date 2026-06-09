using System.Text.Json;
using Framework.Common.Enum;
using LlmIde.Core.Diagnostics;
using LlmIde.Infrastructure.Json;

namespace LlmIde.Infrastructure.Diagnostics;

/// <summary>
/// Writes every actual LLM provider call (injected prompt and raw response) to the supervisor-supplied
/// Framework.Common file logger as one compact JSON line per call.
/// </summary>
/// <remarks>
/// Framework.Common.Logger.Log is a process-global singleton that writes to
/// <c>&lt;working directory&gt;/Log/SystemLog_&lt;yyyyMMdd&gt;.log</c>. The directory is fixed by the
/// library and is not redirected by <c>CheckCreateLogDir</c>; the call is kept only to ensure the
/// directory exists. Each record is serialized as a single line (newlines escaped) so one LLM call
/// occupies exactly one physical log line.
/// </remarks>
public sealed class FrameworkCommonLlmRequestLogger : ILlmRequestLogger
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
    /// Initializes a new instance of the <see cref="FrameworkCommonLlmRequestLogger"/> class.
    /// </summary>
    /// <param name="logDirectory">An optional directory to ensure exists (the library still writes to its fixed Log folder).</param>
    public FrameworkCommonLlmRequestLogger(string? logDirectory = null)
    {
        EnsureInitialized(logDirectory);
    }

    /// <summary>
    /// Logs a single LLM provider call as one compact JSON line.
    /// </summary>
    /// <param name="record">The call record to log.</param>
    public void Log(LlmRequestLogRecord record)
    {
        try
        {
            string json = JsonSerializer.Serialize(record, JsonOptions.Compact);

            if (string.IsNullOrEmpty(record.Error))
            {
                Framework.Common.Logger.Log.Ins.Info(json);
            }
            else
            {
                Framework.Common.Logger.Log.Ins.Error(json);
            }
        }
        catch
        {
            // Diagnostic logging must never disrupt the chat flow.
        }
    }

    /// <summary>
    /// Initializes the global Framework.Common logger once.
    /// </summary>
    /// <param name="logDirectory">An optional directory to ensure exists.</param>
    private static void EnsureInitialized(string? logDirectory)
    {
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
