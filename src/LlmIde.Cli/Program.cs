using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using LlmIde.Infrastructure.Conversations;
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
            ]));
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
            providerSettingsService);

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
    /// Initializes a new instance of the <see cref="CliApplication"/> class.
    /// </summary>
    /// <param name="projectStore">The project metadata store.</param>
    /// <param name="projectInitializer">The project initializer.</param>
    /// <param name="projectRegistryService">The project registry service.</param>
    /// <param name="chatService">The chat service.</param>
    /// <param name="providerSettingsStore">The provider settings store.</param>
    /// <param name="providerSettingsService">The provider settings service.</param>
    public CliApplication(
        IProjectStore projectStore,
        ProjectInitializer projectInitializer,
        ProjectRegistryService projectRegistryService,
        ChatService chatService,
        IProviderSettingsStore providerSettingsStore,
        ProviderSettingsService providerSettingsService)
    {
        this.projectStore = projectStore;
        this.projectInitializer = projectInitializer;
        this.projectRegistryService = projectRegistryService;
        this.chatService = chatService;
        this.providerSettingsStore = providerSettingsStore;
        this.providerSettingsService = providerSettingsService;
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
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        Console.WriteLine("debug:");
        Console.WriteLine(JsonSerializer.Serialize(debugView, options));
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
