using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using LlmIde.Infrastructure.Conversations;
using LlmIde.Infrastructure.Json;
using LlmIde.Infrastructure.Projects;
using LlmIde.Infrastructure.Providers;
using System.Text.Json;

namespace LlmIde.Cli;

/// <summary>
/// Provides the LLM IDE command-line entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the command-line application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static int Main(string[] args)
    {
        string ideProgramRoot = AppContext.BaseDirectory;
        IProjectStore projectStore = new JsonFileProjectStore();
        IProviderSettingsStore providerSettingsStore = new JsonProviderSettingsStore();
        CriteriaService criteriaService = new CriteriaService(new JsonCriteriaStore());
        ProjectStateService projectStateService = new ProjectStateService(new JsonProjectStateStore());
        DeepSeekChatProvider deepSeekProvider = new DeepSeekChatProvider(new HttpClient());
        ProjectRegistryService projectRegistryService = new ProjectRegistryService(new JsonProjectRegistryStore(ideProgramRoot));
        ProjectInitializer projectInitializer = new ProjectInitializer(projectStore, projectRegistryService, ideProgramRoot);
        ChatService chatService = new ChatService(
            providerSettingsStore,
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = deepSeekProvider
            },
            new CompositeConversationLogStore(
            [
                new JsonlConversationLogStore(),
                new SqliteConversationLogStore()
            ]),
            criteriaService,
            projectStateService,
            new FileRollingContextStore());
        ProviderSettingsService providerSettingsService = new ProviderSettingsService(
            providerSettingsStore,
            new Dictionary<string, IModelProvider>
            {
                ["deepseek"] = deepSeekProvider
            });
        CliApplication application = new CliApplication(
            projectStore,
            projectInitializer,
            projectRegistryService,
            chatService,
            providerSettingsStore,
            providerSettingsService,
            criteriaService,
            projectStateService);

        return application.Run(args);
    }
}

/// <summary>
/// Handles LLM IDE command-line commands.
/// </summary>
public sealed class CliApplication
{
    /// <summary>
    /// The project metadata store.
    /// </summary>
    private readonly IProjectStore projectStore;

    /// <summary>
    /// The project initializer.
    /// </summary>
    private readonly ProjectInitializer projectInitializer;

    /// <summary>
    /// The project registry service.
    /// </summary>
    private readonly ProjectRegistryService projectRegistryService;

    /// <summary>
    /// The chat service.
    /// </summary>
    private readonly ChatService chatService;

    /// <summary>
    /// The provider settings store.
    /// </summary>
    private readonly IProviderSettingsStore providerSettingsStore;

    /// <summary>
    /// The provider settings service.
    /// </summary>
    private readonly ProviderSettingsService providerSettingsService;

    /// <summary>
    /// The criteria service.
    /// </summary>
    private readonly CriteriaService criteriaService;

    /// <summary>
    /// The project state service.
    /// </summary>
    private readonly ProjectStateService projectStateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliApplication"/> class.
    /// </summary>
    /// <param name="projectStore">The project metadata store.</param>
    /// <param name="projectInitializer">The project initializer.</param>
    /// <param name="projectRegistryService">The project registry service.</param>
    /// <param name="chatService">The chat service.</param>
    /// <param name="providerSettingsStore">The provider settings store.</param>
    /// <param name="providerSettingsService">The provider settings service.</param>
    /// <param name="criteriaService">The criteria service.</param>
    /// <param name="projectStateService">The project state service.</param>
    public CliApplication(
        IProjectStore projectStore,
        ProjectInitializer projectInitializer,
        ProjectRegistryService projectRegistryService,
        ChatService chatService,
        IProviderSettingsStore providerSettingsStore,
        ProviderSettingsService providerSettingsService,
        CriteriaService criteriaService,
        ProjectStateService projectStateService)
    {
        this.projectStore = projectStore;
        this.projectInitializer = projectInitializer;
        this.projectRegistryService = projectRegistryService;
        this.chatService = chatService;
        this.providerSettingsStore = providerSettingsStore;
        this.providerSettingsService = providerSettingsService;
        this.criteriaService = criteriaService;
        this.projectStateService = projectStateService;
    }

    /// <summary>
    /// Runs a command.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        try
        {
            return RunCore(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Dispatches a command after common error handling is installed.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCore(string[] args)
    {
        string command = args[0].Trim().ToLowerInvariant();

        if (command == "init")
        {
            return RunInit(args);
        }

        if (command == "projects")
        {
            return RunProjects(args);
        }

        if (command == "chat")
        {
            return RunChat(args);
        }

        if (command == "models")
        {
            return RunModels(args);
        }

        if (command == "criteria")
        {
            return RunCriteria(args);
        }

        if (command == "state")
        {
            return RunState(args);
        }

        PrintUsage();
        return 1;
    }

    /// <summary>
    /// Runs the init command.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunInit(string[] args)
    {
        ProjectInitializationRequest request = ParseInitRequest(args);
        ProjectInitializationResult result = projectInitializer.Initialize(request);
        string action = result.Created ? "initialized" : "opened";

        Console.WriteLine($"{action}: {result.ProjectRoot}");
        Console.WriteLine($"project: {result.ProjectInfo.Title}");
        return 0;
    }

    /// <summary>
    /// Runs the chat command.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunChat(string[] args)
    {
        if (args.Length < 3)
        {
            PrintChatUsage();
            return 1;
        }

        string projectName = args[1];
        bool debug = args.Contains("--debug", StringComparer.Ordinal);
        bool noStream = args.Contains("--no-stream", StringComparer.Ordinal);
        string message = string.Join(' ', args.Skip(2).Where(IsChatMessagePart));

        if (string.IsNullOrWhiteSpace(message))
        {
            PrintChatUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(projectName);

        if (noStream)
        {
            ChatProviderResponse response = chatService.SendAsync(project.Path, message, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (debug)
            {
                PrintChatDebug(response);
            }

            Console.WriteLine(response.Content);
            return 0;
        }

        chatService.StreamAsync(
            project.Path,
            message,
            debug ? PrintChatDebug : null,
            Console.Write,
            CancellationToken.None).GetAwaiter().GetResult();
        Console.WriteLine();
        return 0;
    }

    /// <summary>
    /// Determines whether an argument belongs to the user chat message.
    /// </summary>
    /// <param name="arg">The argument.</param>
    /// <returns>True when the argument is part of the user message.</returns>
    private static bool IsChatMessagePart(string arg)
    {
        return !string.Equals(arg, "--debug", StringComparison.Ordinal)
            && !string.Equals(arg, "--no-stream", StringComparison.Ordinal);
    }

    /// <summary>
    /// Prints the provider request used by a chat command.
    /// </summary>
    /// <param name="response">The chat response.</param>
    private static void PrintChatDebug(ChatProviderResponse response)
    {
        ChatDebugView debugView = new ChatDebugView
        {
            RequestId = response.RequestId,
            Provider = response.Provider,
            Model = response.SentRequest?.Model ?? response.Model,
            Messages = response.SentRequest?.Messages ?? []
        };
        Console.WriteLine("debug:");
        Console.WriteLine(JsonSerializer.Serialize(debugView, JsonOptions.Default));
        Console.WriteLine("response:");
    }

    /// <summary>
    /// Runs the models command group.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunModels(string[] args)
    {
        if (args.Length < 3)
        {
            PrintModelsUsage();
            return 1;
        }

        string subCommand = args[1].Trim().ToLowerInvariant();

        if (subCommand == "list")
        {
            return RunModelsList(args);
        }

        if (subCommand == "set")
        {
            return RunModelsSet(args);
        }

        PrintModelsUsage();
        return 1;
    }

    /// <summary>
    /// Lists available models for a project's default provider.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunModelsList(string[] args)
    {
        if (args.Length != 3)
        {
            PrintModelsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        IReadOnlyList<ProviderModel> models = providerSettingsService.ListModelsAsync(project.Path, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        foreach (ProviderModel model in models)
        {
            Console.WriteLine(model.Id);
        }

        return 0;
    }

    /// <summary>
    /// Sets the default model for a project's default provider.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunModelsSet(string[] args)
    {
        if (args.Length != 4)
        {
            PrintModelsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        providerSettingsService.SetDefaultModel(project.Path, args[3]);
        Console.WriteLine($"model: {args[3]}");
        return 0;
    }

    /// <summary>
    /// Runs the criteria command group.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCriteria(string[] args)
    {
        if (args.Length < 3)
        {
            PrintCriteriaUsage();
            return 1;
        }

        string subCommand = args[1].Trim().ToLowerInvariant();

        if (subCommand == "list")
        {
            return RunCriteriaList(args);
        }

        if (subCommand == "add")
        {
            return RunCriteriaAdd(args);
        }

        if (subCommand == "update")
        {
            return RunCriteriaUpdate(args);
        }

        if (subCommand == "remove")
        {
            return RunCriteriaRemove(args);
        }

        if (subCommand == "activate")
        {
            return RunCriteriaActivate(args);
        }

        if (subCommand == "deactivate")
        {
            return RunCriteriaDeactivate(args);
        }

        PrintCriteriaUsage();
        return 1;
    }

    /// <summary>
    /// Lists criteria.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCriteriaList(string[] args)
    {
        if (args.Length != 3)
        {
            PrintCriteriaUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        IReadOnlyList<Criterion> criteria = criteriaService.List(project.Path);

        foreach (Criterion criterion in criteria)
        {
            Console.WriteLine($"{criterion.CriterionId}  {criterion.Status}  {criterion.Priority}  {criterion.Title}");

            if (!string.IsNullOrWhiteSpace(criterion.Description))
            {
                Console.WriteLine($"  {criterion.Description}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Adds a criterion.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCriteriaAdd(string[] args)
    {
        if (args.Length < 5)
        {
            PrintCriteriaUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        CriteriaOptions options = ParseCriteriaOptions(args.Skip(3).ToArray());
        Criterion criterion = criteriaService.Add(
            project.Path,
            options.Title ?? string.Empty,
            options.Description ?? string.Empty,
            options.Priority ?? "normal");
        Console.WriteLine($"criterion: {criterion.CriterionId}");
        return 0;
    }

    /// <summary>
    /// Updates a criterion.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCriteriaUpdate(string[] args)
    {
        if (args.Length < 5)
        {
            PrintCriteriaUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        string criterionId = args[3];
        CriteriaOptions options = ParseCriteriaOptions(args.Skip(4).ToArray());
        criteriaService.Update(project.Path, criterionId, options.Title, options.Description, options.Priority);
        Console.WriteLine($"updated: {criterionId}");
        return 0;
    }

    /// <summary>
    /// Removes a criterion.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCriteriaRemove(string[] args)
    {
        if (args.Length != 4)
        {
            PrintCriteriaUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        bool removed = criteriaService.Remove(project.Path, args[3]);
        Console.WriteLine(removed ? "removed" : "not found");
        return removed ? 0 : 1;
    }

    /// <summary>
    /// Activates a criterion.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCriteriaActivate(string[] args)
    {
        if (args.Length != 4)
        {
            PrintCriteriaUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        criteriaService.Activate(project.Path, args[3]);
        Console.WriteLine($"activated: {args[3]}");
        return 0;
    }

    /// <summary>
    /// Deactivates a criterion.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunCriteriaDeactivate(string[] args)
    {
        if (args.Length != 4)
        {
            PrintCriteriaUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        criteriaService.Deactivate(project.Path, args[3]);
        Console.WriteLine($"deactivated: {args[3]}");
        return 0;
    }

    /// <summary>
    /// Parses criteria options.
    /// </summary>
    /// <param name="args">The criteria option arguments.</param>
    /// <returns>The parsed options.</returns>
    private static CriteriaOptions ParseCriteriaOptions(string[] args)
    {
        CriteriaOptions options = new CriteriaOptions();
        int index = 0;

        while (index < args.Length)
        {
            string option = args[index];

            if (option == "--title")
            {
                options.Title = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            if (option == "--description")
            {
                options.Description = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            if (option == "--priority")
            {
                options.Priority = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            throw new InvalidOperationException($"Unknown criteria option: {option}");
        }

        return options;
    }

    /// <summary>
    /// Runs the state command group.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunState(string[] args)
    {
        if (args.Length < 3)
        {
            PrintStateUsage();
            return 1;
        }

        string subCommand = args[1].Trim().ToLowerInvariant();

        if (subCommand == "show")
        {
            return RunStateShow(args);
        }

        if (subCommand == "set")
        {
            return RunStateSet(args);
        }

        if (subCommand == "add")
        {
            return RunStateAdd(args);
        }

        if (subCommand == "remove")
        {
            return RunStateRemove(args);
        }

        PrintStateUsage();
        return 1;
    }

    /// <summary>
    /// Shows project state.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunStateShow(string[] args)
    {
        if (args.Length != 3)
        {
            PrintStateUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        ProjectState state = projectStateService.Get(project.Path);
        PrintProjectState(state);
        return 0;
    }

    /// <summary>
    /// Updates project state scalar fields.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunStateSet(string[] args)
    {
        if (args.Length < 5)
        {
            PrintStateUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        ProjectStateSetOptions options = ParseProjectStateSetOptions(args.Skip(3).ToArray());
        ProjectState state = projectStateService.Set(
            project.Path,
            options.Stage,
            options.CurrentTask,
            options.LastDecision);
        PrintProjectState(state);
        return 0;
    }

    /// <summary>
    /// Adds an item to a project state list.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunStateAdd(string[] args)
    {
        if (args.Length < 5)
        {
            PrintStateUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        string listName = args[3].Trim().ToLowerInvariant();
        string item = string.Join(' ', args.Skip(4));
        projectStateService.AddItem(project.Path, listName, item);
        Console.WriteLine($"added: {listName}");
        return 0;
    }

    /// <summary>
    /// Removes an item from a project state list.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunStateRemove(string[] args)
    {
        if (args.Length < 5)
        {
            PrintStateUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        string listName = args[3].Trim().ToLowerInvariant();
        string item = string.Join(' ', args.Skip(4));
        bool removed = projectStateService.RemoveItem(project.Path, listName, item);
        Console.WriteLine(removed ? $"removed: {listName}" : "not found");
        return removed ? 0 : 1;
    }

    /// <summary>
    /// Parses project state set options.
    /// </summary>
    /// <param name="args">The option arguments.</param>
    /// <returns>The parsed options.</returns>
    private static ProjectStateSetOptions ParseProjectStateSetOptions(string[] args)
    {
        ProjectStateSetOptions options = new ProjectStateSetOptions();
        int index = 0;

        while (index < args.Length)
        {
            string option = args[index];

            if (option == "--stage")
            {
                options.Stage = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            if (option == "--current-task")
            {
                options.CurrentTask = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            if (option == "--last-decision")
            {
                options.LastDecision = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            throw new InvalidOperationException($"Unknown state option: {option}");
        }

        if (!options.HasAnyValue)
        {
            throw new InvalidOperationException("At least one state option is required.");
        }

        return options;
    }

    /// <summary>
    /// Prints project state as JSON.
    /// </summary>
    /// <param name="state">The project state.</param>
    private static void PrintProjectState(ProjectState state)
    {
        Console.WriteLine(JsonSerializer.Serialize(state, JsonOptions.Default));
    }

    /// <summary>
    /// Runs the projects command group.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjects(string[] args)
    {
        if (args.Length < 2)
        {
            PrintProjectsUsage();
            return 1;
        }

        string subCommand = args[1].Trim().ToLowerInvariant();

        if (subCommand == "list")
        {
            return RunProjectsList();
        }

        if (subCommand == "open")
        {
            return RunProjectsOpen(args);
        }

        if (subCommand == "remove")
        {
            return RunProjectsRemove(args);
        }

        if (subCommand == "delete")
        {
            return RunProjectsDelete(args);
        }

        if (subCommand == "rename")
        {
            return RunProjectsRename(args);
        }

        if (subCommand == "move")
        {
            return RunProjectsMove(args);
        }

        PrintProjectsUsage();
        return 1;
    }

    /// <summary>
    /// Lists registered projects.
    /// </summary>
    /// <returns>The process exit code.</returns>
    private int RunProjectsList()
    {
        IReadOnlyList<ProjectRegistryEntry> projects = projectRegistryService.List();

        if (projects.Count == 0)
        {
            Console.WriteLine("No projects.");
            return 0;
        }

        for (int index = 0; index < projects.Count; index++)
        {
            ProjectRegistryEntry project = projects[index];
            Console.WriteLine($"{index + 1}. {project.Name}  {project.Path}  {project.CreatedAt:O}");
        }

        return 0;
    }

    /// <summary>
    /// Opens an existing project.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjectsOpen(string[] args)
    {
        if (args.Length != 3)
        {
            PrintProjectsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        return OpenProjectPath(project.Path);
    }

    /// <summary>
    /// Opens a project path from the registry.
    /// </summary>
    /// <param name="path">The project root path.</param>
    /// <returns>The process exit code.</returns>
    private int OpenProjectPath(string path)
    {
        string normalizedPath = Path.GetFullPath(path);

        if (!projectStore.IsInitialized(normalizedPath))
        {
            Console.Error.WriteLine($"error: not an initialized LLM IDE project: {normalizedPath}");
            return 1;
        }

        projectStore.ReadProjectInfo(normalizedPath);
        Console.WriteLine($"opened: {normalizedPath}");
        return 0;
    }

    /// <summary>
    /// Removes a project from the registry.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjectsRemove(string[] args)
    {
        if (args.Length != 3)
        {
            PrintProjectsUsage();
            return 1;
        }

        bool removed = projectRegistryService.Remove(args[2]);
        Console.WriteLine(removed ? "removed" : "not found");
        return removed ? 0 : 1;
    }

    /// <summary>
    /// Deletes a project folder and removes it from the registry.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjectsDelete(string[] args)
    {
        if (args.Length < 5)
        {
            PrintProjectsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        ValidateDeleteConfirmation(args, project);

        if (Directory.Exists(project.Path))
        {
            Directory.Delete(project.Path, true);
        }

        projectRegistryService.Remove(args[2]);
        Console.WriteLine($"deleted: {project.Name}");
        return 0;
    }

    /// <summary>
    /// Validates destructive delete confirmation arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="project">The project to delete.</param>
    private void ValidateDeleteConfirmation(string[] args, ProjectRegistryEntry project)
    {
        if (args[3] != "--confirm")
        {
            throw new InvalidOperationException("Delete requires: --confirm <project-name>");
        }

        if (!string.Equals(args[4], project.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Delete confirmation project name does not match.");
        }

        if (HasApiKey(project.Path) && !args.Contains("--confirm-api-key-delete", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Project contains an API key. Add --confirm-api-key-delete to delete it.");
        }
    }

    /// <summary>
    /// Determines whether a project has any configured API key.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>True when at least one API key exists.</returns>
    private bool HasApiKey(string projectRoot)
    {
        ProviderSettingsDocument settings = providerSettingsStore.Load(projectRoot);
        return settings.Providers.Any(provider => !string.IsNullOrWhiteSpace(provider.ApiKey));
    }

    /// <summary>
    /// Renames a project in the registry.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjectsRename(string[] args)
    {
        if (args.Length != 4)
        {
            PrintProjectsUsage();
            return 1;
        }

        projectRegistryService.Rename(args[2], args[3]);
        Console.WriteLine($"renamed: {args[2]} -> {args[3]}");
        return 0;
    }

    /// <summary>
    /// Updates a project path in the registry.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjectsMove(string[] args)
    {
        if (args.Length != 4)
        {
            PrintProjectsUsage();
            return 1;
        }

        projectRegistryService.Move(args[2], args[3]);
        Console.WriteLine($"moved: {args[2]} -> {Path.GetFullPath(args[3])}");
        return 0;
    }

    /// <summary>
    /// Parses an init request.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The parsed request.</returns>
    private static ProjectInitializationRequest ParseInitRequest(string[] args)
    {
        ProjectInitializationRequest request = new ProjectInitializationRequest();
        int index = 1;

        while (index < args.Length)
        {
            string option = args[index];

            if (option == "--name" || option == "-n")
            {
                request.Name = ReadOptionValue(args, index, option);
                request.HasExplicitName = true;
                index += 2;
                continue;
            }

            if (option == "--path" || option == "-p")
            {
                request.Path = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            throw new InvalidOperationException($"Unknown init option: {option}");
        }

        return request;
    }

    /// <summary>
    /// Reads an option value.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="optionIndex">The option index.</param>
    /// <param name="option">The option name.</param>
    /// <returns>The option value.</returns>
    private static string ReadOptionValue(string[] args, int optionIndex, string option)
    {
        int valueIndex = optionIndex + 1;

        if (valueIndex >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for option: {option}");
        }

        return args[valueIndex];
    }

    /// <summary>
    /// Prints root command usage.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide init");
        Console.WriteLine("  llmide init --name <project-name>");
        Console.WriteLine("  llmide init --path <project-path>");
        Console.WriteLine("  llmide init --name <project-name> --path <project-path>");
        Console.WriteLine("  llmide init -n <project-name>");
        Console.WriteLine("  llmide init -p <project-path>");
        Console.WriteLine("  llmide init -n <project-name> -p <project-path>");
        Console.WriteLine("  llmide projects list");
        Console.WriteLine("  llmide projects open <project-name>");
        Console.WriteLine("  llmide projects remove <project-name>");
        Console.WriteLine("  llmide projects delete <project-name> --confirm <project-name>");
        Console.WriteLine("  llmide projects delete <project-name> --confirm <project-name> --confirm-api-key-delete");
        Console.WriteLine("  llmide projects rename <project-name> <new-project-name>");
        Console.WriteLine("  llmide projects move <project-name> <new-project-path>");
        Console.WriteLine("  llmide models list <project-name>");
        Console.WriteLine("  llmide models set <project-name> <model>");
        Console.WriteLine("  llmide criteria list <project-name>");
        Console.WriteLine("  llmide criteria add <project-name> --title <title> --description <description>");
        Console.WriteLine("  llmide criteria add <project-name> --title <title> --description <description> --priority <priority>");
        Console.WriteLine("  llmide criteria update <project-name> <criterion-id> --title <title>");
        Console.WriteLine("  llmide criteria update <project-name> <criterion-id> --description <description>");
        Console.WriteLine("  llmide criteria update <project-name> <criterion-id> --priority <priority>");
        Console.WriteLine("  llmide criteria remove <project-name> <criterion-id>");
        Console.WriteLine("  llmide criteria activate <project-name> <criterion-id>");
        Console.WriteLine("  llmide criteria deactivate <project-name> <criterion-id>");
        Console.WriteLine("  llmide state show <project-name>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage>");
        Console.WriteLine("  llmide state set <project-name> --current-task <task>");
        Console.WriteLine("  llmide state set <project-name> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage> --current-task <task>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-name> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state add <project-name> completed <item>");
        Console.WriteLine("  llmide state add <project-name> in-progress <item>");
        Console.WriteLine("  llmide state add <project-name> next-action <item>");
        Console.WriteLine("  llmide state add <project-name> blocker <item>");
        Console.WriteLine("  llmide state remove <project-name> completed <item>");
        Console.WriteLine("  llmide state remove <project-name> in-progress <item>");
        Console.WriteLine("  llmide state remove <project-name> next-action <item>");
        Console.WriteLine("  llmide state remove <project-name> blocker <item>");
        Console.WriteLine("  llmide chat <project-name> <message>");
        Console.WriteLine("  llmide chat <project-name> <message> --debug");
        Console.WriteLine("  llmide chat <project-name> <message> --no-stream");
        Console.WriteLine("  llmide chat <project-name> <message> --debug --no-stream");
    }

    /// <summary>
    /// Prints projects command usage.
    /// </summary>
    private static void PrintProjectsUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide projects list");
        Console.WriteLine("  llmide projects open <project-name>");
        Console.WriteLine("  llmide projects remove <project-name>");
        Console.WriteLine("  llmide projects delete <project-name> --confirm <project-name>");
        Console.WriteLine("  llmide projects delete <project-name> --confirm <project-name> --confirm-api-key-delete");
        Console.WriteLine("  llmide projects rename <project-name> <new-project-name>");
        Console.WriteLine("  llmide projects move <project-name> <new-project-path>");
    }

    /// <summary>
    /// Prints models command usage.
    /// </summary>
    private static void PrintModelsUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide models list <project-name>");
        Console.WriteLine("  llmide models set <project-name> <model>");
    }

    /// <summary>
    /// Prints criteria command usage.
    /// </summary>
    private static void PrintCriteriaUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide criteria list <project-name>");
        Console.WriteLine("  llmide criteria add <project-name> --title <title> --description <description>");
        Console.WriteLine("  llmide criteria add <project-name> --title <title> --description <description> --priority <priority>");
        Console.WriteLine("  llmide criteria update <project-name> <criterion-id> --title <title>");
        Console.WriteLine("  llmide criteria update <project-name> <criterion-id> --description <description>");
        Console.WriteLine("  llmide criteria update <project-name> <criterion-id> --priority <priority>");
        Console.WriteLine("  llmide criteria remove <project-name> <criterion-id>");
        Console.WriteLine("  llmide criteria activate <project-name> <criterion-id>");
        Console.WriteLine("  llmide criteria deactivate <project-name> <criterion-id>");
    }

    /// <summary>
    /// Prints state command usage.
    /// </summary>
    private static void PrintStateUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide state show <project-name>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage>");
        Console.WriteLine("  llmide state set <project-name> --current-task <task>");
        Console.WriteLine("  llmide state set <project-name> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage> --current-task <task>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-name> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-name> --stage <stage> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state add <project-name> completed <item>");
        Console.WriteLine("  llmide state add <project-name> in-progress <item>");
        Console.WriteLine("  llmide state add <project-name> next-action <item>");
        Console.WriteLine("  llmide state add <project-name> blocker <item>");
        Console.WriteLine("  llmide state remove <project-name> completed <item>");
        Console.WriteLine("  llmide state remove <project-name> in-progress <item>");
        Console.WriteLine("  llmide state remove <project-name> next-action <item>");
        Console.WriteLine("  llmide state remove <project-name> blocker <item>");
    }

    /// <summary>
    /// Prints chat command usage.
    /// </summary>
    private static void PrintChatUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide chat <project-name> <message>");
        Console.WriteLine("  llmide chat <project-name> <message> --debug");
        Console.WriteLine("  llmide chat <project-name> <message> --no-stream");
        Console.WriteLine("  llmide chat <project-name> <message> --debug --no-stream");
    }
}

/// <summary>
/// Describes the chat debug output.
/// </summary>
public sealed class ChatDebugView
{
    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sent messages.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];
}

/// <summary>
/// Describes parsed criteria command options.
/// </summary>
public sealed class CriteriaOptions
{
    /// <summary>
    /// Gets or sets the criterion title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the criterion description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the criterion priority.
    /// </summary>
    public string? Priority { get; set; }
}

/// <summary>
/// Describes parsed project state set options.
/// </summary>
public sealed class ProjectStateSetOptions
{
    /// <summary>
    /// Gets or sets the project stage.
    /// </summary>
    public string? Stage { get; set; }

    /// <summary>
    /// Gets or sets the current task.
    /// </summary>
    public string? CurrentTask { get; set; }

    /// <summary>
    /// Gets or sets the last decision.
    /// </summary>
    public string? LastDecision { get; set; }

    /// <summary>
    /// Gets a value indicating whether any option was supplied.
    /// </summary>
    public bool HasAnyValue => Stage is not null || CurrentTask is not null || LastDecision is not null;
}
