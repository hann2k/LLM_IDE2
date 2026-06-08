using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using LlmIde.Infrastructure.Artifacts;
using LlmIde.Infrastructure.Conversations;
using LlmIde.Infrastructure.Json;
using LlmIde.Infrastructure.Projects;
using LlmIde.Infrastructure.Providers;
using Microsoft.Data.Sqlite;

namespace LlmIde.Wpf;

/// <summary>
/// Provides the first WPF shell for the LLM IDE.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// The main window view model.
    /// </summary>
    private readonly MainWindowViewModel viewModel;

    /// <summary>
    /// The project registry service.
    /// </summary>
    private readonly ProjectRegistryService projectRegistryService;

    /// <summary>
    /// The project metadata store.
    /// </summary>
    private readonly IProjectStore projectStore;

    /// <summary>
    /// The project initializer.
    /// </summary>
    private readonly ProjectInitializer projectInitializer;

    /// <summary>
    /// The artifact service.
    /// </summary>
    private readonly ArtifactService artifactService;

    /// <summary>
    /// The provider settings store.
    /// </summary>
    private readonly IProviderSettingsStore providerSettingsStore;

    /// <summary>
    /// The chat service.
    /// </summary>
    private readonly ChatService chatService;

    /// <summary>
    /// The conversation log store used for importance updates.
    /// </summary>
    private readonly IConversationLogStore conversationLogStore;

    /// <summary>
    /// The criteria service.
    /// </summary>
    private readonly CriteriaService criteriaService;

    /// <summary>
    /// A value indicating whether a chat request is currently in progress.
    /// </summary>
    private bool isSending;

    /// <summary>
    /// A value indicating whether the chat input box is being dragged.
    /// </summary>
    private bool isDraggingChatInput;

    /// <summary>
    /// The last drag position while moving the chat input box.
    /// </summary>
    private System.Windows.Point chatInputDragStart;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        string ideProgramRoot = AppContext.BaseDirectory;
        viewModel = new MainWindowViewModel();
        projectStore = new JsonFileProjectStore(ideProgramRoot);
        projectRegistryService = new ProjectRegistryService(new JsonProjectRegistryStore(ideProgramRoot));
        projectInitializer = new ProjectInitializer(projectStore, projectRegistryService, ideProgramRoot);
        artifactService = new ArtifactService(new FileArtifactStore());
        providerSettingsStore = new JsonProviderSettingsStore();

        // Compose the same Core chat pipeline the CLI uses.
        criteriaService = new CriteriaService(new JsonCriteriaStore());
        ProjectStateService projectStateService = new ProjectStateService(new JsonProjectStateStore());
        FileRollingContextStore rollingContextStore = new FileRollingContextStore(ideProgramRoot);
        ContextBuilder contextBuilder = new ContextBuilder(
            criteriaService,
            projectStateService,
            rollingContextStore,
            new FileSystemRuleStore(),
            new FileArtifactRuleStore(),
            new FileImportanceRuleStore(ideProgramRoot),
            artifactService);
        DeepSeekChatProvider deepSeekProvider = new DeepSeekChatProvider(new HttpClient());
        conversationLogStore = new CompositeConversationLogStore(
        [
            new JsonlConversationLogStore(),
            new SqliteConversationLogStore()
        ]);
        chatService = new ChatService(
            providerSettingsStore,
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = deepSeekProvider
            },
            conversationLogStore,
            contextBuilder,
            rollingContextStore,
            artifactService);

        DataContext = viewModel;
    }

    /// <summary>
    /// Loads the initial WPF shell data.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProjects();
    }

    /// <summary>
    /// Handles project selection changes.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (viewModel.SelectedProject is null)
        {
            viewModel.Conversations.Clear();
            viewModel.Artifacts.Clear();
            return;
        }

        LoadProjectDetails(viewModel.SelectedProject);
    }

    /// <summary>
    /// Opens the project creation dialog and creates a project when accepted.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CreateProjectMenuItem_Click(object sender, RoutedEventArgs e)
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
            string pId = projectRegistryService.NextProjectPId();
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
            ProjectInitializationResult result = projectInitializer.Initialize(request);
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
    private void CloneProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        ProjectListItem source = viewModel.SelectedProject;

        try
        {
            string sourceRoot = System.IO.Path.GetFullPath(source.Path);
            string startLocation = System.IO.Path.GetDirectoryName(sourceRoot) ?? AppContext.BaseDirectory;
            string newPId = projectRegistryService.NextProjectPId();
            string destinationRoot = System.IO.Path.Combine(startLocation, newPId);

            // Release SQLite pools so the source database file can be copied.
            SqliteConnection.ClearAllPools();
            CopyDirectory(sourceRoot, destinationRoot);
            projectRegistryService.Add(new ProjectRegistryEntry
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
    private void RenameProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        ProjectListItem selectedProject = viewModel.SelectedProject;
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
            projectRegistryService.Rename(selectedProject.PId, dialog.ProjectName);
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
    private void DeleteProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        ProjectListItem selectedProject = viewModel.SelectedProject;
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
    private void DeleteProject(ProjectListItem project)
    {
        string projectRoot = System.IO.Path.GetFullPath(project.Path);

        if (Directory.Exists(projectRoot))
        {
            // Release SQLite connection pools so the conversation database file can be removed.
            SqliteConnection.ClearAllPools();
            Directory.Delete(projectRoot, true);
        }

        projectRegistryService.Remove(project.PId);
    }

    /// <summary>
    /// Determines whether a project has any stored provider API key.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>True when at least one API key exists.</returns>
    private bool HasApiKey(string projectRoot)
    {
        ProviderSettingsDocument settings = providerSettingsStore.Load(projectRoot);
        return settings.Providers.Any(provider => !string.IsNullOrWhiteSpace(provider.ApiKey));
    }

    /// <summary>
    /// Opens the artifact rule file in the edit dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ArtifactRuleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditPolicyFile(LlmIdeLayout.ArtifactRuleFileName);
    }

    /// <summary>
    /// Opens the compression rule file in the edit dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CompressionRuleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditPolicyFile(LlmIdeLayout.CompressionRuleFileName);
    }

    /// <summary>
    /// Opens the system rule file in the edit dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void SystemRuleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditPolicyFile(LlmIdeLayout.SystemRuleFileName);
    }

    /// <summary>
    /// Opens the importance rule file in the edit dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ImportanceRuleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditPolicyFile(LlmIdeLayout.ImportanceRuleFileName);
    }

    /// <summary>
    /// Opens the edit dialog for project criteria using one line per criterion.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CriteriaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        string projectRoot = viewModel.SelectedProject.Path;
        string criteriaPath = Path.Combine(
            System.IO.Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.CriteriaFileName);

        try
        {
            // Present one criterion title per line instead of the raw JSON file.
            IReadOnlyList<Criterion> existing = criteriaService.List(projectRoot);
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
            criteriaService.ReplaceFromLines(projectRoot, lines);
            System.Windows.MessageBox.Show(this, "기준을 저장했습니다.", "기준 편집", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "기준 편집 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the edit dialog for a project policy file.
    /// </summary>
    /// <param name="policyFileName">The policy file name under the policies folder.</param>
    private void EditPolicyFile(string policyFileName)
    {
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        string policyPath = Path.Combine(
            System.IO.Path.GetFullPath(viewModel.SelectedProject.Path),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.PoliciesDirectoryName,
            policyFileName);
        EditTextFile(policyPath);
    }

    /// <summary>
    /// Opens the edit dialog for a text file and saves changes.
    /// </summary>
    /// <param name="filePath">The file path to edit.</param>
    private void EditTextFile(string filePath)
    {
        try
        {
            string content = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            EditDialog dialog = new EditDialog(filePath, content)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllText(filePath, dialog.EditedContent);
            System.Windows.MessageBox.Show(this, "저장했습니다.", "편집", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "편집 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the edit dialog for the project's provider API key and saves changes.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ApiKeyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        string projectRoot = System.IO.Path.GetFullPath(viewModel.SelectedProject.Path);
        string settingsPath = Path.Combine(
            projectRoot,
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.SettingsDirectoryName,
            LlmIdeLayout.ProvidersFileName);

        try
        {
            ProviderSettingsDocument document = providerSettingsStore.Load(projectRoot);
            ProviderSettings settings = GetDefaultProviderSettings(document);
            EditDialog dialog = new EditDialog(settingsPath, settings.ApiKey)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            settings.ApiKey = dialog.EditedContent.Trim();
            providerSettingsStore.Save(projectRoot, document);
            System.Windows.MessageBox.Show(this, "API 키를 저장했습니다.", "API 키 입력", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "API 키 입력 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Gets the default provider settings from a settings document.
    /// </summary>
    /// <param name="document">The provider settings document.</param>
    /// <returns>The default provider settings.</returns>
    private static ProviderSettings GetDefaultProviderSettings(ProviderSettingsDocument document)
    {
        ProviderSettings? settings = document.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Name, document.DefaultProvider, StringComparison.OrdinalIgnoreCase));

        if (settings is not null)
        {
            return settings;
        }

        if (document.Providers.Count > 0)
        {
            return document.Providers[0];
        }

        throw new InvalidOperationException("프로바이더 설정이 없습니다.");
    }

    /// <summary>
    /// Loads registered projects into the project list.
    /// </summary>
    private void LoadProjects()
    {
        viewModel.Projects.Clear();
        IReadOnlyList<ProjectRegistryEntry> projects = projectRegistryService.List();

        foreach (ProjectRegistryEntry project in projects)
        {
            viewModel.Projects.Add(new ProjectListItem(project));
        }

        if (viewModel.Projects.Count > 0)
        {
            viewModel.SelectedProject = viewModel.Projects[0];
        }
        else
        {
            viewModel.SelectedProject = null;
        }
    }

    /// <summary>
    /// Selects a project by project root path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    private void SelectProjectByRoot(string projectRoot)
    {
        string normalizedProjectRoot = System.IO.Path.GetFullPath(projectRoot);

        foreach (ProjectListItem project in viewModel.Projects)
        {
            if (!string.Equals(System.IO.Path.GetFullPath(project.Path), normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            viewModel.SelectedProject = project;
            return;
        }
    }

    /// <summary>
    /// Selects a project by project identifier.
    /// </summary>
    /// <param name="pId">The project identifier.</param>
    private void SelectProjectByPId(string pId)
    {
        foreach (ProjectListItem project in viewModel.Projects)
        {
            if (!string.Equals(project.PId, pId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            viewModel.SelectedProject = project;
            return;
        }
    }

    /// <summary>
    /// Loads conversation and artifact data for a selected project.
    /// </summary>
    /// <param name="project">The selected project.</param>
    private void LoadProjectDetails(ProjectListItem project)
    {
        viewModel.Conversations.Clear();
        viewModel.Artifacts.Clear();

        foreach (ConversationListItem conversation in ConversationLogReader.Read(project.Path))
        {
            viewModel.Conversations.Add(conversation);
        }

        foreach (Artifact artifact in artifactService.List(project.Path))
        {
            viewModel.Artifacts.Add(new ArtifactListItem(artifact));
        }

        // After a project's conversations load, scroll to the latest one once.
        ScrollConversationsToEnd();
    }

    /// <summary>
    /// Scrolls the conversation grid to the latest conversation.
    /// </summary>
    private void ScrollConversationsToEnd()
    {
        if (viewModel.Conversations.Count == 0)
        {
            return;
        }

        ConversationListItem last = viewModel.Conversations[^1];

        // Defer until the rows are generated so the scroll reaches the bottom.
        Dispatcher.InvokeAsync(() => ConversationGrid.ScrollIntoView(last), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Shows the chat input box when Enter is pressed outside an editor.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        // When the input box is already open, let its own handler manage Enter.
        if (ChatInputPanel.Visibility == Visibility.Visible)
        {
            return;
        }

        if (isSending || viewModel.SelectedProject is null)
        {
            return;
        }

        ShowChatInput();
        e.Handled = true;
    }

    /// <summary>
    /// Handles Enter and Escape inside the chat input box.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ChatInputTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            HandleChatInputEnter();
            return;
        }

        if (e.Key == Key.Escape)
        {
            // Hide but keep the typed text.
            e.Handled = true;
            HideChatInput();
        }
    }

    /// <summary>
    /// Sends the chat input or closes the box when it is empty.
    /// </summary>
    private void HandleChatInputEnter()
    {
        string text = ChatInputTextBox.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            HideChatInput();
            ChatInputTextBox.Clear();
            System.Windows.MessageBox.Show(this, "전송할 내용이 없어 전송되지 않고 닫혔습니다.", "전송", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _ = SendMessageAsync(text);
    }

    /// <summary>
    /// Shows the chat input box and focuses it.
    /// </summary>
    private void ShowChatInput()
    {
        ChatInputPanel.Visibility = Visibility.Visible;
        ChatInputTextBox.Focus();
        ChatInputTextBox.CaretIndex = ChatInputTextBox.Text.Length;
    }

    /// <summary>
    /// Hides the chat input box without clearing the text.
    /// </summary>
    private void HideChatInput()
    {
        ChatInputPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Begins dragging the chat input box.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ChatInputDragHandle_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not UIElement handle)
        {
            return;
        }

        isDraggingChatInput = true;
        chatInputDragStart = e.GetPosition(this);
        handle.CaptureMouse();
    }

    /// <summary>
    /// Moves the chat input box while dragging.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ChatInputDragHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isDraggingChatInput)
        {
            return;
        }

        System.Windows.Point current = e.GetPosition(this);
        ChatInputTransform.X += current.X - chatInputDragStart.X;
        ChatInputTransform.Y += current.Y - chatInputDragStart.Y;
        chatInputDragStart = current;
    }

    /// <summary>
    /// Ends dragging the chat input box.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ChatInputDragHandle_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not UIElement handle)
        {
            return;
        }

        isDraggingChatInput = false;
        handle.ReleaseMouseCapture();
    }

    /// <summary>
    /// Sends a chat message to the project's provider and streams the response.
    /// </summary>
    /// <param name="text">The user message.</param>
    /// <returns>A task that completes when the chat finishes.</returns>
    private async Task SendMessageAsync(string text)
    {
        if (isSending || viewModel.SelectedProject is null)
        {
            return;
        }

        isSending = true;
        string projectRoot = viewModel.SelectedProject.Path;

        // The send is accepted: hide and clear the input box.
        HideChatInput();
        ChatInputTextBox.Clear();

        ConversationListItem? row = null;
        DateTimeOffset sentAt = DateTimeOffset.Now;

        try
        {
            ChatProviderResponse response = await chatService.StreamAsync(
                projectRoot,
                text,
                preview => RunOnUi(() => row = AddConversationRow(preview.RequestId, text, sentAt)),
                chunk => RunOnUi(() => AppendAssistantChunk(row, chunk)),
                CancellationToken.None);

            RunOnUi(() => FinalizeConversationRow(row, response));
            SaveResponseArtifacts(projectRoot, response);
            RunOnUi(() => ReloadArtifacts(projectRoot));
        }
        catch (Exception ex)
        {
            RunOnUi(() => HandleSendFailure(row, text, ex));
        }
        finally
        {
            isSending = false;
        }
    }

    /// <summary>
    /// Adds a new conversation row when the response starts.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="userText">The user message.</param>
    /// <param name="sentAt">The send timestamp.</param>
    /// <returns>The added conversation row.</returns>
    private ConversationListItem AddConversationRow(string requestId, string userText, DateTimeOffset sentAt)
    {
        ConversationListItem row = new ConversationListItem
        {
            RequestId = requestId,
            ImportanceWeight = 0,
            CreatedAtText = ConversationLogReader.FormatTimestamp(sentAt),
            UserContent = userText,
            AssistantContent = string.Empty
        };
        viewModel.Conversations.Add(row);
        ConversationGrid.ScrollIntoView(row);
        return row;
    }

    /// <summary>
    /// Appends a streamed chunk to a conversation row.
    /// </summary>
    /// <param name="row">The conversation row.</param>
    /// <param name="chunk">The streamed chunk.</param>
    private void AppendAssistantChunk(ConversationListItem? row, string chunk)
    {
        if (row is null)
        {
            return;
        }

        row.AssistantContent += chunk;
        ConversationGrid.ScrollIntoView(row);
    }

    /// <summary>
    /// Finalizes a conversation row with parsed content and importance.
    /// </summary>
    /// <param name="row">The conversation row.</param>
    /// <param name="response">The completed chat response.</param>
    private void FinalizeConversationRow(ConversationListItem? row, ChatProviderResponse response)
    {
        if (row is null)
        {
            return;
        }

        // Artifact blocks are separated out and replaced with their extracted name.
        row.AssistantContent = ArtifactTagParser.ReplaceArtifactsWithTitles(response.Content);
        row.ImportanceWeight = response.ImportanceWeight;
        ConversationGrid.ScrollIntoView(row);
    }

    /// <summary>
    /// Saves artifacts found in a response and adds them to the project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="response">The completed chat response.</param>
    private void SaveResponseArtifacts(string projectRoot, ChatProviderResponse response)
    {
        foreach (ArtifactCandidate candidate in response.ArtifactCandidates)
        {
            artifactService.Save(projectRoot, candidate, response.RequestId);
        }
    }

    /// <summary>
    /// Reloads the artifact list for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    private void ReloadArtifacts(string projectRoot)
    {
        viewModel.Artifacts.Clear();

        foreach (Artifact artifact in artifactService.List(projectRoot))
        {
            viewModel.Artifacts.Add(new ArtifactListItem(artifact));
        }
    }

    /// <summary>
    /// Handles a failed chat send.
    /// </summary>
    /// <param name="row">The conversation row, or null when none was added.</param>
    /// <param name="text">The user message.</param>
    /// <param name="ex">The error.</param>
    private void HandleSendFailure(ConversationListItem? row, string text, Exception ex)
    {
        // When no row was added the request was never stored, so restore the input for retry.
        if (row is null)
        {
            ChatInputTextBox.Text = text;
            ShowChatInput();
        }

        System.Windows.MessageBox.Show(this, ex.Message, "전송 오류", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Persists a keyboard-driven importance change. Mouse drags persist on drag completion.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ImportanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider slider || slider.DataContext is not ConversationListItem row)
        {
            return;
        }

        // During a mouse drag we wait for drag completion; ignore data binding and row recycling.
        if (slider.IsMouseCaptureWithin || !slider.IsKeyboardFocusWithin)
        {
            return;
        }

        int weight = Math.Clamp((int)Math.Round(e.NewValue), 0, 10);
        PersistImportanceWeight(row, weight);
    }

    /// <summary>
    /// Persists the importance weight when a slider drag completes.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ImportanceSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (sender is not Slider slider || slider.DataContext is not ConversationListItem row)
        {
            return;
        }

        int weight = Math.Clamp((int)Math.Round(slider.Value), 0, 10);
        PersistImportanceWeight(row, weight);
    }

    /// <summary>
    /// Persists an importance weight for a conversation row.
    /// </summary>
    /// <param name="row">The conversation row.</param>
    /// <param name="weight">The importance weight.</param>
    private void PersistImportanceWeight(ConversationListItem row, int weight)
    {
        if (viewModel.SelectedProject is null || string.IsNullOrWhiteSpace(row.RequestId))
        {
            return;
        }

        try
        {
            conversationLogStore.UpdateRequestImportanceWeight(viewModel.SelectedProject.Path, row.RequestId, weight);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "중요도 저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the artifact viewer for a clicked artifact.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ArtifactItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ArtifactListItem item)
        {
            return;
        }

        if (viewModel.SelectedProject is null)
        {
            return;
        }

        try
        {
            string projectRoot = viewModel.SelectedProject.Path;
            Artifact artifact = artifactService.Get(projectRoot, item.ArtifactId);
            string content = artifactService.ReadContent(projectRoot, artifact);
            ArtifactViewer viewer = new ArtifactViewer(item.Title, content)
            {
                Owner = this
            };
            viewer.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "아티팩트 뷰어 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Runs an action on the UI thread.
    /// </summary>
    /// <param name="action">The action to run.</param>
    private void RunOnUi(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.Invoke(action);
    }
}

/// <summary>
/// Provides bindable data for the main window.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// The selected project.
    /// </summary>
    private ProjectListItem? selectedProject;

    /// <summary>
    /// Occurs when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the registered projects.
    /// </summary>
    public ObservableCollection<ProjectListItem> Projects { get; } = [];

    /// <summary>
    /// Gets the project conversations.
    /// </summary>
    public ObservableCollection<ConversationListItem> Conversations { get; } = [];

    /// <summary>
    /// Gets the project artifacts.
    /// </summary>
    public ObservableCollection<ArtifactListItem> Artifacts { get; } = [];

    /// <summary>
    /// Gets or sets the selected project.
    /// </summary>
    public ProjectListItem? SelectedProject
    {
        get
        {
            return selectedProject;
        }

        set
        {
            if (ReferenceEquals(selectedProject, value))
            {
                return;
            }

            selectedProject = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedProject));
        }
    }

    /// <summary>
    /// Gets a value indicating whether a project is selected.
    /// </summary>
    public bool HasSelectedProject
    {
        get
        {
            return SelectedProject is not null;
        }
    }

    /// <summary>
    /// Raises the property changed event.
    /// </summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a project entry displayed by the WPF shell.
/// </summary>
public sealed class ProjectListItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectListItem"/> class.
    /// </summary>
    /// <param name="entry">The project registry entry.</param>
    public ProjectListItem(ProjectRegistryEntry entry)
    {
        PId = entry.PId;
        DisplayName = entry.Name;
        Path = entry.Path;
    }

    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    public string PId { get; }

    /// <summary>
    /// Gets the user-visible project name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the project root path.
    /// </summary>
    public string Path { get; }
}

/// <summary>
/// Represents one conversation row in the WPF shell.
/// </summary>
public sealed class ConversationListItem : INotifyPropertyChanged
{
    /// <summary>
    /// The importance weight.
    /// </summary>
    private int importanceWeight;

    /// <summary>
    /// The assistant message content.
    /// </summary>
    private string assistantContent = string.Empty;

    /// <summary>
    /// Occurs when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the importance weight.
    /// </summary>
    public int ImportanceWeight
    {
        get
        {
            return importanceWeight;
        }

        set
        {
            if (importanceWeight == value)
            {
                return;
            }

            importanceWeight = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the created-at display text.
    /// </summary>
    public string CreatedAtText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user message content.
    /// </summary>
    public string UserContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assistant message content.
    /// </summary>
    public string AssistantContent
    {
        get
        {
            return assistantContent;
        }

        set
        {
            if (assistantContent == value)
            {
                return;
            }

            assistantContent = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Raises the property changed event.
    /// </summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents one artifact displayed by the WPF shell.
/// </summary>
public sealed class ArtifactListItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactListItem"/> class.
    /// </summary>
    /// <param name="artifact">The stored artifact.</param>
    public ArtifactListItem(Artifact artifact)
    {
        ArtifactId = artifact.ArtifactId;
        Title = artifact.Title;
        Type = artifact.Type;
        SourceRequestId = artifact.SourceRequestId;
    }

    /// <summary>
    /// Gets the artifact identifier.
    /// </summary>
    public string ArtifactId { get; }

    /// <summary>
    /// Gets the source conversation request identifier.
    /// </summary>
    public string SourceRequestId { get; }

    /// <summary>
    /// Gets the artifact title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the artifact type.
    /// </summary>
    public string Type { get; }
}

/// <summary>
/// Reads conversation list data from the project JSONL logs.
/// </summary>
public static class ConversationLogReader
{
    /// <summary>
    /// Reads conversation rows from a project root.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The conversation rows.</returns>
    public static IReadOnlyList<ConversationListItem> Read(string projectRoot)
    {
        string conversationDirectory = Path.Combine(
            System.IO.Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.ConversationsDirectoryName);
        string requestPath = Path.Combine(conversationDirectory, LlmIdeLayout.RequestsFileName);
        string messagePath = Path.Combine(conversationDirectory, LlmIdeLayout.MessagesFileName);
        List<ConversationRequestRecord> requests = ReadJsonLines<ConversationRequestRecord>(requestPath);
        List<ConversationMessageRecord> messages = ReadJsonLines<ConversationMessageRecord>(messagePath);
        Dictionary<string, ConversationRequestRecord> requestsById = BuildRequestLookup(requests);
        List<ConversationListItem> rows = [];

        foreach (ConversationMessageRecord message in messages)
        {
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ConversationMessageRecord? assistantMessage = FindAssistantMessage(messages, message.RequestId);
            ConversationRequestRecord? request = GetRequestOrDefault(requestsById, message.RequestId);
            rows.Add(CreateConversationRow(message, assistantMessage, request));
        }

        return rows;
    }

    /// <summary>
    /// Reads JSONL records from a file.
    /// </summary>
    /// <typeparam name="TValue">The record type.</typeparam>
    /// <param name="path">The JSONL path.</param>
    /// <returns>The parsed records.</returns>
    private static List<TValue> ReadJsonLines<TValue>(string path)
    {
        List<TValue> values = [];

        if (!File.Exists(path))
        {
            return values;
        }

        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            TValue? value = JsonSerializer.Deserialize<TValue>(line, JsonOptions.Compact);

            if (value is null)
            {
                continue;
            }

            values.Add(value);
        }

        return values;
    }

    /// <summary>
    /// Builds a request lookup by request identifier.
    /// </summary>
    /// <param name="requests">The request records.</param>
    /// <returns>The request lookup.</returns>
    private static Dictionary<string, ConversationRequestRecord> BuildRequestLookup(IReadOnlyList<ConversationRequestRecord> requests)
    {
        Dictionary<string, ConversationRequestRecord> requestsById = new Dictionary<string, ConversationRequestRecord>(StringComparer.Ordinal);

        foreach (ConversationRequestRecord request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                continue;
            }

            requestsById[request.RequestId] = request;
        }

        return requestsById;
    }

    /// <summary>
    /// Finds an assistant message for the request.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The assistant message, or null.</returns>
    private static ConversationMessageRecord? FindAssistantMessage(
        IReadOnlyList<ConversationMessageRecord> messages,
        string requestId)
    {
        foreach (ConversationMessageRecord message in messages)
        {
            if (!string.Equals(message.RequestId, requestId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a request from a lookup if it exists.
    /// </summary>
    /// <param name="requestsById">The request lookup.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The request record, or null.</returns>
    private static ConversationRequestRecord? GetRequestOrDefault(
        Dictionary<string, ConversationRequestRecord> requestsById,
        string requestId)
    {
        if (requestsById.TryGetValue(requestId, out ConversationRequestRecord? request))
        {
            return request;
        }

        return null;
    }

    /// <summary>
    /// Creates one conversation list row.
    /// </summary>
    /// <param name="userMessage">The user message.</param>
    /// <param name="assistantMessage">The assistant message.</param>
    /// <param name="request">The request record.</param>
    /// <returns>The conversation list row.</returns>
    private static ConversationListItem CreateConversationRow(
        ConversationMessageRecord userMessage,
        ConversationMessageRecord? assistantMessage,
        ConversationRequestRecord? request)
    {
        DateTimeOffset createdAt = userMessage.CreatedAt;

        if (request is not null && request.CreatedAt != default)
        {
            createdAt = request.CreatedAt;
        }

        return new ConversationListItem
        {
            RequestId = userMessage.RequestId,
            ImportanceWeight = request?.ImportanceWeight ?? 0,
            CreatedAtText = createdAt == default ? string.Empty : FormatTimestamp(createdAt),
            UserContent = userMessage.Content,
            AssistantContent = ArtifactTagParser.ReplaceArtifactsWithTitles(assistantMessage?.Content ?? string.Empty)
        };
    }

    /// <summary>
    /// Formats a timestamp for display.
    /// </summary>
    /// <param name="value">The timestamp.</param>
    /// <returns>The formatted timestamp text.</returns>
    public static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("[yyyy-MM-dd HH:mm:ss.fff]");
    }
}
