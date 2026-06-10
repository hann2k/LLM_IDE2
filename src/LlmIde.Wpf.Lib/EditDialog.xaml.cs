using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace LlmIde.Wpf.Lib;

/// <summary>
/// Provides a single text editor dialog for project files. Markdown files additionally show a
/// live rendered preview next to the editor.
/// </summary>
public partial class EditDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditDialog"/> class.
    /// </summary>
    /// <param name="title">The window title showing the edited file.</param>
    /// <param name="content">The initial content to edit.</param>
    /// <param name="markdownPreview">Whether to show a live Markdown preview next to the editor.</param>
    public EditDialog(string title, string content, bool markdownPreview = false)
    {
        InitializeComponent();
        Title = title;
        ContentTextBox.Text = content;
        ContentTextBox.Focus();
        ContentTextBox.CaretIndex = ContentTextBox.Text.Length;

        if (markdownPreview)
        {
            EnableMarkdownPreview();
        }
        else
        {
            DisableMarkdownPreview();
        }
    }

    /// <summary>
    /// Gets the edited content.
    /// </summary>
    public string EditedContent
    {
        get
        {
            return ContentTextBox.Text;
        }
    }

    /// <summary>
    /// Enables the live Markdown preview pane and keeps it in sync with the editor.
    /// </summary>
    private void EnableMarkdownPreview()
    {
        // Open Markdown hyperlinks in the default browser instead of throwing on click.
        CommandBindings.Add(new CommandBinding(Markdig.Wpf.Commands.Hyperlink, OpenHyperlink));
        ContentTextBox.TextChanged += OnContentTextChanged;
        UpdatePreview();
    }

    /// <summary>
    /// Hides the preview pane so the editor uses the full width.
    /// </summary>
    private void DisableMarkdownPreview()
    {
        SplitterColumn.Width = new GridLength(0);
        PreviewColumn.Width = new GridLength(0);
        PreviewSplitter.Visibility = Visibility.Collapsed;
        PreviewContent.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Re-renders the Markdown preview from the current editor text.
    /// </summary>
    private void UpdatePreview()
    {
        PreviewContent.Markdown = ContentTextBox.Text;
    }

    /// <summary>
    /// Updates the preview whenever the editor text changes.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnContentTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    /// <summary>
    /// Opens a clicked Markdown hyperlink in the default browser.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The command event arguments carrying the link target.</param>
    private void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
    {
        string? url = e.Parameter as string;

        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>
    /// Accepts the edited content.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    /// <summary>
    /// Cancels the edit.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
