using LlmIde.Core.Projects;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Resolves project-relative paths for file tools and enforces the access boundary: absolute paths,
/// parent traversal ("..") , escapes outside the project folder, and the IDE system metadata folder
/// (.llmide) are all blocked. This is the single source of truth for file-tool path safety.
/// </summary>
internal static class ProjectPathGuard
{
    /// <summary>
    /// Resolves a project-relative path to a full path inside the project root.
    /// </summary>
    /// <param name="workingDirectory">The project root.</param>
    /// <param name="path">The requested project-relative path.</param>
    /// <param name="allowRoot">When true, an empty or "." path resolves to the project root itself.</param>
    /// <returns>The resolved full path, or an error message when blocked.</returns>
    public static (string? FullPath, string? Error) Resolve(string workingDirectory, string path, bool allowRoot)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return (null, "작업 폴더가 설정되지 않았습니다.");
        }

        string requested = path ?? string.Empty;

        if (string.IsNullOrWhiteSpace(requested) || requested == ".")
        {
            return allowRoot
                ? (Path.GetFullPath(workingDirectory), null)
                : (null, "경로가 비어 있습니다.");
        }

        if (requested.Length > 1024)
        {
            return (null, "경로가 너무 깁니다.");
        }

        // Block absolute paths (e.g. /etc/passwd, C:\Windows, \\server\share).
        if (Path.IsPathRooted(requested))
        {
            return (null, "보안 위반: 절대경로 접근은 차단됩니다.");
        }

        // Block parent traversal segments anywhere in the path.
        foreach (string segment in requested.Split('/', '\\'))
        {
            if (segment == "..")
            {
                return (null, "보안 위반: '..' 상위 폴더 접근은 차단됩니다.");
            }
        }

        string root = Path.GetFullPath(workingDirectory);
        string full = Path.GetFullPath(Path.Combine(root, requested));
        string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        // Authoritative guard: the resolved path must stay inside the project root.
        bool insideRoot = full.Equals(root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);

        if (!insideRoot)
        {
            return (null, "보안 위반: 프로젝트 폴더 밖 접근은 차단됩니다.");
        }

        if (IsMetadata(root, full))
        {
            return (null, "보안 위반: 시스템 폴더(.llmide) 접근은 차단됩니다.");
        }

        return (full, null);
    }

    /// <summary>
    /// Determines whether a full path is the IDE system metadata folder (.llmide) or anything under it.
    /// </summary>
    /// <param name="root">The project root full path.</param>
    /// <param name="fullPath">The full path to test.</param>
    /// <returns>True when the path is inside .llmide.</returns>
    public static bool IsMetadata(string root, string fullPath)
    {
        Log.Ins.Debug("시작");
        string metadataRoot = Path.Combine(root, LlmIdeLayout.MetadataDirectoryName);
        string metadataWithSeparator = metadataRoot + Path.DirectorySeparatorChar;
        return fullPath.Equals(metadataRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(metadataWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
