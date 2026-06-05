using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Projects;

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
        IProjectStore projectStore = new JsonFileProjectStore();
        RecentProjectService recentProjectService = new RecentProjectService(new JsonRecentProjectStore());
        ProjectInitializer projectInitializer = new ProjectInitializer(projectStore, recentProjectService);
        CliApplication application = new CliApplication(projectStore, projectInitializer, recentProjectService);

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
    /// The recent project service.
    /// </summary>
    private readonly RecentProjectService recentProjectService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliApplication"/> class.
    /// </summary>
    /// <param name="projectStore">The project metadata store.</param>
    /// <param name="projectInitializer">The project initializer.</param>
    /// <param name="recentProjectService">The recent project service.</param>
    public CliApplication(
        IProjectStore projectStore,
        ProjectInitializer projectInitializer,
        RecentProjectService recentProjectService)
    {
        this.projectStore = projectStore;
        this.projectInitializer = projectInitializer;
        this.recentProjectService = recentProjectService;
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
        string path = args.Length >= 2 ? args[1] : ".";
        ProjectInitializationResult result = projectInitializer.Initialize(path);
        string action = result.Created ? "initialized" : "opened";

        Console.WriteLine($"{action}: {result.ProjectRoot}");
        Console.WriteLine($"project: {result.ProjectInfo.Title}");
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

        PrintProjectsUsage();
        return 1;
    }

    /// <summary>
    /// Lists recent projects.
    /// </summary>
    /// <returns>The process exit code.</returns>
    private int RunProjectsList()
    {
        IReadOnlyList<RecentProjectEntry> projects = recentProjectService.List();

        if (projects.Count == 0)
        {
            Console.WriteLine("No recent projects.");
            return 0;
        }

        for (int index = 0; index < projects.Count; index++)
        {
            RecentProjectEntry project = projects[index];
            Console.WriteLine($"{index + 1}. {project.Title}  {project.Path}");
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
        if (args.Length >= 3)
        {
            return OpenProjectPath(args[2]);
        }

        IReadOnlyList<RecentProjectEntry> projects = recentProjectService.List();

        if (projects.Count == 0)
        {
            Console.WriteLine("No recent projects.");
            return 1;
        }

        RunProjectsList();
        Console.Write("Select project number: ");
        string? input = Console.ReadLine();

        if (!int.TryParse(input, out int selectedIndex))
        {
            Console.Error.WriteLine("error: invalid project number.");
            return 1;
        }

        if (selectedIndex < 1 || selectedIndex > projects.Count)
        {
            Console.Error.WriteLine("error: project number is out of range.");
            return 1;
        }

        RecentProjectEntry selectedProject = projects[selectedIndex - 1];
        return OpenProjectPath(selectedProject.Path);
    }

    /// <summary>
    /// Opens a project path and records it as recent.
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

        ProjectInfo projectInfo = projectStore.ReadProjectInfo(normalizedPath);
        recentProjectService.RecordOpened(normalizedPath, projectInfo.Title);
        Console.WriteLine($"opened: {normalizedPath}");
        return 0;
    }

    /// <summary>
    /// Removes projects from the recent project list.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjectsRemove(string[] args)
    {
        if (args.Length >= 3)
        {
            bool removed = recentProjectService.Remove(args[2]);
            Console.WriteLine(removed ? "removed" : "not found");
            return removed ? 0 : 1;
        }

        int removedMissingCount = recentProjectService.RemoveMissing();
        Console.WriteLine($"removed missing projects: {removedMissingCount}");
        return 0;
    }

    /// <summary>
    /// Prints root command usage.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide init [path]");
        Console.WriteLine("  llmide projects list");
        Console.WriteLine("  llmide projects open [path]");
        Console.WriteLine("  llmide projects remove [path]");
    }

    /// <summary>
    /// Prints projects command usage.
    /// </summary>
    private static void PrintProjectsUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  llmide projects list");
        Console.WriteLine("  llmide projects open [path]");
        Console.WriteLine("  llmide projects remove [path]");
    }
}
