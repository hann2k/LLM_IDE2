using System.Text;
using System.Text.Json;
using LlmIde.Core.Agents;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Reads a file from inside the project folder. Paths are resolved against the request's working
/// directory (the project root) and may not escape it: absolute paths, parent traversal ("..") and
/// the IDE system metadata folder (.llmide) are blocked.
/// </summary>
public sealed class ReadFileTool : IAgentTool
{
    /// <summary>
    /// The default maximum returned characters.
    /// </summary>
    private readonly int defaultMaxChars;

    /// <summary>
    /// The hard cap on characters read from a file.
    /// </summary>
    private readonly int maxReadChars;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadFileTool"/> class.
    /// </summary>
    /// <param name="defaultMaxChars">The default maximum returned characters.</param>
    /// <param name="maxReadChars">The hard cap on characters read from a file.</param>
    public ReadFileTool(int defaultMaxChars = 20000, int maxReadChars = 2_000_000)
    {
        Log.Ins.Debug("시작");
        this.defaultMaxChars = defaultMaxChars;
        this.maxReadChars = maxReadChars;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "read_file";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "프로젝트 폴더 기준 상대 경로의 파일을 읽어 텍스트로 반환한다. 절대경로, '..' 상위 이동, 시스템 폴더(.llmide) 접근은 차단된다.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{\"path\":\"필수(프로젝트 폴더 기준 상대경로)\",\"maxChars\":\"선택, 기본 20000\"}";

    /// <summary>
    /// Executes the read_file tool.
    /// </summary>
    /// <param name="request">The tool request (its WorkingDirectory is the project root).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public async Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        string path = ReadString(request.Arguments, "path");
        int maxChars = ReadInt(request.Arguments, "maxChars", defaultMaxChars);

        if (maxChars <= 0)
        {
            maxChars = defaultMaxChars;
        }

        (string? resolvedFullPath, string? validationError) = ResolveSafePath(request.WorkingDirectory, path);

        if (validationError is not null)
        {
            return Failure(request, path, validationError);
        }

        try
        {
            if (!File.Exists(resolvedFullPath))
            {
                return Failure(request, path, $"파일이 없습니다: {path}");
            }

            (string text, bool truncated) = await ReadCappedAsync(resolvedFullPath!, maxChars, cancellationToken);

            ReadFileResult result = new ReadFileResult
            {
                Ok = true,
                Path = path,
                Text = text,
                Truncated = truncated
            };

            return new AgentToolResult
            {
                Tool = Name,
                RequestId = request.RequestId,
                Ok = true,
                Result = result
            };
        }
        catch (Exception ex)
        {
            // A tool failure must not crash the app; report it as a failed result.
            return Failure(request, path, ex.Message);
        }
    }

    /// <summary>
    /// Resolves a project-relative path to a full path inside the project root, rejecting absolute
    /// paths and parent traversal. The boundary check is the authoritative guard.
    /// </summary>
    /// <param name="workingDirectory">The project root.</param>
    /// <param name="path">The requested path.</param>
    /// <returns>The resolved full path, or an error message when blocked.</returns>
    private static (string? FullPath, string? Error) ResolveSafePath(string workingDirectory, string path)
    {
        // read_file requires a concrete file path, so the project root itself is not a valid target.
        Log.Ins.Debug("시작");
        return ProjectPathGuard.Resolve(workingDirectory, path, allowRoot: false);
    }

    /// <summary>
    /// Reads a file as text up to a character cap.
    /// </summary>
    /// <param name="fullPath">The full file path.</param>
    /// <param name="maxChars">The maximum returned characters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The text and whether it was truncated.</returns>
    private async Task<(string Text, bool Truncated)> ReadCappedAsync(string fullPath, int maxChars, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        int cap = Math.Min(maxChars, maxReadChars);
        await using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new StreamReader(stream);
        StringBuilder builder = new StringBuilder();
        char[] buffer = new char[8192];
        bool truncated = false;

        while (true)
        {
            int read = await reader.ReadAsync(buffer, cancellationToken);

            if (read <= 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);

            // Once the content exceeds the cap there is more data than we return: trim and stop.
            if (builder.Length > cap)
            {
                builder.Length = cap;
                truncated = true;
                break;
            }
        }

        return (builder.ToString(), truncated);
    }

    /// <summary>
    /// Reads a string argument.
    /// </summary>
    /// <param name="arguments">The arguments element.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The string value, or empty.</returns>
    private static string ReadString(JsonElement arguments, string name)
    {
        Log.Ins.Debug("시작");
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads an integer argument.
    /// </summary>
    /// <param name="arguments">The arguments element.</param>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The fallback value.</param>
    /// <returns>The integer value.</returns>
    private static int ReadInt(JsonElement arguments, string name, int fallback)
    {
        Log.Ins.Debug("시작");
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int parsed))
        {
            return parsed;
        }

        return fallback;
    }

    /// <summary>
    /// Builds a failed read result.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="path">The requested path.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed tool result.</returns>
    private AgentToolResult Failure(AgentToolRequest request, string path, string errorMessage)
    {
        Log.Ins.Debug("시작");
        ReadFileResult result = new ReadFileResult
        {
            Ok = false,
            Path = path,
            Text = string.Empty,
            ErrorMessage = errorMessage
        };

        return new AgentToolResult
        {
            Tool = Name,
            RequestId = request.RequestId,
            Ok = false,
            Result = result,
            ErrorMessage = errorMessage
        };
    }
}
