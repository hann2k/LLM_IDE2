using System.Text.Json;
using LlmIde.Core.Agents;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Lists folders and files under a project-relative directory. Paths are resolved against the request's
/// working directory (the project root) and may not escape it: absolute paths, parent traversal ("..")
/// and the IDE system metadata folder (.llmide) are blocked, and .llmide is never listed or descended into.
/// </summary>
public sealed class ListFilesTool : IAgentTool
{
    /// <summary>
    /// The default maximum number of returned entries.
    /// </summary>
    private readonly int defaultMaxEntries;

    /// <summary>
    /// The hard cap on returned entries.
    /// </summary>
    private readonly int maxEntriesCap;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListFilesTool"/> class.
    /// </summary>
    /// <param name="defaultMaxEntries">The default maximum number of returned entries.</param>
    /// <param name="maxEntriesCap">The hard cap on returned entries.</param>
    public ListFilesTool(int defaultMaxEntries = 1000, int maxEntriesCap = 5000)
    {
        Log.Ins.Debug("시작");
        this.defaultMaxEntries = defaultMaxEntries;
        this.maxEntriesCap = maxEntriesCap;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "list_files";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "프로젝트 폴더 기준 상대 경로의 폴더/파일 목록을 반환한다. 절대경로, '..' 상위 이동, 시스템 폴더(.llmide) 접근은 차단되며 .llmide는 목록에서 제외된다.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{\"path\":\"선택(기본 프로젝트 루트)\",\"recursive\":\"선택, 기본 false\",\"maxEntries\":\"선택, 기본 1000\"}";

    /// <summary>
    /// Executes the list_files tool.
    /// </summary>
    /// <param name="request">The tool request (its WorkingDirectory is the project root).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        string path = ReadString(request.Arguments, "path");
        bool recursive = ReadBool(request.Arguments, "recursive", false);
        int maxEntries = ReadInt(request.Arguments, "maxEntries", defaultMaxEntries);

        if (maxEntries <= 0)
        {
            maxEntries = defaultMaxEntries;
        }

        maxEntries = Math.Min(maxEntries, maxEntriesCap);

        (string? resolvedFullPath, string? validationError) = ProjectPathGuard.Resolve(request.WorkingDirectory, path, allowRoot: true);

        if (validationError is not null)
        {
            return Task.FromResult(Failure(request, path, validationError));
        }

        try
        {
            if (!Directory.Exists(resolvedFullPath))
            {
                return Task.FromResult(Failure(request, path, $"디렉터리가 없습니다: {path}"));
            }

            string root = Path.GetFullPath(request.WorkingDirectory);
            (List<ListFilesEntry> entries, bool truncated) = Enumerate(root, resolvedFullPath!, recursive, maxEntries, cancellationToken);

            ListFilesResult result = new ListFilesResult
            {
                Ok = true,
                Path = path,
                Entries = entries,
                Truncated = truncated
            };

            return Task.FromResult(new AgentToolResult
            {
                Tool = Name,
                RequestId = request.RequestId,
                Ok = true,
                Result = result
            });
        }
        catch (Exception ex)
        {
            // A tool failure must not crash the app; report it as a failed result.
            return Task.FromResult(Failure(request, path, ex.Message));
        }
    }

    /// <summary>
    /// Enumerates directory entries, skipping the .llmide system folder, up to the entry cap.
    /// </summary>
    /// <param name="root">The project root full path.</param>
    /// <param name="startDirectory">The directory to list.</param>
    /// <param name="recursive">Whether to descend into subdirectories.</param>
    /// <param name="maxEntries">The maximum number of entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries and whether the listing was truncated.</returns>
    private static (List<ListFilesEntry> Entries, bool Truncated) Enumerate(
        string root,
        string startDirectory,
        bool recursive,
        int maxEntries,
        CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        List<ListFilesEntry> entries = [];
        bool truncated = false;
        Queue<string> directories = new Queue<string>();
        directories.Enqueue(startDirectory);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = directories.Dequeue();
            string[] children;

            try
            {
                children = Directory.GetFileSystemEntries(directory);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            Array.Sort(children, StringComparer.OrdinalIgnoreCase);

            foreach (string child in children)
            {
                string fullChild = Path.GetFullPath(child);

                // Never list or descend into the IDE system metadata folder.
                if (ProjectPathGuard.IsMetadata(root, fullChild))
                {
                    continue;
                }

                bool isDirectory = Directory.Exists(fullChild);
                entries.Add(new ListFilesEntry
                {
                    Path = Path.GetRelativePath(root, fullChild).Replace('\\', '/'),
                    Name = Path.GetFileName(fullChild),
                    IsDirectory = isDirectory
                });

                if (entries.Count >= maxEntries)
                {
                    return (entries, true);
                }

                if (recursive && isDirectory)
                {
                    directories.Enqueue(fullChild);
                }
            }
        }

        return (entries, truncated);
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
    /// Reads a boolean argument.
    /// </summary>
    /// <param name="arguments">The arguments element.</param>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The fallback value.</param>
    /// <returns>The boolean value.</returns>
    private static bool ReadBool(JsonElement arguments, string name, bool fallback)
    {
        Log.Ins.Debug("시작");
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out JsonElement value))
        {
            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return fallback;
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
    /// Builds a failed listing result.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="path">The requested path.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed tool result.</returns>
    private AgentToolResult Failure(AgentToolRequest request, string path, string errorMessage)
    {
        Log.Ins.Debug("시작");
        ListFilesResult result = new ListFilesResult
        {
            Ok = false,
            Path = path,
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
