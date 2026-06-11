using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using LlmIde.Wpf.Lib;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Renders the body markdown (text + images) read-only. Relative image paths in the body are
/// rewritten to absolute file URIs so the markdown renderer can load them.
/// </summary>
public sealed class BodyPreviewWindow : Window
{
    private BodyPreviewWindow(string bodyMarkdown, string projectRoot, Window owner)
    {
        Log.Ins.Debug("시작");
        Title = "본문 미리보기";
        Owner = owner;
        Width = 860;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Malgun Gothic");

        RichTextBox view = new RichTextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(12)
        };

        // Reuse the shared Markdown renderer (Markdig.Wpf) via the attached property.
        MarkdownText.SetText(view, RewriteImagePaths(bodyMarkdown, projectRoot));
        Content = view;
    }

    /// <summary>
    /// Opens the body preview window.
    /// </summary>
    /// <param name="bodyMarkdown">The body markdown text.</param>
    /// <param name="projectRoot">The project root path (base for relative image paths).</param>
    /// <param name="owner">The owner window.</param>
    public static void Show(string bodyMarkdown, string projectRoot, Window owner)
    {
        Log.Ins.Debug("시작");
        new BodyPreviewWindow(bodyMarkdown, projectRoot, owner).ShowDialog();
    }

    /// <summary>
    /// Rewrites relative markdown image paths to absolute file URIs (so the renderer can load them).
    /// Absolute, http(s) and file URIs are left untouched.
    /// </summary>
    /// <param name="markdown">The body markdown.</param>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The markdown with absolute image URIs.</returns>
    private static string RewriteImagePaths(string markdown, string projectRoot)
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
