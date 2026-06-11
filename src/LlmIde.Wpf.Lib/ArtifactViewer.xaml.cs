using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Lib;

/// <summary>
/// Provides a read-only artifact viewer. Code artifacts use a fixed-width code view; other
/// artifacts render as Markdown. Both offer copy and close actions.
/// </summary>
public partial class ArtifactViewer : Window
{
    /// <summary>
    /// The artifact content (raw text).
    /// </summary>
    private readonly string content;

    /// <summary>
    /// Gets a value indicating whether the user requested deletion of this artifact.
    /// </summary>
    public bool DeleteRequested { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactViewer"/> class.
    /// </summary>
    /// <param name="title">The artifact title shown in the window title.</param>
    /// <param name="content">The artifact content to display.</param>
    /// <param name="type">The artifact type (e.g., Code, Json, Markdown, Table).</param>
    /// <param name="targetPath">The optional target path; its extension helps detect code.</param>
    public ArtifactViewer(string title, string content, string type, string targetPath)
    {
        Log.Ins.Debug("시작");
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title) ? "아티팩트 뷰어" : title;
        this.content = content;

        if (IsCodeArtifact(type, targetPath))
        {
            // Fixed-width, no-wrap view so code keeps its lines and indentation.
            CodeContent.Text = content;
            CodeContent.Visibility = Visibility.Visible;
            MarkdownContent.Visibility = Visibility.Collapsed;
            return;
        }

        // Open Markdown hyperlinks in the default browser instead of throwing on click.
        CommandBindings.Add(new CommandBinding(Markdig.Wpf.Commands.Hyperlink, OpenHyperlink));
        MarkdownContent.Markdown = content;
        MarkdownContent.Visibility = Visibility.Visible;
        CodeContent.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Determines whether an artifact is code-like (shown in the code view rather than Markdown;
    /// also used by the Writer body drop to wrap insertions in a markdown code fence).
    /// </summary>
    /// <param name="type">The artifact type.</param>
    /// <param name="targetPath">The optional target path.</param>
    /// <returns>True when the artifact is code-like.</returns>
    public static bool IsCodeArtifact(string type, string targetPath)
    {
        Log.Ins.Debug("시작");
        string normalizedType = (type ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedType is "code" or "json" or "patch" or "command" or "config" or "log")
        {
            return true;
        }

        if (normalizedType is "markdown" or "md" or "table" or "note"
            or "plan" or "review" or "report" or "prompt")
        {
            return false;
        }

        // Unknown type: infer from the target path extension.
        string extension = Path.GetExtension(targetPath ?? string.Empty).ToLowerInvariant();
        return extension is ".cs" or ".json" or ".xml" or ".js" or ".ts" or ".py" or ".java"
            or ".cpp" or ".c" or ".h" or ".css" or ".html" or ".sql" or ".sh" or ".ps1"
            or ".yaml" or ".yml" or ".ini" or ".toml" or ".diff" or ".patch" or ".txt";
    }

    /// <summary>
    /// Copies the raw artifact content to the clipboard.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        System.Windows.Clipboard.SetText(content);
    }

    /// <summary>
    /// Requests deletion of the artifact after user confirmation, then closes the viewer.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        MessageBoxResult result = System.Windows.MessageBox.Show(
            this,
            "이 아티팩트를 삭제하시겠습니까? 파일도 함께 삭제됩니다.",
            "아티팩트 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        DeleteRequested = true;
        Close();
    }

    /// <summary>
    /// Closes the viewer.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        Close();
    }

    /// <summary>
    /// Opens a clicked Markdown hyperlink in the default browser.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The command event arguments carrying the link target.</param>
    private void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        string? url = e.Parameter as string;

        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
