using System.Windows;

namespace LlmIde.Wpf;

/// <summary>
/// Provides the project display name rename dialog.
/// </summary>
public partial class ProjectRenameDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectRenameDialog"/> class.
    /// </summary>
    /// <param name="currentProjectName">The current project display name.</param>
    public ProjectRenameDialog(string currentProjectName)
    {
        InitializeComponent();
        ProjectNameTextBox.Text = currentProjectName;
        ProjectNameTextBox.SelectAll();
        ProjectNameTextBox.Focus();
    }

    /// <summary>
    /// Gets the requested project display name.
    /// </summary>
    public string ProjectName
    {
        get
        {
            return ProjectNameTextBox.Text.Trim();
        }
    }

    /// <summary>
    /// Accepts the rename input.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    /// <summary>
    /// Cancels project rename.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
