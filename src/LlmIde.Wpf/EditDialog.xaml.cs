using System.Windows;

namespace LlmIde.Wpf;

/// <summary>
/// Provides a single text editor dialog for project files.
/// </summary>
public partial class EditDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditDialog"/> class.
    /// </summary>
    /// <param name="title">The window title showing the edited file.</param>
    /// <param name="content">The initial content to edit.</param>
    public EditDialog(string title, string content)
    {
        InitializeComponent();
        Title = title;
        ContentTextBox.Text = content;
        ContentTextBox.Focus();
        ContentTextBox.CaretIndex = ContentTextBox.Text.Length;
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
