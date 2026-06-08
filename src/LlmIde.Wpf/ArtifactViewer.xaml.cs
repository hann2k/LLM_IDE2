using System.Windows;

namespace LlmIde.Wpf;

/// <summary>
/// Provides a read-only artifact viewer with copy and close actions.
/// </summary>
public partial class ArtifactViewer : Window
{
    /// <summary>
    /// The artifact content.
    /// </summary>
    private readonly string content;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactViewer"/> class.
    /// </summary>
    /// <param name="title">The artifact title shown in the window title.</param>
    /// <param name="content">The artifact content to display.</param>
    public ArtifactViewer(string title, string content)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title) ? "아티팩트 뷰어" : title;
        this.content = content;
        ContentTextBox.Text = content;
    }

    /// <summary>
    /// Copies the artifact content to the clipboard.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(content);
    }

    /// <summary>
    /// Closes the viewer.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
