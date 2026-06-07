using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Artifacts;
using LlmIde.Infrastructure.Json;
using LlmIde.Infrastructure.Projects;

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
        ProjectCreateDialog dialog = new ProjectCreateDialog
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
            ProjectInitializationRequest request = dialog.CreateRequest();
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
        ProjectRenameDialog dialog = new ProjectRenameDialog(selectedProject.DisplayName)
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
public sealed class ConversationListItem
{
    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the importance weight.
    /// </summary>
    public int ImportanceWeight { get; set; }

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
    public string AssistantContent { get; set; } = string.Empty;
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
    }

    /// <summary>
    /// Gets the artifact identifier.
    /// </summary>
    public string ArtifactId { get; }

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
            CreatedAtText = createdAt == default ? string.Empty : createdAt.ToString("yyyy-MM-dd HH:mm"),
            UserContent = userMessage.Content,
            AssistantContent = assistantMessage?.Content ?? string.Empty
        };
    }
}
