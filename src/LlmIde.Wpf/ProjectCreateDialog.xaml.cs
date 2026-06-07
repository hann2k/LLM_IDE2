using System.Windows;
using LlmIde.Core.Projects;
using Forms = System.Windows.Forms;

namespace LlmIde.Wpf;

/// <summary>
/// Provides the project creation dialog.
/// </summary>
public partial class ProjectCreateDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectCreateDialog"/> class.
    /// </summary>
    public ProjectCreateDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates a project initialization request from dialog input.
    /// </summary>
    /// <returns>The project initialization request.</returns>
    public ProjectInitializationRequest CreateRequest()
    {
        string pId = ProjectIdTextBox.Text.Trim();
        string name = ProjectNameTextBox.Text.Trim();

        return new ProjectInitializationRequest
        {
            PId = pId,
            Name = name,
            Path = ProjectPathTextBox.Text.Trim(),
            HasExplicitPId = !string.IsNullOrWhiteSpace(pId),
            HasExplicitName = !string.IsNullOrWhiteSpace(name)
        };
    }

    /// <summary>
    /// Accepts the project creation input.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    /// <summary>
    /// Opens the Windows folder picker and fills the project path.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedPath = ProjectPathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            selectedPath = AppContext.BaseDirectory;
        }

        using Forms.FolderBrowserDialog dialog = new Forms.FolderBrowserDialog
        {
            Description = "프로젝트 폴더를 선택하세요.",
            SelectedPath = selectedPath,
            UseDescriptionForTitle = true
        };

        Forms.DialogResult result = dialog.ShowDialog();

        if (result == Forms.DialogResult.OK)
        {
            ProjectPathTextBox.Text = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// Cancels project creation.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
