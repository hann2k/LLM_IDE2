using System.IO;
using System.Linq;
using System.Windows;
using LlmIde.Core.Agents;
using LlmIde.Core.Projects;
using LlmIde.Core.Providers;

namespace LlmIde.Wpf.Lib;

/// <summary>
/// A lightweight reference to the project currently selected in a workspace window.
/// Abstracts away the app-specific project list item type so shared menu handlers can
/// live in <see cref="WorkspaceWindowBase"/>.
/// </summary>
/// <param name="Path">The project root path.</param>
/// <param name="DisplayName">The project display name.</param>
/// <param name="PId">The project identifier.</param>
public readonly record struct WorkspaceProject(string Path, string DisplayName, string PId);

/// <summary>
/// Shared base window for the workspace applications (Chat and Writer). It hosts the
/// menu handlers that are identical across apps — project create/clone/rename/delete,
/// the common LLM provider manager, the read-only tool list and project criteria editing.
/// The menu XAML stays per-app (so each app's GUI can differ) and simply binds its
/// <c>Click</c> events to these inherited handlers. App-specific concerns are exposed
/// through the abstract members below.
/// </summary>
public abstract class WorkspaceWindowBase : Window
{
    /// <summary>Gets the project registry service.</summary>
    protected abstract ProjectRegistryService ProjectRegistry { get; }

    /// <summary>Gets the project initializer.</summary>
    protected abstract ProjectInitializer Initializer { get; }

    /// <summary>Gets the project store.</summary>
    protected abstract IProjectStore ProjectStore { get; }

    /// <summary>Gets the common provider settings store.</summary>
    protected abstract IProviderSettingsStore ProviderSettingsStore { get; }

    /// <summary>Gets the agent tool host.</summary>
    protected abstract IAgentToolHost AgentToolHost { get; }

    /// <summary>Gets the project criteria service.</summary>
    protected abstract CriteriaService Criteria { get; }

    /// <summary>Gets the currently selected project, or <c>null</c> when none is selected.</summary>
    protected abstract WorkspaceProject? CurrentProject { get; }

    /// <summary>Reloads the project list from the registry.</summary>
    protected abstract void LoadProjects();

    /// <summary>Selects a project by its root path.</summary>
    /// <param name="projectRoot">The project root path.</param>
    protected abstract void SelectProjectByRoot(string projectRoot);

    /// <summary>Selects a project by its identifier.</summary>
    /// <param name="pId">The project identifier.</param>
    protected abstract void SelectProjectByPId(string pId);

    /// <summary>
    /// Releases file locks (e.g. SQLite connection pools) so a project folder can be
    /// copied or deleted. Apps override this to clear their database pools.
    /// </summary>
    protected virtual void ReleaseProjectFileLocks()
    {
    }

    /// <summary>
    /// Called after the provider list may have changed (e.g. via the LLM manager).
    /// Apps that show providers in their UI override this to refresh.
    /// </summary>
    protected virtual void OnProvidersChanged()
    {
    }

    /// <summary>
    /// Opens the project creation dialog and creates a project when accepted.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected void CreateProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ProjectDialog dialog = new ProjectDialog(ProjectDialogMode.Create)
        {
            Owner = this
        };

        bool? accepted = dialog.ShowDialog();

        if (accepted != true)
        {
            return;
        }

        try
        {
            // pID is auto-generated; the chosen path is the start location and the folder name uses the pID.
            string pId = ProjectRegistry.NextProjectPId();
            string startLocation = dialog.ProjectStartLocation;
            string projectPath = string.IsNullOrWhiteSpace(startLocation)
                ? string.Empty
                : System.IO.Path.Combine(startLocation, pId);
            ProjectInitializationRequest request = new ProjectInitializationRequest
            {
                PId = pId,
                Name = dialog.ProjectName,
                Path = projectPath,
                HasExplicitPId = true,
                HasExplicitName = !string.IsNullOrWhiteSpace(dialog.ProjectName)
            };
            ProjectInitializationResult result = Initializer.Initialize(request);

            // Store the conversation type selected during project creation.
            ProjectInfo projectInfo = result.ProjectInfo;
            projectInfo.LongTermConversation = dialog.IsLongTermConversation;
            ProjectStore.SaveProjectInfo(result.ProjectRoot, projectInfo);

            // API keys are managed centrally via 편집 > LLM (common providers), not per project.
            LoadProjects();
            SelectProjectByRoot(result.ProjectRoot);
            System.Windows.MessageBox.Show(this, "프로젝트를 생성했습니다.", "프로젝트 생성", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "프로젝트 생성 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Clones the selected project into a new project with an incremented pID.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected void CloneProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentProject is not { } source)
        {
            return;
        }

        try
        {
            string sourceRoot = System.IO.Path.GetFullPath(source.Path);
            string startLocation = System.IO.Path.GetDirectoryName(sourceRoot) ?? AppContext.BaseDirectory;
            string newPId = ProjectRegistry.NextProjectPId();
            string destinationRoot = System.IO.Path.Combine(startLocation, newPId);

            // Release file locks so the source database file can be copied.
            ReleaseProjectFileLocks();
            CopyDirectory(sourceRoot, destinationRoot);
            ProjectRegistry.Add(new ProjectRegistryEntry
            {
                PId = newPId,
                Name = source.DisplayName,
                Path = destinationRoot,
                CreatedAt = DateTimeOffset.UtcNow
            });
            LoadProjects();
            SelectProjectByPId(newPId);
            System.Windows.MessageBox.Show(this, "프로젝트를 복제했습니다.", "프로젝트 복제", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "프로젝트 복제 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Recursively copies a directory and all of its contents.
    /// </summary>
    /// <param name="sourceDirectory">The source directory.</param>
    /// <param name="destinationDirectory">The destination directory.</param>
    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string filePath in Directory.GetFiles(sourceDirectory))
        {
            string fileName = System.IO.Path.GetFileName(filePath);
            File.Copy(filePath, System.IO.Path.Combine(destinationDirectory, fileName), true);
        }

        foreach (string directoryPath in Directory.GetDirectories(sourceDirectory))
        {
            string directoryName = System.IO.Path.GetFileName(directoryPath);
            CopyDirectory(directoryPath, System.IO.Path.Combine(destinationDirectory, directoryName));
        }
    }

    /// <summary>
    /// Opens the project rename dialog and renames the selected project when accepted.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected void RenameProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentProject is not { } selectedProject)
        {
            return;
        }

        ProjectDialog dialog = new ProjectDialog(ProjectDialogMode.Rename, selectedProject.DisplayName)
        {
            Owner = this
        };

        bool? accepted = dialog.ShowDialog();

        if (accepted != true)
        {
            return;
        }

        try
        {
            ProjectRegistry.Rename(selectedProject.PId, dialog.ProjectName);
            LoadProjects();
            SelectProjectByPId(selectedProject.PId);
            System.Windows.MessageBox.Show(this, "프로젝트 이름을 변경했습니다.", "프로젝트 이름변경", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "프로젝트 이름변경 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the project delete dialog and deletes the selected project when confirmed.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected void DeleteProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentProject is not { } selectedProject)
        {
            return;
        }

        bool hasApiKey = HasApiKey(selectedProject.Path);
        ProjectDialog dialog = new ProjectDialog(ProjectDialogMode.Delete, selectedProject.DisplayName, hasApiKey)
        {
            Owner = this
        };

        bool? accepted = dialog.ShowDialog();

        // Delete only when the confirm button was pressed and the API key checkbox is satisfied.
        if (accepted != true || !dialog.IsApiKeyDeletionConfirmed())
        {
            System.Windows.MessageBox.Show(this, "프로젝트 삭제가 취소되었습니다.", "프로젝트 삭제", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            DeleteProject(selectedProject);
            LoadProjects();
            System.Windows.MessageBox.Show(this, "프로젝트를 삭제했습니다.", "프로젝트 삭제", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "프로젝트 삭제 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Deletes a project folder and removes the registry entry.
    /// </summary>
    /// <param name="project">The project to delete.</param>
    private void DeleteProject(WorkspaceProject project)
    {
        string projectRoot = System.IO.Path.GetFullPath(project.Path);

        if (Directory.Exists(projectRoot))
        {
            // Release file locks so the conversation database file can be removed.
            ReleaseProjectFileLocks();
            Directory.Delete(projectRoot, true);
        }

        ProjectRegistry.Remove(project.PId);
    }

    /// <summary>
    /// Determines whether a project has any stored provider API key.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>True when at least one API key exists.</returns>
    private bool HasApiKey(string projectRoot)
    {
        ProviderSettingsDocument settings = ProviderSettingsStore.Load(projectRoot);
        return settings.Providers.Any(provider => !string.IsNullOrWhiteSpace(provider.ApiKey));
    }

    /// <summary>
    /// Opens the common LLM provider manager, then refreshes any provider-dependent UI.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected void LlmMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ProviderManagerDialog.Show(ProviderSettingsStore, this);
        OnProvidersChanged();
    }

    /// <summary>
    /// Opens the read-only tool management window listing the tools the LLM can use.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected void ToolManagerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToolManagerDialog dialog = new ToolManagerDialog(AgentToolHost.ListTools())
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// Opens the edit dialog for project criteria using one line per criterion.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected void CriteriaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentProject is not { } project)
        {
            return;
        }

        string projectRoot = project.Path;
        string criteriaPath = System.IO.Path.Combine(
            System.IO.Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.CriteriaFileName);

        try
        {
            // Present one criterion title per line instead of the raw JSON file.
            IReadOnlyList<Criterion> existing = Criteria.List(projectRoot);
            string initialText = string.Join(Environment.NewLine, existing.Select(criterion => criterion.Title));
            EditDialog dialog = new EditDialog(criteriaPath, initialText)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            // Each line becomes one criterion; the program rebuilds criteria.json.
            string[] lines = dialog.EditedContent.Split('\n');
            Criteria.ReplaceFromLines(projectRoot, lines);
            System.Windows.MessageBox.Show(this, "기준을 저장했습니다.", "기준 편집", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "기준 편집 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

}
