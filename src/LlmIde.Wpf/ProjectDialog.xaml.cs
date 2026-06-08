using System.Windows;
using LlmIde.Core.Projects;
using Forms = System.Windows.Forms;

namespace LlmIde.Wpf;

/// <summary>
/// Identifies the operation a <see cref="ProjectDialog"/> performs.
/// </summary>
public enum ProjectDialogMode
{
    /// <summary>
    /// Creates a new project.
    /// </summary>
    Create,

    /// <summary>
    /// Renames an existing project display name.
    /// </summary>
    Rename,

    /// <summary>
    /// Deletes an existing project.
    /// </summary>
    Delete
}

/// <summary>
/// Provides the project create, rename, and delete dialog.
/// </summary>
public partial class ProjectDialog : Window
{
    /// <summary>
    /// The dialog operation mode.
    /// </summary>
    private readonly ProjectDialogMode mode;

    /// <summary>
    /// A value indicating whether the project being deleted has a stored API key.
    /// </summary>
    private readonly bool requiresApiKeyConfirmation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectDialog"/> class.
    /// </summary>
    /// <param name="mode">The dialog operation mode.</param>
    /// <param name="currentName">The current project display name for rename and delete modes.</param>
    /// <param name="requiresApiKeyConfirmation">Whether the deleted project has a stored API key.</param>
    public ProjectDialog(
        ProjectDialogMode mode,
        string currentName = "",
        bool requiresApiKeyConfirmation = false)
    {
        InitializeComponent();
        this.mode = mode;
        this.requiresApiKeyConfirmation = requiresApiKeyConfirmation;
        ApplyMode(currentName);
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
    /// Configures the dialog layout and labels for the active mode.
    /// </summary>
    /// <param name="currentName">The current project display name.</param>
    private void ApplyMode(string currentName)
    {
        if (mode == ProjectDialogMode.Create)
        {
            ApplyCreateMode();
            return;
        }

        if (mode == ProjectDialogMode.Rename)
        {
            ApplyRenameMode(currentName);
            return;
        }

        ApplyDeleteMode();
    }

    /// <summary>
    /// Configures the dialog for project creation.
    /// </summary>
    private void ApplyCreateMode()
    {
        Title = "프로젝트 생성";
        ConfirmButton.Content = "생성";
    }

    /// <summary>
    /// Configures the dialog for project rename.
    /// </summary>
    /// <param name="currentName">The current project display name.</param>
    private void ApplyRenameMode(string currentName)
    {
        Title = "프로젝트 이름변경";
        ConfirmButton.Content = "수정";

        // Rename shows the name field only.
        ProjectIdLabel.Visibility = Visibility.Collapsed;
        ProjectIdTextBox.Visibility = Visibility.Collapsed;
        ProjectPathLabel.Visibility = Visibility.Collapsed;
        ProjectPathRow.Visibility = Visibility.Collapsed;

        ProjectNameTextBox.Text = currentName;
        ProjectNameTextBox.SelectAll();
        ProjectNameTextBox.Focus();
    }

    /// <summary>
    /// Configures the dialog for project deletion.
    /// </summary>
    private void ApplyDeleteMode()
    {
        Title = "프로젝트 삭제";
        ConfirmButton.Content = "삭제";

        // Delete hides all input fields and shows the warning notice.
        ProjectIdLabel.Visibility = Visibility.Collapsed;
        ProjectIdTextBox.Visibility = Visibility.Collapsed;
        ProjectNameLabel.Visibility = Visibility.Collapsed;
        ProjectNameTextBox.Visibility = Visibility.Collapsed;
        ProjectPathLabel.Visibility = Visibility.Collapsed;
        ProjectPathRow.Visibility = Visibility.Collapsed;
        DeleteNoticeText.Visibility = Visibility.Visible;

        if (requiresApiKeyConfirmation)
        {
            DeleteApiKeyNoticeText.Visibility = Visibility.Visible;
            ConfirmApiKeyCheckBox.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Gets a value indicating whether API key deletion was confirmed.
    /// </summary>
    /// <returns>True when API key deletion is confirmed or not required.</returns>
    public bool IsApiKeyDeletionConfirmed()
    {
        if (!requiresApiKeyConfirmation)
        {
            return true;
        }

        return ConfirmApiKeyCheckBox.IsChecked == true;
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
    /// Accepts the dialog input.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    /// <summary>
    /// Cancels the dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
