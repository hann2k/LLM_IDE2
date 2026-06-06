using LlmIde.Cli;
using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using LlmIde.Infrastructure.Json;
using LlmIde.Infrastructure.Projects;
using LlmIde.Infrastructure.Providers;
using System.Text.Json;

namespace LlmIde.Tests;

/// <summary>
/// Provides a lightweight test runner for implemented behavior.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs all tests.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static int Main()
    {
        List<Action> tests =
        [
            DefaultInitCreatesDefaultProjectBelowProgramRoot,
            NamedInitWithPathStoresRegistryAndDataSeparately,
            DuplicateDefaultInitStopsBeforeCreatingNewFiles,
            RegistryRenameAndMoveUpdateProjectInfo,
            CliDeleteRemovesProjectFolderAndRegistryEntry,
            CliDeleteRequiresConfirmation,
            CliDeleteRequiresExtraConfirmationWhenApiKeyExists,
            ProviderSettingsAreCreatedWithEmptyApiKey,
            CliChatPrintsProviderResponse,
            CliWithoutOptionsPrintsFullUsage
        ];

        foreach (Action test in tests)
        {
            test();
            Console.WriteLine($"passed: {test.Method.Name}");
        }

        Console.WriteLine("All tests passed.");
        return 0;
    }

    /// <summary>
    /// Verifies that default initialization creates DefaultProject below the program root.
    /// </summary>
    private static void DefaultInitCreatesDefaultProjectBelowProgramRoot()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.Root);

        ProjectInitializationResult result = initializer.Initialize(new ProjectInitializationRequest());

        AssertEqual("DefaultProject", result.ProjectInfo.Title, "Default project name should be assigned.");
        AssertEqual(Path.Combine(workspace.Root, "DefaultProject"), result.ProjectRoot, "Default project path should be under program root.");
        AssertFileExists(result.ProjectRoot, ".llmide/project.json");
        AssertFileExists(result.ProjectRoot, ".llmide/init-progress.json");
        AssertFileExists(workspace.Root, "project/projects.json");
    }

    /// <summary>
    /// Verifies that a named project with a path stores registry info separately from project data.
    /// </summary>
    private static void NamedInitWithPathStoresRegistryAndDataSeparately()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.Root);
        string projectRoot = Path.Combine(workspace.Root, "external", "alpha");

        ProjectInitializationResult result = initializer.Initialize(new ProjectInitializationRequest
        {
            Name = "Alpha",
            Path = projectRoot,
            HasExplicitName = true
        });

        ProjectRegistryService registry = CreateProjectRegistryService(workspace.Root);
        IReadOnlyList<ProjectRegistryEntry> projects = registry.List();

        AssertEqual(Path.GetFullPath(projectRoot), result.ProjectRoot, "Project data should be stored at the requested path.");
        AssertEqual(1, projects.Count, "Registry should contain one project.");
        AssertEqual("Alpha", projects[0].Name, "Registry should store the project name.");
        AssertEqual(Path.GetFullPath(projectRoot), projects[0].Path, "Registry should store the project path.");
        AssertTrue(projects[0].CreatedAt != default, "Registry should store the creation date.");
        AssertFileExists(workspace.Root, "project/projects.json");
        AssertDirectoryNotExists(workspace.Root, "project/Alpha");
    }

    /// <summary>
    /// Verifies that duplicate default creation fails before creating new files.
    /// </summary>
    private static void DuplicateDefaultInitStopsBeforeCreatingNewFiles()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.Root);

        initializer.Initialize(new ProjectInitializationRequest());
        DateTime registryWriteTime = File.GetLastWriteTimeUtc(Path.Combine(workspace.Root, "project", "projects.json"));
        InvalidOperationException ex = AssertThrows<InvalidOperationException>(() => initializer.Initialize(new ProjectInitializationRequest()));

        AssertEqual("DefaultProject already exists.", ex.Message, "Duplicate default project should report a specific error.");
        AssertEqual(registryWriteTime, File.GetLastWriteTimeUtc(Path.Combine(workspace.Root, "project", "projects.json")), "Registry should not be written on duplicate default error.");
    }

    /// <summary>
    /// Verifies that registry rename and move update stored project information.
    /// </summary>
    private static void RegistryRenameAndMoveUpdateProjectInfo()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.Root);
        ProjectRegistryService registry = CreateProjectRegistryService(workspace.Root);

        initializer.Initialize(new ProjectInitializationRequest
        {
            Name = "Alpha",
            HasExplicitName = true
        });

        string movedPath = Path.Combine(workspace.Root, "MovedAlpha");
        registry.Rename("Alpha", "Beta");
        registry.Move("Beta", movedPath);

        ProjectRegistryEntry project = registry.GetRequired("Beta");
        AssertEqual("Beta", project.Name, "Project name should be updated.");
        AssertEqual(Path.GetFullPath(movedPath), project.Path, "Project path should be updated.");
    }

    /// <summary>
    /// Verifies that running the CLI without options prints all usage combinations.
    /// </summary>
    private static void CliWithoutOptionsPrintsFullUsage()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);
            int exitCode = application.Run([]);
            string usage = output.ToString();

            AssertEqual(0, exitCode, "Usage output should exit successfully.");
            AssertContains(usage, "llmide init");
            AssertContains(usage, "llmide init --name <project-name>");
            AssertContains(usage, "llmide init --path <project-path>");
            AssertContains(usage, "llmide init --name <project-name> --path <project-path>");
            AssertContains(usage, "llmide projects list");
            AssertContains(usage, "llmide projects open <project-name>");
            AssertContains(usage, "llmide projects remove <project-name>");
            AssertContains(usage, "llmide projects delete <project-name> --confirm <project-name>");
            AssertContains(usage, "llmide projects delete <project-name> --confirm <project-name> --confirm-api-key-delete");
            AssertContains(usage, "llmide projects rename <project-name> <new-project-name>");
            AssertContains(usage, "llmide projects move <project-name> <new-project-path>");
            AssertContains(usage, "llmide chat <project-name> <message>");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that CLI delete removes both the project folder and registry entry.
    /// </summary>
    private static void CliDeleteRemovesProjectFolderAndRegistryEntry()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);

        int initExitCode = application.Run(["init", "--name", "DeleteMe"]);
        int deleteExitCode = application.Run(["projects", "delete", "DeleteMe", "--confirm", "DeleteMe"]);
        ProjectRegistryService registry = CreateProjectRegistryService(workspace.Root);

        AssertEqual(0, initExitCode, "Init should succeed.");
        AssertEqual(0, deleteExitCode, "Delete should succeed.");
        AssertDirectoryNotExists(workspace.Root, "DeleteMe");
        AssertEqual(0, registry.List().Count, "Registry entry should be removed.");
    }

    /// <summary>
    /// Verifies that CLI delete requires explicit project name confirmation.
    /// </summary>
    private static void CliDeleteRequiresConfirmation()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);

        int initExitCode = application.Run(["init", "--name", "KeepMe"]);
        int deleteExitCode = application.Run(["projects", "delete", "KeepMe"]);
        ProjectRegistryService registry = CreateProjectRegistryService(workspace.Root);

        AssertEqual(0, initExitCode, "Init should succeed.");
        AssertEqual(1, deleteExitCode, "Delete without confirmation should fail.");
        AssertTrue(Directory.Exists(Path.Combine(workspace.Root, "KeepMe")), "Project folder should remain.");
        AssertEqual(1, registry.List().Count, "Registry entry should remain.");
    }

    /// <summary>
    /// Verifies that CLI delete requires extra confirmation when a project has an API key.
    /// </summary>
    private static void CliDeleteRequiresExtraConfirmationWhenApiKeyExists()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);

        application.Run(["init", "--name", "KeyProject"]);
        WriteApiKey(Path.Combine(workspace.Root, "KeyProject"), "secret");
        int firstDeleteExitCode = application.Run(["projects", "delete", "KeyProject", "--confirm", "KeyProject"]);
        int secondDeleteExitCode = application.Run(["projects", "delete", "KeyProject", "--confirm", "KeyProject", "--confirm-api-key-delete"]);

        AssertEqual(1, firstDeleteExitCode, "Delete with API key should require extra confirmation.");
        AssertEqual(0, secondDeleteExitCode, "Delete with extra confirmation should succeed.");
        AssertDirectoryNotExists(workspace.Root, "KeyProject");
    }

    /// <summary>
    /// Verifies that provider settings are created with an empty API key placeholder.
    /// </summary>
    private static void ProviderSettingsAreCreatedWithEmptyApiKey()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.Root);

        ProjectInitializationResult result = initializer.Initialize(new ProjectInitializationRequest());
        ProviderSettingsDocument settings = new JsonProviderSettingsStore().Load(result.ProjectRoot);
        ProviderSettings provider = settings.Providers[0];

        AssertEqual("deepseek", settings.DefaultProvider, "Default provider should be DeepSeek.");
        AssertEqual("deepseek", provider.Name, "Provider settings should include DeepSeek.");
        AssertEqual(string.Empty, provider.ApiKey, "API key should be an empty placeholder.");
        AssertEqual("deepseek-chat", provider.Model, "DeepSeek model should be initialized.");
    }

    /// <summary>
    /// Verifies that CLI chat sends a message and prints the provider response.
    /// </summary>
    private static void CliChatPrintsProviderResponse()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "ChatProject"]);
            Console.SetOut(output);
            int exitCode = application.Run(["chat", "ChatProject", "hello"]);
            string chatOutput = output.ToString();

            AssertEqual(0, exitCode, "Chat should succeed with the fake provider.");
            AssertContains(chatOutput, "ok");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Creates a project initializer for tests.
    /// </summary>
    /// <param name="ideProgramRoot">The test IDE program root.</param>
    /// <returns>The project initializer.</returns>
    private static ProjectInitializer CreateProjectInitializer(string ideProgramRoot)
    {
        ProjectRegistryService registry = CreateProjectRegistryService(ideProgramRoot);
        return new ProjectInitializer(new JsonFileProjectStore(), registry, ideProgramRoot);
    }

    /// <summary>
    /// Creates a CLI application for tests.
    /// </summary>
    /// <param name="ideProgramRoot">The test IDE program root.</param>
    /// <returns>The CLI application.</returns>
    private static CliApplication CreateCliApplication(string ideProgramRoot)
    {
        IProjectStore projectStore = new JsonFileProjectStore();
        ProjectRegistryService registry = CreateProjectRegistryService(ideProgramRoot);
        ProjectInitializer initializer = new ProjectInitializer(projectStore, registry, ideProgramRoot);
        ChatService chatService = new ChatService(
            new JsonProviderSettingsStore(),
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = new FakeChatProvider()
            });

        return new CliApplication(projectStore, initializer, registry, chatService, new JsonProviderSettingsStore());
    }

    /// <summary>
    /// Writes an API key into a project's provider settings.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="apiKey">The API key.</param>
    private static void WriteApiKey(string projectRoot, string apiKey)
    {
        string providersPath = Path.Combine(projectRoot, ".llmide", "settings", "providers.json");
        ProviderSettingsDocument settings = new JsonProviderSettingsStore().Load(projectRoot);
        settings.Providers[0].ApiKey = apiKey;
        string json = JsonSerializer.Serialize(settings, JsonOptions.Default);
        File.WriteAllText(providersPath, json);
    }

    /// <summary>
    /// Creates a project registry service for tests.
    /// </summary>
    /// <param name="ideProgramRoot">The test IDE program root.</param>
    /// <returns>The project registry service.</returns>
    private static ProjectRegistryService CreateProjectRegistryService(string ideProgramRoot)
    {
        return new ProjectRegistryService(new JsonProjectRegistryStore(ideProgramRoot));
    }

    /// <summary>
    /// Asserts that a file exists below a project root.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="relativePath">The relative file path.</param>
    private static void AssertFileExists(string projectRoot, string relativePath)
    {
        string path = Path.Combine(projectRoot, relativePath);
        AssertTrue(File.Exists(path), $"Expected file to exist: {relativePath}");
    }

    /// <summary>
    /// Asserts that a directory does not exist below a project root.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="relativePath">The relative directory path.</param>
    private static void AssertDirectoryNotExists(string projectRoot, string relativePath)
    {
        string path = Path.Combine(projectRoot, relativePath);
        AssertFalse(Directory.Exists(path), $"Expected directory not to exist: {relativePath}");
    }

    /// <summary>
    /// Asserts that text contains a value.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <param name="value">The expected value.</param>
    private static void AssertContains(string text, string value)
    {
        AssertTrue(text.Contains(value, StringComparison.Ordinal), $"Expected usage to contain: {value}");
    }

    /// <summary>
    /// Asserts that a condition is true.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="message">The failure message.</param>
    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Asserts that a condition is false.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="message">The failure message.</param>
    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Asserts that two values are equal.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="message">The failure message.</param>
    private static void AssertEqual<TValue>(TValue expected, TValue actual, string message)
    {
        if (!EqualityComparer<TValue>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    /// <summary>
    /// Asserts that an action throws.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <returns>The thrown exception.</returns>
    private static TException AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected exception: {typeof(TException).Name}");
    }
}

/// <summary>
/// Provides a fake chat provider for CLI tests.
/// </summary>
public sealed class FakeChatProvider : IChatProvider
{
    /// <summary>
    /// Sends a fake chat request.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The fake response.</returns>
    public Task<ChatProviderResponse> SendAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ChatProviderResponse
        {
            Content = "ok",
            Provider = settings.Name,
            Model = settings.Model
        });
    }
}

/// <summary>
/// Provides an isolated temporary workspace for tests.
/// </summary>
public sealed class TestWorkspace : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestWorkspace"/> class.
    /// </summary>
    /// <param name="root">The workspace root path.</param>
    private TestWorkspace(string root)
    {
        Root = root;
    }

    /// <summary>
    /// Gets the workspace root path.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Creates a new test workspace.
    /// </summary>
    /// <returns>The created test workspace.</returns>
    public static TestWorkspace Create()
    {
        string root = Path.Combine(Path.GetTempPath(), "LlmIde.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TestWorkspace(root);
    }

    /// <summary>
    /// Deletes the test workspace.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
