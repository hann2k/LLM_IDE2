using LlmIde.Wpf.Lib;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LlmIde.Core.Agents;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Conversations;
using LlmIde.Core.Diagnostics;
using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using LlmIde.Infrastructure.Agents;
using LlmIde.Infrastructure.Artifacts;
using LlmIde.Infrastructure.Conversations;
using LlmIde.Infrastructure.Diagnostics;
using LlmIde.Infrastructure.Json;
using LlmIde.Infrastructure.Projects;
using LlmIde.Infrastructure.Providers;
using Microsoft.Data.Sqlite;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Provides the first WPF shell for the LLM IDE.
/// </summary>
public partial class MainWindow : WorkspaceWindowBase
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
    /// The agent tool host (provides the list of tools the LLM can use).
    /// </summary>
    private readonly IAgentToolHost agentToolHost;

    /// <summary>
    /// The criteria service.
    /// </summary>
    private readonly CriteriaService criteriaService;

    /// <summary>
    /// A value indicating whether a chat request is currently in progress.
    /// </summary>
    private bool isSending;

    /// <summary>
    /// Persists and restores the resizable panel layout.
    /// </summary>
    private readonly WindowLayoutStore layoutStore;

    /// <summary>
    /// Persists the document outline and per-item body files under the project root.
    /// </summary>
    private readonly OutlineStore outlineStore = new OutlineStore();

    /// <summary>
    /// Bridges the body-editing agent tools to this editor.
    /// </summary>
    private readonly WriterDocumentBodyBridge bodyBridge;

    /// <summary>
    /// Suppresses the provider-change handler while the selector is being populated.
    /// </summary>
    private bool suppressProviderChange;

    /// <summary>
    /// The mouse position where an outline drag may have started.
    /// </summary>
    private System.Windows.Point outlineDragStart;

    /// <summary>
    /// The outline item under the mouse at left-button-down (drag candidate).
    /// </summary>
    private OutlineItem? outlineDragItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        string ideProgramRoot = LlmIde.Infrastructure.ProgramDataRoot.EnsureAndGet();
        Log.Ins.SetLogDir(LlmIde.Infrastructure.ProgramDataRoot.GetLogDir());
        Log.Ins.Debug("시작");

        InitializeComponent();

        // Open Markdown hyperlinks (in rendered conversation responses) in the default browser.
        CommandBindings.Add(new System.Windows.Input.CommandBinding(Markdig.Wpf.Commands.Hyperlink, OpenMarkdownHyperlink));
        
        layoutStore = new WindowLayoutStore(ideProgramRoot);
        viewModel = new MainWindowViewModel();
        projectStore = new JsonFileProjectStore(ideProgramRoot);
        projectRegistryService = new ProjectRegistryService(new JsonProjectRegistryStore(ideProgramRoot));
        projectInitializer = new ProjectInitializer(projectStore, projectRegistryService, ideProgramRoot);
        artifactService = new ArtifactService(new FileArtifactStore());
        providerSettingsStore = new JsonProviderSettingsStore(ideProgramRoot);

        // Compose the same Core chat pipeline the CLI uses.
        criteriaService = new CriteriaService(new JsonCriteriaStore());
        ProjectStateService projectStateService = new ProjectStateService(new JsonProjectStateStore());
        FileRollingContextStore rollingContextStore = new FileRollingContextStore(ideProgramRoot);
        FilePromptStore promptStore = new FilePromptStore(ideProgramRoot);
        ContextBuilder contextBuilder = new ContextBuilder(
            criteriaService,
            projectStateService,
            rollingContextStore,
            new FileSystemRuleStore(ideProgramRoot),
            new FileArtifactRuleStore(ideProgramRoot),
            new FileImportanceRuleStore(ideProgramRoot),
            artifactService,
            promptStore);
        DeepSeekChatProvider deepSeekProvider = new DeepSeekChatProvider(new HttpClient());
        ClaudeChatProvider claudeProvider = new ClaudeChatProvider(new HttpClient());
        conversationLogStore = new CompositeConversationLogStore(
        [
            new JsonlConversationLogStore(),
            new SqliteConversationLogStore()
        ]);
        HttpClient fetchHttpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        // Bridge the body-editing tools to this editor: read the selected section on the UI thread,
        // and stage proposed revisions for user approval after the agent loop.
        bodyBridge = new WriterDocumentBodyBridge(
            Dispatcher,
            () => viewModel.SelectedOutlineItem is OutlineItem item
                ? new DocumentBodySnapshot { Title = item.Title, Body = viewModel.BodyText }
                : null);
        agentToolHost = AgentToolHostFactory.Create(ideProgramRoot, fetchHttpClient, artifactService, bodyBridge);
        // Records every actual LLM call (injected prompt + raw response) via the supervisor-supplied Framework.Common file logger.
        ILlmRequestLogger llmRequestLogger = new FrameworkCommonLlmRequestLogger(Path.Combine(ideProgramRoot, "Log"));
        chatService = new ChatService(
            providerSettingsStore,
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = deepSeekProvider,
                ["claude"] = claudeProvider
            },
            conversationLogStore,
            contextBuilder,
            rollingContextStore,
            artifactService,
            projectStore,
            agentToolHost,
            llmRequestLogger,
            promptStore,
            new FrameworkCommonToolIoLogger(Path.Combine(ideProgramRoot, "Log")));

        DataContext = viewModel;

        // Persist layout + the in-progress body when the window closes.
        Closing += (_, _) =>
        {
            SaveCurrentBody();
            SaveLayout();
        };
    }

    /// <summary>
    /// Loads the initial WPF shell data.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        ApplyLayout();
        LoadProjects();
        WarnIfNoApiKey();
    }

    /// <summary>
    /// On startup, nudges a first-time/pilot user to set an LLM API key (편집 &gt; LLM) when none is
    /// configured, so it is clear why chat and body editing would otherwise fail.
    /// </summary>
    private void WarnIfNoApiKey()
    {
        Log.Ins.Debug("시작");
        try
        {
            // Providers are common (program root); the project argument is ignored by the store.
            ProviderSettingsDocument providers = providerSettingsStore.Load(string.Empty);

            if (providers.Providers.Any(provider => !string.IsNullOrWhiteSpace(provider.ApiKey)))
            {
                return;
            }

            System.Windows.MessageBox.Show(
                this,
                "LLM API 키가 설정되어 있지 않습니다.\n[편집 > LLM] 메뉴에서 프로바이더 API 키를 입력해야 채팅과 본문 편집이 동작합니다.",
                "API 키 필요",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch
        {
            // The startup nudge must never block the app from opening.
        }
    }

    /// <summary>
    /// Applies the saved resizable-panel layout, if any.
    /// </summary>
    private void ApplyLayout()
    {
        Log.Ins.Debug("시작");
        WindowLayout? layout = layoutStore.Load();

        if (layout is null)
        {
            return;
        }

        if (layout.ProjectWidth > 0)
        {
            ProjectColumn.Width = new GridLength(layout.ProjectWidth);
        }

        if (layout.ChatWidth > 0)
        {
            ChatColumn.Width = new GridLength(layout.ChatWidth);
        }

        if (layout.OutlineWidth > 0)
        {
            OutlineColumn.Width = new GridLength(layout.OutlineWidth);
        }

        if (layout.ArtifactHeight > 0)
        {
            ArtifactRow.Height = new GridLength(layout.ArtifactHeight);
        }

        if (layout.ComposerHeight > 0)
        {
            ComposerRow.Height = new GridLength(layout.ComposerHeight);
        }
    }

    /// <summary>
    /// Saves the current resizable-panel sizes.
    /// </summary>
    private void SaveLayout()
    {
        Log.Ins.Debug("시작");
        layoutStore.Save(new WindowLayout
        {
            ProjectWidth = ProjectColumn.ActualWidth,
            ChatWidth = ChatColumn.ActualWidth,
            OutlineWidth = OutlineColumn.ActualWidth,
            ArtifactHeight = ArtifactRow.ActualHeight,
            ComposerHeight = ComposerRow.ActualHeight
        });
    }

    /// <summary>
    /// Persists the layout when a panel boundary is dragged.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void LayoutSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        Log.Ins.Debug("시작");
        SaveLayout();
    }

    /// <summary>
    /// Opens a clicked Markdown hyperlink (in a rendered response) in the default browser.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The command event arguments carrying the link target.</param>
    private void OpenMarkdownHyperlink(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        string? url = e.Parameter as string;

        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        // 잘못된 링크(상대 경로 등)가 앱을 종료시키지 않도록 방어한다 (DEC-087).
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"링크를 열 수 없습니다: {url}\n{ex.Message}", "링크 열기 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Handles project selection changes.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Save the in-progress body to the previously selected project before switching.
        Log.Ins.Debug("시작");
        if (e.RemovedItems.Count > 0
            && e.RemovedItems[0] is ProjectListItem previousProject
            && viewModel.SelectedOutlineItem is OutlineItem previousItem)
        {
            outlineStore.WriteBody(previousProject.Path, previousItem, viewModel.BodyText);
        }

        if (viewModel.SelectedProject is null)
        {
            viewModel.Conversations.Clear();
            viewModel.Artifacts.Clear();
            return;
        }

        LoadProjectDetails(viewModel.SelectedProject);
    }


    /// <inheritdoc />
    protected override ProjectRegistryService ProjectRegistry => projectRegistryService;

    /// <inheritdoc />
    protected override ProjectInitializer Initializer => projectInitializer;

    /// <inheritdoc />
    protected override IProjectStore ProjectStore => projectStore;

    /// <inheritdoc />
    protected override IProviderSettingsStore ProviderSettingsStore => providerSettingsStore;

    /// <inheritdoc />
    protected override IAgentToolHost AgentToolHost => agentToolHost;

    /// <inheritdoc />
    protected override CriteriaService Criteria => criteriaService;

    /// <inheritdoc />
    protected override WorkspaceProject? CurrentProject =>
        viewModel.SelectedProject is { } project
            ? new WorkspaceProject(project.Path, project.DisplayName, project.PId)
            : null;

    /// <inheritdoc />
    protected override void ReleaseProjectFileLocks() 
    {
        Log.Ins.Debug("시작");
        SqliteConnection.ClearAllPools();
    }

    /// <inheritdoc />
    protected override void OnProvidersChanged() 
    {
        Log.Ins.Debug("시작");
        PopulateComposerProviders();
    }

    /// <summary>
    /// Loads registered projects into the project list.
    /// </summary>
    protected override void LoadProjects()
    {
        Log.Ins.Debug("시작");
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
    protected override void SelectProjectByRoot(string projectRoot)
    {
        Log.Ins.Debug("시작");
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
    protected override void SelectProjectByPId(string pId)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        viewModel.Conversations.Clear();
        viewModel.Artifacts.Clear();

        // Load the document outline (and reset the body editor) for this project.
        SetBodyPreviewMode(false);
        viewModel.SelectedOutlineItem = null;
        viewModel.BodyText = string.Empty;
        ResetBodyUndoHistory();
        viewModel.OutlineItems.Clear();

        foreach (OutlineItem root in outlineStore.Load(project.Path))
        {
            viewModel.OutlineItems.Add(root);
        }

        RenumberOutline();

        // Importance is editable only for long-term conversation projects.
        try
        {
            viewModel.IsImportanceEditable = projectStore.ReadProjectInfo(project.Path).LongTermConversation;
        }
        catch (Exception)
        {
            viewModel.IsImportanceEditable = true;
        }

        foreach (ConversationListItem conversation in ConversationLogReader.Read(project.Path))
        {
            viewModel.Conversations.Add(conversation);
        }

        foreach (Artifact artifact in artifactService.List(project.Path))
        {
            viewModel.Artifacts.Add(ToArtifactListItem(project.Path, artifact));
        }

        // After a project's conversations load, scroll to the latest one once.
        ScrollConversationsToEnd();

        PopulateComposerProviders();
    }

    /// <summary>
    /// Scrolls the conversation grid to the latest conversation.
    /// </summary>
    private void ScrollConversationsToEnd()
    {
        Log.Ins.Debug("시작");
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
    /// <summary>
    /// Sends the composer text on the 전송 button click.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ComposerSend_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        SubmitComposer();
    }

    /// <summary>
    /// Sends the composer text on Enter (Shift+Enter inserts a newline).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ComposerInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return;
        }

        e.Handled = true;
        SubmitComposer();
    }

    /// <summary>
    /// Submits the composer text to the chat pipeline (outline-scoped) and clears the input.
    /// </summary>
    private void SubmitComposer()
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        string text = ComposerInput.Text.Trim();

        if (text.Length == 0)
        {
            return;
        }

        ComposerInput.Clear();
        _ = SendMessageAsync(text);
    }

    /// <summary>
    /// Tracks the selected outline item so the body editor can gate input on it.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        Log.Ins.Debug("시작");
        // The preview is a snapshot; switching sections returns to the editor (DEC-084).
        SetBodyPreviewMode(false);
        string? projectRoot = viewModel.SelectedProject?.Path;

        // Save the previously selected item's body before switching (focus-move save).
        if (projectRoot is not null && e.OldValue is OutlineItem previous)
        {
            outlineStore.WriteBody(projectRoot, previous, viewModel.BodyText);
        }

        OutlineItem? current = e.NewValue as OutlineItem;
        viewModel.SelectedOutlineItem = current;

        // Load the newly selected item's body.
        viewModel.BodyText = projectRoot is not null && current is not null
            ? outlineStore.ReadBody(projectRoot, current)
            : string.Empty;

        // 다른 섹션의 텍스트가 Ctrl+Z로 되살아나지 않도록 스택을 비운다 (DEC-088).
        ResetBodyUndoHistory();
    }

    /// <summary>
    /// Ensures the item has a linked body file (created empty) under the current project root.
    /// </summary>
    /// <param name="item">The outline item.</param>
    private void PrepareBodyFile(OutlineItem item)
    {
        Log.Ins.Debug("시작");
        item.BodyFile = item.Id + ".md";

        if (viewModel.SelectedProject is not null)
        {
            outlineStore.EnsureBodyFile(viewModel.SelectedProject.Path, item);
        }
    }

    /// <summary>
    /// Saves the outline tree to the project root.
    /// </summary>
    private void SaveOutline()
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is not null)
        {
            outlineStore.Save(viewModel.SelectedProject.Path, viewModel.OutlineItems);
        }
    }

    /// <summary>
    /// Saves the currently selected item's body text.
    /// </summary>
    private void SaveCurrentBody()
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is not null && viewModel.SelectedOutlineItem is not null)
        {
            outlineStore.WriteBody(viewModel.SelectedProject.Path, viewModel.SelectedOutlineItem, viewModel.BodyText);
        }
    }

    /// <summary>
    /// Saves the current body and the outline tree.
    /// </summary>
    private void SaveAll()
    {
        Log.Ins.Debug("시작");
        SaveCurrentBody();
        SaveOutline();
    }

    /// <summary>
    /// Exports the outline + bodies to a .docx file chosen by the user.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ExportDocxMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        if (viewModel.OutlineItems.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "내보낼 목차가 없습니다.", "docx 내보내기", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Persist the latest body edit and outline before exporting (export reads bodies from disk).
        SaveAll();

        string projectRoot = viewModel.SelectedProject.Path;
        Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "docx 내보내기",
            Filter = "Word 문서 (*.docx)|*.docx",
            DefaultExt = ".docx",
            FileName = (string.IsNullOrWhiteSpace(viewModel.SelectedProject.DisplayName) ? "document" : viewModel.SelectedProject.DisplayName) + ".docx"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            DocxExporter.Export(dialog.FileName, viewModel.OutlineItems, item => outlineStore.ReadBody(projectRoot, item));
            System.Windows.MessageBox.Show(this, "docx로 내보냈습니다.\n" + dialog.FileName, "docx 내보내기", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "docx 내보내기 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Enables the save command only when a project is selected.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void SaveCommand_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        e.CanExecute = viewModel.SelectedProject is not null;
    }

    /// <summary>
    /// Saves the body and outline on Ctrl+S / 프로젝트 &gt; 저장.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void SaveCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        SaveAll();
    }

    /// <summary>
    /// Adds a sibling outline item after the selected item (or at the root when none is selected).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineAdd_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        string? title = TextInputDialog.Prompt("목차 추가", "새 목차 제목:", "새 항목", this);

        if (title is null)
        {
            return;
        }

        OutlineItem? selected = viewModel.SelectedOutlineItem;
        ObservableCollection<OutlineItem> collection = selected?.Parent?.Children ?? viewModel.OutlineItems;
        OutlineItem item = new OutlineItem { Id = NewOutlineId(), Title = title, Parent = selected?.Parent };
        int index = selected is null ? collection.Count : collection.IndexOf(selected) + 1;
        collection.Insert(index, item);

        RenumberOutline();
        PrepareBodyFile(item);
        SaveOutline();
        item.IsSelected = true;
    }

    /// <summary>
    /// Adds a child outline item under the selected item.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineAddChild_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        OutlineItem? selected = viewModel.SelectedOutlineItem;

        if (selected is null)
        {
            OutlineAdd_Click(sender, e);
            return;
        }

        string? title = TextInputDialog.Prompt("하위 목차 추가", "새 하위 목차 제목:", "새 항목", this);

        if (title is null)
        {
            return;
        }

        OutlineItem item = new OutlineItem { Id = NewOutlineId(), Title = title, Parent = selected };
        selected.Children.Add(item);
        selected.IsExpanded = true;

        RenumberOutline();
        PrepareBodyFile(item);
        SaveOutline();
        item.IsSelected = true;
    }

    /// <summary>
    /// Begins inline title editing on double-click.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (sender is FrameworkElement element && element.DataContext is OutlineItem item)
        {
            item.IsEditing = true;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Focuses the inline edit box when it becomes visible.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (sender is System.Windows.Controls.TextBox box && box.IsVisible)
        {
            box.Focus();
            box.SelectAll();
        }
    }

    /// <summary>
    /// Commits or cancels inline editing on Enter/Escape.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (sender is System.Windows.Controls.TextBox box
            && (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape)
            && box.DataContext is OutlineItem item)
        {
            item.RevertTitleIfEmpty();
            item.IsEditing = false;
            SaveOutline();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Ends inline editing when the edit box loses focus.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (sender is System.Windows.Controls.TextBox box && box.DataContext is OutlineItem item)
        {
            item.RevertTitleIfEmpty();
            item.IsEditing = false;
            SaveOutline();
        }
    }

    /// <summary>
    /// Renames the selected outline item.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineRename_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        OutlineItem? selected = viewModel.SelectedOutlineItem;

        if (selected is null)
        {
            System.Windows.MessageBox.Show(this, "이름을 변경할 목차를 먼저 선택하세요.", "목차", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string? title = TextInputDialog.Prompt("이름 변경", "목차 제목:", selected.Title, this);

        if (title is null)
        {
            return;
        }

        selected.Title = title;
        SaveOutline();
    }

    /// <summary>
    /// Deletes the selected outline item (and its children) after confirmation.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineDelete_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        OutlineItem? selected = viewModel.SelectedOutlineItem;

        if (selected is null)
        {
            System.Windows.MessageBox.Show(this, "삭제할 목차를 먼저 선택하세요.", "목차", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string message = selected.Children.Count > 0
            ? $"'{selected.Title}'와(과) 하위 목차를 모두 삭제할까요?"
            : $"'{selected.Title}'을(를) 삭제할까요?";

        if (System.Windows.MessageBox.Show(this, message, "목차 삭제", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        if (viewModel.SelectedProject is not null)
        {
            outlineStore.DeleteBodyFiles(viewModel.SelectedProject.Path, selected);
        }

        ObservableCollection<OutlineItem> collection = selected.Parent?.Children ?? viewModel.OutlineItems;
        collection.Remove(selected);
        SetBodyPreviewMode(false);
        viewModel.SelectedOutlineItem = null;
        viewModel.BodyText = string.Empty;
        ResetBodyUndoHistory();
        RenumberOutline();
        SaveOutline();
    }

    /// <summary>
    /// Selects the right-clicked tree item so context-menu actions target it.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (e.OriginalSource is DependencyObject source
            && FindAncestor<System.Windows.Controls.TreeViewItem>(source) is System.Windows.Controls.TreeViewItem item)
        {
            item.IsSelected = true;
        }
    }

    /// <summary>
    /// Outline drag-and-drop target relationship.
    /// </summary>
    private enum OutlineDrop
    {
        Before,
        After,
        Into
    }

    /// <summary>
    /// Records the potential drag source on left-button-down.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Log.Ins.Debug("시작");
        outlineDragStart = e.GetPosition(null);
        outlineDragItem = e.OriginalSource is DependencyObject source
            && FindAncestor<System.Windows.Controls.TreeViewItem>(source) is System.Windows.Controls.TreeViewItem item
                ? item.DataContext as OutlineItem
                : null;
    }

    /// <summary>
    /// Begins an outline drag once the drag threshold is exceeded.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineTree_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Log.Ins.Debug("시작");
        if (e.LeftButton != MouseButtonState.Pressed || outlineDragItem is null || outlineDragItem.IsEditing)
        {
            return;
        }

        System.Windows.Point current = e.GetPosition(null);

        if (Math.Abs(current.X - outlineDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - outlineDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        OutlineItem dragged = outlineDragItem;
        outlineDragItem = null;
        System.Windows.DragDrop.DoDragDrop(
            (DependencyObject)sender,
            new System.Windows.DataObject(typeof(OutlineItem), dragged),
            System.Windows.DragDropEffects.Move);
    }

    /// <summary>
    /// Shows whether the current drag can be dropped at the hovered location.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineTree_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        // Log.Ins.Debug("시작");
        bool ok = TryResolveOutlineDrop(e, out OutlineItem dragged, out OutlineItem? target, out _)
            && (target is null || !ReferenceEquals(dragged, target));
        e.Effects = ok ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Moves the dragged outline item to the drop location (Shift = make child).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OutlineTree_Drop(object sender, System.Windows.DragEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (!TryResolveOutlineDrop(e, out OutlineItem dragged, out OutlineItem? target, out OutlineDrop pos))
        {
            return;
        }

        if (target is null)
        {
            MoveOutlineToRootEnd(dragged);
        }
        else if (!ReferenceEquals(dragged, target))
        {
            MoveOutlineItem(dragged, target, pos);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Resolves the drop target and relationship; returns false when the drop is invalid.
    /// </summary>
    /// <param name="e">The drag event arguments.</param>
    /// <param name="dragged">The dragged item.</param>
    /// <param name="target">The target item (null = empty area / root).</param>
    /// <param name="pos">The drop relationship.</param>
    /// <returns>True when the drop is allowed.</returns>
    private bool TryResolveOutlineDrop(System.Windows.DragEventArgs e, out OutlineItem dragged, out OutlineItem? target, out OutlineDrop pos)
    {
        Log.Ins.Debug("시작");
        dragged = null!;
        target = null;
        pos = OutlineDrop.After;

        if (!e.Data.GetDataPresent(typeof(OutlineItem)))
        {
            return false;
        }

        dragged = (OutlineItem)e.Data.GetData(typeof(OutlineItem));

        if (e.OriginalSource is DependencyObject source
            && FindAncestor<System.Windows.Controls.TreeViewItem>(source) is System.Windows.Controls.TreeViewItem tvi
            && tvi.DataContext is OutlineItem candidate)
        {
            target = candidate;

            if (IsSelfOrDescendant(dragged, candidate))
            {
                return false;
            }

            bool intoChild = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            double y = e.GetPosition(tvi).Y;
            pos = intoChild ? OutlineDrop.Into : (y < 13 ? OutlineDrop.Before : OutlineDrop.After);
        }

        return true;
    }

    /// <summary>
    /// Moves an item to a sibling position relative to (or as a child of) the target.
    /// </summary>
    /// <param name="dragged">The dragged item.</param>
    /// <param name="target">The target item.</param>
    /// <param name="pos">The drop relationship.</param>
    private void MoveOutlineItem(OutlineItem dragged, OutlineItem target, OutlineDrop pos)
    {
        Log.Ins.Debug("시작");
        ObservableCollection<OutlineItem> source = dragged.Parent?.Children ?? viewModel.OutlineItems;
        source.Remove(dragged);

        if (pos == OutlineDrop.Into)
        {
            dragged.Parent = target;
            target.Children.Add(dragged);
            target.IsExpanded = true;
        }
        else
        {
            ObservableCollection<OutlineItem> destination = target.Parent?.Children ?? viewModel.OutlineItems;
            int index = destination.IndexOf(target);

            if (pos == OutlineDrop.After)
            {
                index++;
            }

            dragged.Parent = target.Parent;
            destination.Insert(index, dragged);
        }

        RenumberOutline();
        SaveOutline();
        dragged.IsSelected = true;
    }

    /// <summary>
    /// Moves an item to the end of the root level (used when dropped on empty space).
    /// </summary>
    /// <param name="dragged">The dragged item.</param>
    private void MoveOutlineToRootEnd(OutlineItem dragged)
    {
        Log.Ins.Debug("시작");
        ObservableCollection<OutlineItem> source = dragged.Parent?.Children ?? viewModel.OutlineItems;

        if (ReferenceEquals(source, viewModel.OutlineItems)
            && viewModel.OutlineItems.IndexOf(dragged) == viewModel.OutlineItems.Count - 1)
        {
            return;
        }

        source.Remove(dragged);
        dragged.Parent = null;
        viewModel.OutlineItems.Add(dragged);

        RenumberOutline();
        SaveOutline();
        dragged.IsSelected = true;
    }

    /// <summary>
    /// Returns true when <paramref name="node"/> is the same as or a descendant of <paramref name="ancestor"/>.
    /// </summary>
    /// <param name="ancestor">The potential ancestor.</param>
    /// <param name="node">The node to test.</param>
    /// <returns>True when node is ancestor or within its subtree.</returns>
    private static bool IsSelfOrDescendant(OutlineItem ancestor, OutlineItem node)
    {
        Log.Ins.Debug("시작");
        if (ReferenceEquals(ancestor, node))
        {
            return true;
        }

        foreach (OutlineItem child in ancestor.Children)
        {
            if (IsSelfOrDescendant(child, node))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Generates a stable outline item id.
    /// </summary>
    /// <returns>The new id.</returns>
    private static string NewOutlineId()
    {
        Log.Ins.Debug("시작");
        return "sec_" + Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// Recomputes chapter/section numbers for the whole outline tree.
    /// </summary>
    private void RenumberOutline()
    {
        Log.Ins.Debug("시작");
        RenumberOutline(viewModel.OutlineItems, string.Empty);
    }

    /// <summary>
    /// Recomputes chapter/section numbers for one level and its descendants.
    /// </summary>
    /// <param name="items">The items at this level.</param>
    /// <param name="prefix">The parent number prefix.</param>
    private static void RenumberOutline(IEnumerable<OutlineItem> items, string prefix)
    {
        Log.Ins.Debug("시작");
        int index = 1;

        foreach (OutlineItem item in items)
        {
            item.Number = prefix.Length == 0 ? index.ToString() : prefix + "." + index;
            RenumberOutline(item.Children, item.Number);
            index++;
        }
    }

    /// <summary>
    /// Finds the nearest ancestor of the given type in the visual tree.
    /// </summary>
    /// <typeparam name="T">The ancestor type.</typeparam>
    /// <param name="node">The starting node.</param>
    /// <returns>The ancestor, or null.</returns>
    private static T? FindAncestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        Log.Ins.Debug("시작");
        DependencyObject? current = node;

        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Loads the selected project's providers into the composer's provider selector.
    /// </summary>
    private void PopulateComposerProviders()
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is null)
        {
            ProviderSelector.ItemsSource = null;
            return;
        }

        ProviderSettingsDocument document = providerSettingsStore.Load(viewModel.SelectedProject.Path);

        suppressProviderChange = true;
        ProviderSelector.ItemsSource = document.Providers.Select(provider => provider.Name).ToList();
        ProviderSelector.SelectedItem = document.DefaultProvider;
        suppressProviderChange = false;
    }

    /// <summary>
    /// Applies the chosen provider as the project's default.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ProviderSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (suppressProviderChange || ProviderSelector.SelectedItem is not string providerName)
        {
            return;
        }

        ChangeDefaultProvider(providerName);
    }

    /// <summary>
    /// Changes the project's default provider.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    private void ChangeDefaultProvider(string providerName)
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        try
        {
            string projectRoot = viewModel.SelectedProject.Path;
            ProviderSettingsDocument document = providerSettingsStore.Load(projectRoot);
            document.DefaultProvider = providerName;
            providerSettingsStore.Save(projectRoot, document);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "프로바이더 변경 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Sends a chat message to the project's provider and streams the response.
    /// </summary>
    /// <param name="text">The user message.</param>
    /// <returns>A task that completes when the chat finishes.</returns>
    private async Task SendMessageAsync(string text)
    {
        Log.Ins.Debug("시작");
        if (isSending || viewModel.SelectedProject is null)
        {
            return;
        }

        isSending = true;
        string projectRoot = viewModel.SelectedProject.Path;

        ConversationListItem? row = null;
        DateTimeOffset sentAt = DateTimeOffset.Now;

        try
        {
            ChatProviderResponse response = await chatService.StreamAsync(
                projectRoot,
                text,
                preview => RunOnUi(() => row = AddConversationRow(preview.RequestId, text, sentAt)),
                chunk => RunOnUi(() => AppendAssistantChunk(row, chunk)),
                CancellationToken.None,
                onAgentStep: label => RunOnUi(() => OnChatAgentStep(row, label)));

            

            RunOnUi(() => FinalizeConversationRow(row, response));
            SaveResponseArtifacts(projectRoot, response);
            RunOnUi(() => ReloadArtifacts(projectRoot));
            RunOnUi(ShowBodyProposalIfAny);
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
        Log.Ins.Debug("시작");
        ConversationListItem row = new ConversationListItem
        {
            RequestId = requestId,
            ImportanceWeight = 0,
            CreatedAtText = ConversationLogReader.FormatTimestamp(sentAt),
            UserContent = userText,
            AssistantContent = string.Empty,
            StatusText = "답변중"
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
        Log.Ins.Debug("시작");
        if (row is null)
        {
            return;
        }

        row.AssistantContent += chunk;
        ConversationGrid.ScrollIntoView(row);
    }

    /// <summary>
    /// Handles an agent step (tool discovery or tool run) by replacing the streamed protocol JSON with a notice.
    /// </summary>
    /// <param name="row">The conversation row.</param>
    /// <param name="label">The step label.</param>
    private void OnChatAgentStep(ConversationListItem? row, string label)
    {
        Log.Ins.Debug("시작");
        if (row is null)
        {
            return;
        }

        // Clear the streamed protocol JSON and show a short notice; the final answer streams in next.
        row.AssistantContent = $"[{label}]" + Environment.NewLine;
        ConversationGrid.ScrollIntoView(row);
    }

    /// <summary>
    /// Finalizes a conversation row with parsed content and importance.
    /// </summary>
    /// <param name="row">The conversation row.</param>
    /// <param name="response">The completed chat response.</param>
    /// <summary>
    /// If the model proposed a body revision during the turn, shows the before/after window and applies
    /// the change on approval (then saves). The body is only ever changed with user confirmation.
    /// </summary>
    private void ShowBodyProposalIfAny()
    {
        Log.Ins.Debug("시작");
        string? proposed = bodyBridge.TakePendingProposal();

        if (proposed is null || viewModel.SelectedOutlineItem is null)
        {
            return;
        }

        if (BodyDiffDialog.Confirm(viewModel.BodyText, proposed, this))
        {
            // Return to the editor so the applied revision is immediately visible/editable (DEC-084).
            SetBodyPreviewMode(false);
            viewModel.BodyText = proposed;
            SaveCurrentBody();
        }
    }

    /// <summary>
    /// Debug helper: opens the body before/after dialog with sample text so its layout can be
    /// inspected without driving the LLM. Uses the current body as "before" when one is loaded.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void DebugBodyDiffMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        string before = string.IsNullOrEmpty(viewModel.BodyText)
            ? "이전 본문 샘플입니다.\n첫 번째 문단.\n두 번째 문단."
            : viewModel.BodyText;
        string after = before + "\n\n[디버그] 변경후 미리보기 — 이 줄이 수정안에 추가되었습니다.";
        BodyDiffDialog.Confirm(before, after, this);
    }

    private void FinalizeConversationRow(ConversationListItem? row, ChatProviderResponse response)
    {
        Log.Ins.Debug("시작");
        if (row is null)
        {
            return;
        }

        // Artifact blocks are separated out and replaced with their extracted name.
        row.AssistantContent = ArtifactTagParser.ReplaceArtifactsWithTitles(response.Content);
        row.ImportanceWeight = response.ImportanceWeight;

        // An empty final answer means the model returned nothing (e.g. after tool use); surface it explicitly.
        row.StatusText = string.IsNullOrWhiteSpace(response.Content) ? "답변완료 · 빈 응답" : "답변완료";
        ConversationGrid.ScrollIntoView(row);
    }

    /// <summary>
    /// Saves artifacts found in a response and adds them to the project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="response">The completed chat response.</param>
    private void SaveResponseArtifacts(string projectRoot, ChatProviderResponse response)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        viewModel.Artifacts.Clear();

        foreach (Artifact artifact in artifactService.List(projectRoot))
        {
            viewModel.Artifacts.Add(ToArtifactListItem(projectRoot, artifact));
        }
    }

    /// <summary>
    /// Builds a list item for an artifact, resolving the absolute image path for image artifacts.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifact">The stored artifact.</param>
    /// <returns>The artifact list item.</returns>
    private ArtifactListItem ToArtifactListItem(string projectRoot, Artifact artifact)
    {
        Log.Ins.Debug("시작");
        string? imagePath = ArtifactService.IsImage(artifact)
            ? artifactService.GetContentFullPath(projectRoot, artifact)
            : null;
        return new ArtifactListItem(artifact, imagePath);
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
        Log.Ins.Debug("시작");
        if (row is null)
        {
            ComposerInput.Text = text;
        }
        else
        {
            row.StatusText = "실패";
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
    /// Deletes the conversation row after user confirmation.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void DeleteConversationButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (sender is not FrameworkElement element || element.DataContext is not ConversationListItem row)
        {
            return;
        }

        if (viewModel.SelectedProject is null || string.IsNullOrWhiteSpace(row.RequestId))
        {
            return;
        }

        MessageBoxResult result = System.Windows.MessageBox.Show(
            this,
            $"이 대화(ID {row.RequestId})를 삭제하시겠습니까? 대화 로그에서 제거됩니다.",
            "대화 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            conversationLogStore.DeleteConversation(viewModel.SelectedProject.Path, row.RequestId);
            viewModel.Conversations.Remove(row);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "대화 삭제 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Extracts the dragged selection from a conversation response into a new artifact.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ExtractArtifactMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (sender is not System.Windows.Controls.MenuItem menuItem
            || menuItem.Parent is not System.Windows.Controls.ContextMenu contextMenu
            || contextMenu.PlacementTarget is not System.Windows.Controls.RichTextBox richTextBox
            || richTextBox.DataContext is not ConversationListItem row)
        {
            return;
        }

        if (viewModel.SelectedProject is null)
        {
            return;
        }

        string selection = richTextBox.Selection.Text;

        if (string.IsNullOrWhiteSpace(selection))
        {
            System.Windows.MessageBox.Show(
                this,
                "추출할 영역을 드래그하여 선택하세요.",
                "아티팩트 추출",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Map the rendered selection back to the original Markdown source span so the artifact keeps
        // its Markdown (line breaks, formatting) and the right region can be replaced.
        (int Start, int Length)? span = MarkdownSelectionMatcher.FindSourceSpan(row.AssistantContent, selection);
        string initialContent = span is { } found
            ? row.AssistantContent.Substring(found.Start, found.Length)
            : selection;

        ArtifactEditDialog dialog = new ArtifactEditDialog(row.RequestId, initialContent)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            string projectRoot = viewModel.SelectedProject.Path;
            artifactService.Save(
                projectRoot,
                new ArtifactCandidate
                {
                    Title = dialog.ArtifactTitle,
                    Type = dialog.ArtifactType,
                    Content = dialog.ArtifactContent
                },
                row.RequestId);
            ReloadArtifacts(projectRoot);

            if (span is { } target)
            {
                ReplaceSourceSpanWithArtifactMarker(projectRoot, row, target.Start, target.Length, dialog.ArtifactTitle);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    this,
                    "아티팩트는 저장되었지만, 선택 영역을 본문에서 찾지 못해 치환하지 못했습니다.",
                    "아티팩트 추출",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "아티팩트 추출 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Replaces a source span in the conversation response with the artifact name marker, then persists it.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="row">The conversation row.</param>
    /// <param name="start">The source span start index.</param>
    /// <param name="length">The source span length.</param>
    /// <param name="artifactTitle">The extracted artifact title.</param>
    private void ReplaceSourceSpanWithArtifactMarker(
        string projectRoot,
        ConversationListItem row,
        int start,
        int length,
        string artifactTitle)
    {
        Log.Ins.Debug("시작");
        string content = row.AssistantContent;

        if (start < 0 || length <= 0 || start + length > content.Length)
        {
            return;
        }

        string marker = $"[아티팩트: {artifactTitle}]";
        string updated = content.Remove(start, length).Insert(start, marker);

        conversationLogStore.UpdateAssistantMessageContent(projectRoot, row.RequestId, updated);
        row.AssistantContent = updated;
    }

    /// <summary>
    /// Opens the artifact viewer for a clicked artifact.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ArtifactItem_Click(object sender, MouseButtonEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (sender is not FrameworkElement element || element.DataContext is not ArtifactListItem item)
        {
            return;
        }

        if (viewModel.SelectedProject is null)
        {
            return;
        }

        // Image artifacts open in an image viewer, not the text artifact viewer.
        if (item.IsImage && item.ImagePath is not null)
        {
            ImageViewerWindow.Show(item.Title, item.ImagePath, this);
            return;
        }

        try
        {
            string projectRoot = viewModel.SelectedProject.Path;
            Artifact artifact = artifactService.Get(projectRoot, item.ArtifactId);
            string content = artifactService.ReadContent(projectRoot, artifact);
            ArtifactViewer viewer = new ArtifactViewer(item.Title, content, artifact.Type, artifact.TargetPath)
            {
                Owner = this
            };
            viewer.ShowDialog();

            if (viewer.DeleteRequested)
            {
                artifactService.Remove(projectRoot, artifact.ArtifactId);
                ReloadArtifacts(projectRoot);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "아티팩트 뷰어 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>The drag format that carries an artifact id from a card to the body editor.</summary>
    private const string ArtifactDragFormat = "LlmIdeArtifactId";

    /// <summary>The image file extensions accepted as image artifacts.</summary>
    private static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico"];

    private System.Windows.Point artifactDragStartPoint;
    private ArtifactListItem? artifactDragCandidate;

    /// <summary>
    /// Shows a copy cursor when image files are dragged over the artifact area.
    /// </summary>
    private void ArtifactArea_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        // Log.Ins.Debug("시작");
        e.Effects = viewModel.SelectedProject is not null && HasImageFiles(e.Data)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Adds dropped image files as image artifacts.
    /// </summary>
    private void ArtifactArea_Drop(object sender, System.Windows.DragEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is null || !e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        string projectRoot = viewModel.SelectedProject.Path;
        string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        int added = 0;

        try
        {
            foreach (string file in files)
            {
                if (IsImageFile(file))
                {
                    artifactService.SaveImage(projectRoot, string.Empty, file);
                    added++;
                }
            }

            if (added > 0)
            {
                ReloadArtifacts(projectRoot);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "이미지 추가 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Records the potential start of an artifact-card drag.
    /// </summary>
    private void ArtifactCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Log.Ins.Debug("시작");
        artifactDragStartPoint = e.GetPosition(null);
        artifactDragCandidate = (sender as FrameworkElement)?.DataContext as ArtifactListItem;
    }

    /// <summary>
    /// Starts dragging an artifact card once the pointer moves past the drag threshold.
    /// </summary>
    private void ArtifactCard_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Log.Ins.Debug("시작");
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || artifactDragCandidate is null)
        {
            return;
        }

        System.Windows.Point current = e.GetPosition(null);

        if (Math.Abs(current.X - artifactDragStartPoint.X) < System.Windows.SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - artifactDragStartPoint.Y) < System.Windows.SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        // The drag carries the artifact id; the body editor turns it into a markdown reference on drop.
        System.Windows.DataObject data = new System.Windows.DataObject(ArtifactDragFormat, artifactDragCandidate.ArtifactId);
        artifactDragCandidate = null;
        System.Windows.DragDrop.DoDragDrop((System.Windows.DependencyObject)sender, data, System.Windows.DragDropEffects.Copy);
    }

    /// <summary>
    /// Shows a copy cursor when an artifact is dragged over the editable body.
    /// </summary>
    private void BodyEditor_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        // Log.Ins.Debug("시작");
        if (e.Data.GetDataPresent(ArtifactDragFormat) && viewModel.IsBodyEditable)
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Inserts the dropped artifact at the drop position in the body: an image reference tag for
    /// image artifacts, or the artifact's text content itself for text artifacts (DEC-087).
    /// </summary>
    private void BodyEditor_Drop(object sender, System.Windows.DragEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (!e.Data.GetDataPresent(ArtifactDragFormat)
            || viewModel.SelectedProject is null
            || sender is not System.Windows.Controls.TextBox editor)
        {
            return;
        }

        string artifactId = (string)e.Data.GetData(ArtifactDragFormat);
        string projectRoot = viewModel.SelectedProject.Path;

        try
        {
            Artifact artifact = artifactService.Get(projectRoot, artifactId);
            string insertion = BuildArtifactInsertion(projectRoot, artifact);

            // Insert at the drop position; fall back to the caret when the point maps nowhere.
            int index = editor.GetCharacterIndexFromPoint(e.GetPosition(editor), true);

            if (index < 0)
            {
                index = editor.CaretIndex;
            }

            string body = editor.Text ?? string.Empty;
            index = Math.Clamp(index, 0, body.Length);

            // 드롭 지점이 기존 마크다운 이미지/링크 태그 내부면 태그가 쪼개지지 않도록
            // 태그 끝으로 보정하고, 빈 줄(엔터 2개)로 구분해 별도 문단으로 삽입한다.
            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(body, @"!?\[[^\]]*\]\([^)]*\)"))
            {
                if (index > match.Index && index < match.Index + match.Length)
                {
                    index = match.Index + match.Length;
                    insertion = "\n\n" + insertion;
                    break;
                }
            }

            // 편집기 경유로 삽입해 실행취소(Ctrl+Z) 단위로 기록한다 (DEC-088).
            // 양방향 바인딩이 BodyText를 자동 갱신한다.
            editor.Select(index, 0);
            editor.SelectedText = insertion;
            editor.CaretIndex = index + insertion.Length;

            // 드롭 직후 바로 Ctrl+Z/Ctrl+Y가 듣도록 포커스를 편집기로 옮긴다.
            editor.Focus();
            e.Handled = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "본문 삽입 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Builds the body insertion for a dropped artifact: a project-relative markdown image
    /// reference for images (rendered by the preview, DEC-081), or the artifact's text content
    /// for text artifacts (DEC-087 — links crashed the preview and were not the writing intent).
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="artifact">The dropped artifact.</param>
    /// <returns>The text to insert into the body.</returns>
    private string BuildArtifactInsertion(string projectRoot, Artifact artifact)
    {
        Log.Ins.Debug("시작");
        if (ArtifactService.IsImage(artifact))
        {
            // Body files live in the project root; reference the image via its project-relative path.
            string relative = $"{LlmIdeLayout.MetadataDirectoryName}/{LlmIdeLayout.ArtifactsDirectoryName}/{artifact.ContentPath}";
            return $"![{artifact.Title}]({relative})";
        }

        return artifactService.ReadContent(projectRoot, artifact);
    }

    /// <summary>
    /// Toggles the body area between the text editor and the in-place markdown preview
    /// ([미리보기] ↔ [수정], DEC-084).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void BodyPreviewToggle_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (viewModel.SelectedProject is null)
        {
            return;
        }

        SetBodyPreviewMode(BodyPreviewView.Visibility != Visibility.Visible);
    }

    /// <summary>
    /// Undoes the last body editor change ([←] 버튼, Ctrl+Z와 동일, DEC-088).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void BodyUndo_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        BodyEditor.Undo();
    }

    /// <summary>
    /// Redoes the last undone body editor change ([→] 버튼, Ctrl+Y와 동일, DEC-088).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void BodyRedo_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        BodyEditor.Redo();
    }

    /// <summary>
    /// Clears the body editor's undo/redo stack. Called on section switch, project change, and
    /// outline deletion so undo can never resurrect another document's text (DEC-088).
    /// </summary>
    private void ResetBodyUndoHistory()
    {
        Log.Ins.Debug("시작");
        BodyEditor.IsUndoEnabled = false;
        BodyEditor.IsUndoEnabled = true;
    }

    /// <summary>
    /// Shows either the rendered markdown preview (true) or the text editor (false) in the body
    /// area, and flips the toggle button caption to the opposite action. The preview is a snapshot
    /// of the editor content at switch time (relative image paths resolved against the project root).
    /// </summary>
    /// <param name="preview">True to show the rendered preview, false to show the editor.</param>
    private void SetBodyPreviewMode(bool preview)
    {
        Log.Ins.Debug("시작");
        if (preview)
        {
            string projectRoot = viewModel.SelectedProject?.Path ?? string.Empty;
            MarkdownText.SetText(BodyPreviewView, BodyMarkdownPreview.RewriteImagePaths(viewModel.BodyText ?? string.Empty, projectRoot));
        }

        BodyPreviewView.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
        BodyEditor.Visibility = preview ? Visibility.Collapsed : Visibility.Visible;
        BodyPreviewToggleButton.Content = preview ? "수정" : "미리보기";
    }

    /// <summary>
    /// Determines whether a drag payload contains at least one image file.
    /// </summary>
    private static bool HasImageFiles(System.Windows.IDataObject data)
    {
        Log.Ins.Debug("시작");
        return data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            && ((string[])data.GetData(System.Windows.DataFormats.FileDrop)).Any(IsImageFile);
    }

    /// <summary>
    /// Determines whether a file path has an accepted image extension.
    /// </summary>
    private static bool IsImageFile(string path)
    {
        Log.Ins.Debug("시작");
        return ImageExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());
    }

    /// <summary>
    /// Runs an action on the UI thread.
    /// </summary>
    /// <param name="action">The action to run.</param>
    private void RunOnUi(Action action)
    {
        Log.Ins.Debug("시작");
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
    /// A value indicating whether importance editing is allowed.
    /// </summary>
    private bool isImportanceEditable = true;

    /// <summary>
    /// Occurs when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets a value indicating whether importance editing is allowed.
    /// </summary>
    public bool IsImportanceEditable
    {
        get
        {
            return isImportanceEditable;
        }

        set
        {
            if (isImportanceEditable == value)
            {
                return;
            }

            isImportanceEditable = value;
            OnPropertyChanged();
        }
    }

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
    /// Gets the document outline (목차) tree.
    /// </summary>
    public ObservableCollection<OutlineItem> OutlineItems { get; } = [];

    private OutlineItem? selectedOutlineItem;

    /// <summary>
    /// Gets or sets the selected outline item. Body editing is only allowed while an item is selected.
    /// </summary>
    public OutlineItem? SelectedOutlineItem
    {
        get
        {
            return selectedOutlineItem;
        }

        set
        {
            if (ReferenceEquals(selectedOutlineItem, value))
            {
                return;
            }

            selectedOutlineItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBodyEditable));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the body editor accepts input (an outline item is selected).
    /// </summary>
    public bool IsBodyEditable
    {
        get
        {
            return selectedOutlineItem is not null;
        }
    }

    private string bodyText = string.Empty;

    /// <summary>
    /// Gets or sets the body text of the selected outline item.
    /// </summary>
    public string BodyText
    {
        get
        {
            return bodyText;
        }

        set
        {
            if (bodyText == value)
            {
                return;
            }

            bodyText = value;
            OnPropertyChanged();
        }
    }

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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
    /// The response status text ("답변중" while streaming, "답변완료" when done).
    /// </summary>
    private string statusText = "답변완료";

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
    /// Gets or sets the response status text shown in the LLM response column ("답변중" / "답변완료").
    /// </summary>
    public string StatusText
    {
        get
        {
            return statusText;
        }

        set
        {
            if (statusText == value)
            {
                return;
            }

            statusText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Raises the property changed event.
    /// </summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        Log.Ins.Debug("시작");
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
    /// <param name="imagePath">The absolute image path for image artifacts; null for text artifacts.</param>
    public ArtifactListItem(Artifact artifact, string? imagePath = null)
    {
        Log.Ins.Debug("시작");
        ArtifactId = artifact.ArtifactId;
        Title = artifact.Title;
        Type = artifact.Type;
        SourceRequestId = artifact.SourceRequestId;
        ImagePath = imagePath;
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

    /// <summary>
    /// Gets the absolute image path for image artifacts (null for text artifacts).
    /// </summary>
    public string? ImagePath { get; }

    /// <summary>
    /// Gets a value indicating whether this is an image artifact (card shows a thumbnail).
    /// </summary>
    public bool IsImage => ImagePath is not null;

    /// <summary>
    /// Gets a value indicating whether this is a text artifact (card shows source metadata).
    /// </summary>
    public bool IsTextArtifact => ImagePath is null;
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        return value.ToLocalTime().ToString("[yyyy-MM-dd HH:mm:ss.fff]");
    }
}
