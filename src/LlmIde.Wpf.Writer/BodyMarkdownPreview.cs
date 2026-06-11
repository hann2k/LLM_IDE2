using System;
using System.Text.RegularExpressions;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Helpers for rendering the body markdown in the in-place preview (DEC-084).
/// </summary>
public static class BodyMarkdownPreview
{
    /// <summary>
    /// Rewrites relative markdown image paths to absolute file URIs (so the renderer can load them).
    /// Absolute, http(s) and file URIs are left untouched.
    /// </summary>
    /// <param name="markdown">The body markdown.</param>
    /// <param name="projectRoot">The project root path (base for relative image paths).</param>
    /// <returns>The markdown with absolute image URIs.</returns>
    public static string RewriteImagePaths(string markdown, string projectRoot)
    {
        Log.Ins.Debug("시작");
        return Regex.Replace(
            markdown,
            @"(!\[[^\]]*\]\()([^)\s]+)(\))",
            match =>
            {
                string path = match.Groups[2].Value;

                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                    || System.IO.Path.IsPathRooted(path))
                {
                    return match.Value;
                }

                try
                {
                    string absolute = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, path));
                    return match.Groups[1].Value + new Uri(absolute).AbsoluteUri + match.Groups[3].Value;
                }
                catch (Exception)
                {
                    return match.Value;
                }
            });
    }
}
