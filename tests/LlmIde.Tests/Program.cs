using LlmIde.Cli;
using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using LlmIde.Infrastructure.Conversations;
using LlmIde.Infrastructure.Json;
using LlmIde.Infrastructure.Projects;
using LlmIde.Infrastructure.Providers;
using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
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
            CliChatStoresConversationLogs,
            CliChatInjectsRecentMessages,
            CliChatDebugPrintsSentRequest,
            CliChatStopsWithoutActiveCriteria,
            CliModelsListPrintsAvailableModels,
            CliModelsSetUpdatesProviderSettings,
            CliCriteriaAddListUpdateDeactivateRemove,
            CliChatInjectsActiveCriteria,
            CliStateShowSetAddRemove,
            CliChatInjectsProjectState,
            JsonOptionsPreserveKoreanText,
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
        AssertFileExists(result.ProjectRoot, ".llmide/conversations/conversation.db");
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
            AssertContains(usage, "llmide models list <project-name>");
            AssertContains(usage, "llmide models set <project-name> <model>");
            AssertContains(usage, "llmide criteria list <project-name>");
            AssertContains(usage, "llmide criteria add <project-name> --title <title> --description <description>");
            AssertContains(usage, "llmide criteria add <project-name> --title <title> --description <description> --priority <priority>");
            AssertContains(usage, "llmide criteria update <project-name> <criterion-id> --title <title>");
            AssertContains(usage, "llmide criteria update <project-name> <criterion-id> --description <description>");
            AssertContains(usage, "llmide criteria update <project-name> <criterion-id> --priority <priority>");
            AssertContains(usage, "llmide criteria remove <project-name> <criterion-id>");
            AssertContains(usage, "llmide criteria activate <project-name> <criterion-id>");
            AssertContains(usage, "llmide criteria deactivate <project-name> <criterion-id>");
            AssertContains(usage, "llmide state show <project-name>");
            AssertContains(usage, "llmide state set <project-name> --stage <stage>");
            AssertContains(usage, "llmide state set <project-name> --current-task <task>");
            AssertContains(usage, "llmide state set <project-name> --last-decision <decision>");
            AssertContains(usage, "llmide state set <project-name> --stage <stage> --current-task <task>");
            AssertContains(usage, "llmide state set <project-name> --stage <stage> --last-decision <decision>");
            AssertContains(usage, "llmide state set <project-name> --current-task <task> --last-decision <decision>");
            AssertContains(usage, "llmide state set <project-name> --stage <stage> --current-task <task> --last-decision <decision>");
            AssertContains(usage, "llmide state add <project-name> completed <item>");
            AssertContains(usage, "llmide state add <project-name> in-progress <item>");
            AssertContains(usage, "llmide state add <project-name> next-action <item>");
            AssertContains(usage, "llmide state add <project-name> blocker <item>");
            AssertContains(usage, "llmide state remove <project-name> completed <item>");
            AssertContains(usage, "llmide state remove <project-name> in-progress <item>");
            AssertContains(usage, "llmide state remove <project-name> next-action <item>");
            AssertContains(usage, "llmide state remove <project-name> blocker <item>");
            AssertContains(usage, "llmide chat <project-name> <message>");
            AssertContains(usage, "llmide chat <project-name> <message> --debug");
            AssertContains(usage, "llmide chat <project-name> <message> --no-stream");
            AssertContains(usage, "llmide chat <project-name> <message> --debug --no-stream");
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
            AddDefaultCriterion(application, "ChatProject");
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
    /// Verifies that CLI chat stores messages, requests, and a context package.
    /// </summary>
    private static void CliChatStoresConversationLogs()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);

        application.Run(["init", "--name", "LogProject"]);
        AddDefaultCriterion(application, "LogProject");
        int chatExitCode = application.Run(["chat", "LogProject", "hello"]);
        string projectRoot = Path.Combine(workspace.Root, "LogProject");
        string messagesPath = Path.Combine(projectRoot, ".llmide", "conversations", "messages.jsonl");
        string requestsPath = Path.Combine(projectRoot, ".llmide", "conversations", "requests.jsonl");
        string packagesPath = Path.Combine(projectRoot, ".llmide", "conversations", "context-packages");
        string databasePath = Path.Combine(projectRoot, ".llmide", "conversations", "conversation.db");

        AssertEqual(0, chatExitCode, "Chat should succeed.");
        AssertTrue(File.Exists(databasePath), "Conversation database should be created.");
        AssertEqual(1, CountRows(databasePath, "conversation_turns"), "One conversation turn should be stored.");
        AssertEqual(2, CountRows(databasePath, "conversation_messages"), "User and assistant messages should be stored in SQLite.");
        AssertEqual(1, CountRows(databasePath, "context_packages"), "One context package should be stored in SQLite.");
        AssertEqual(2, File.ReadAllLines(messagesPath).Length, "User and assistant messages should be stored.");
        AssertEqual(1, File.ReadAllLines(requestsPath).Length, "One request should be stored.");
        AssertEqual(1, Directory.GetFiles(packagesPath, "*.json").Length, "One context package should be stored.");
    }

    /// <summary>
    /// Verifies that CLI chat injects recent messages into the next provider request.
    /// </summary>
    private static void CliChatInjectsRecentMessages()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "MemoryProject"]);
            AddDefaultCriterion(application, "MemoryProject");
            application.Run(["chat", "MemoryProject", "first"]);
            Console.SetOut(output);
            int exitCode = application.Run(["chat", "MemoryProject", "second"]);
            string chatOutput = output.ToString();

            AssertEqual(0, exitCode, "Second chat should succeed.");
            AssertContains(chatOutput, "ok:5");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that CLI chat debug output includes the provider request messages.
    /// </summary>
    private static void CliChatDebugPrintsSentRequest()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "DebugProject"]);
            AddDefaultCriterion(application, "DebugProject");
            Console.SetOut(output);
            int exitCode = application.Run(["chat", "DebugProject", "hello", "--debug"]);
            string chatOutput = output.ToString();

            AssertEqual(0, exitCode, "Debug chat should succeed.");
            AssertContains(chatOutput, "debug:");
            AssertContains(chatOutput, "\"messages\"");
            AssertContains(chatOutput, "\"role\": \"user\"");
            AssertContains(chatOutput, "\"content\": \"hello\"");
            AssertContains(chatOutput, "response:");
            AssertContains(chatOutput, "ok:3");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that CLI chat stops before provider transmission without active criteria.
    /// </summary>
    private static void CliChatStopsWithoutActiveCriteria()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);

        application.Run(["init", "--name", "NoCriteriaProject"]);
        int exitCode = application.Run(["chat", "NoCriteriaProject", "hello"]);
        string projectRoot = Path.Combine(workspace.Root, "NoCriteriaProject");
        string messagesPath = Path.Combine(projectRoot, ".llmide", "conversations", "messages.jsonl");
        string requestsPath = Path.Combine(projectRoot, ".llmide", "conversations", "requests.jsonl");

        AssertEqual(1, exitCode, "Chat without active criteria should fail.");
        AssertEqual(0, File.ReadAllLines(messagesPath).Length, "No message should be stored when criteria are missing.");
        AssertEqual(0, File.ReadAllLines(requestsPath).Length, "No request should be stored when criteria are missing.");
    }

    /// <summary>
    /// Verifies that CLI models list prints available provider models.
    /// </summary>
    private static void CliModelsListPrintsAvailableModels()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "ModelsProject"]);
            Console.SetOut(output);
            int exitCode = application.Run(["models", "list", "ModelsProject"]);
            string modelsOutput = output.ToString();

            AssertEqual(0, exitCode, "Models list should succeed.");
            AssertContains(modelsOutput, "deepseek-chat");
            AssertContains(modelsOutput, "deepseek-reasoner");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that CLI models set updates provider settings.
    /// </summary>
    private static void CliModelsSetUpdatesProviderSettings()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);

        application.Run(["init", "--name", "ModelSetProject"]);
        int exitCode = application.Run(["models", "set", "ModelSetProject", "deepseek-reasoner"]);
        ProviderSettingsDocument settings = new JsonProviderSettingsStore().Load(Path.Combine(workspace.Root, "ModelSetProject"));

        AssertEqual(0, exitCode, "Models set should succeed.");
        AssertEqual("deepseek-reasoner", settings.Providers[0].Model, "Provider model should be updated.");
    }

    /// <summary>
    /// Verifies that CLI criteria commands create, list, update, deactivate, and remove criteria.
    /// </summary>
    private static void CliCriteriaAddListUpdateDeactivateRemove()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "CriteriaProject"]);
            Console.SetOut(output);
            int addExitCode = application.Run([
                "criteria",
                "add",
                "CriteriaProject",
                "--title",
                "Korean",
                "--description",
                "Answer in Korean",
                "--priority",
                "high"]);
            string addOutput = output.ToString();
            string criterionId = addOutput.Split(':', StringSplitOptions.TrimEntries)[1].Trim();

            output.GetStringBuilder().Clear();
            int listExitCode = application.Run(["criteria", "list", "CriteriaProject"]);
            string listOutput = output.ToString();

            output.GetStringBuilder().Clear();
            int updateExitCode = application.Run(["criteria", "update", "CriteriaProject", criterionId, "--title", "KoreanOnly"]);
            int deactivateExitCode = application.Run(["criteria", "deactivate", "CriteriaProject", criterionId]);
            IReadOnlyList<Criterion> criteriaAfterDeactivate = new JsonCriteriaStore().Load(Path.Combine(workspace.Root, "CriteriaProject"));
            int removeExitCode = application.Run(["criteria", "remove", "CriteriaProject", criterionId]);
            IReadOnlyList<Criterion> criteriaAfterRemove = new JsonCriteriaStore().Load(Path.Combine(workspace.Root, "CriteriaProject"));

            AssertEqual(0, addExitCode, "Criteria add should succeed.");
            AssertEqual(0, listExitCode, "Criteria list should succeed.");
            AssertContains(listOutput, "Korean");
            AssertContains(listOutput, "Answer in Korean");
            AssertEqual(0, updateExitCode, "Criteria update should succeed.");
            AssertEqual("KoreanOnly", criteriaAfterDeactivate[0].Title, "Criterion title should be updated.");
            AssertEqual(0, deactivateExitCode, "Criteria deactivate should succeed.");
            AssertEqual("inactive", criteriaAfterDeactivate[0].Status, "Criterion should be inactive.");
            AssertEqual(0, removeExitCode, "Criteria remove should succeed.");
            AssertEqual(0, criteriaAfterRemove.Count, "Criterion should be removed.");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that active criteria are injected into chat provider requests.
    /// </summary>
    private static void CliChatInjectsActiveCriteria()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "CriteriaChatProject"]);
            application.Run([
                "criteria",
                "add",
                "CriteriaChatProject",
                "--title",
                "Korean",
                "--description",
                "Answer in Korean"]);
            Console.SetOut(output);
            int chatExitCode = application.Run(["chat", "CriteriaChatProject", "hello", "--debug"]);
            string chatOutput = output.ToString();

            AssertEqual(0, chatExitCode, "Chat with criteria should succeed.");
            AssertContains(chatOutput, "\"role\": \"system\"");
            AssertContains(chatOutput, "Follow these active project criteria");
            AssertContains(chatOutput, "Answer in Korean");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that project state commands show, set, add, and remove state values.
    /// </summary>
    private static void CliStateShowSetAddRemove()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "StateProject"]);
            Console.SetOut(output);
            int setExitCode = application.Run([
                "state",
                "set",
                "StateProject",
                "--stage",
                "phase5",
                "--current-task",
                "Project State 관리",
                "--last-decision",
                "상태는 사용자 승인으로 변경한다"]);

            output.GetStringBuilder().Clear();
            int addExitCode = application.Run(["state", "add", "StateProject", "next-action", "Phase 6 시작"]);
            int secondAddExitCode = application.Run(["state", "add", "StateProject", "blocker", "없음"]);
            int removeExitCode = application.Run(["state", "remove", "StateProject", "blocker", "없음"]);

            output.GetStringBuilder().Clear();
            int showExitCode = application.Run(["state", "show", "StateProject"]);
            string stateOutput = output.ToString();
            ProjectState state = new JsonProjectStateStore().Load(Path.Combine(workspace.Root, "StateProject"));

            AssertEqual(0, setExitCode, "State set should succeed.");
            AssertEqual(0, addExitCode, "State add should succeed.");
            AssertEqual(0, secondAddExitCode, "Second state add should succeed.");
            AssertEqual(0, removeExitCode, "State remove should succeed.");
            AssertEqual(0, showExitCode, "State show should succeed.");
            AssertEqual("phase5", state.Stage, "Stage should be updated.");
            AssertEqual("Project State 관리", state.CurrentTask, "Current task should be updated.");
            AssertEqual("상태는 사용자 승인으로 변경한다", state.LastDecision, "Last decision should be updated.");
            AssertEqual(1, state.NextActions.Count, "Next action should be added.");
            AssertEqual("Phase 6 시작", state.NextActions[0], "Next action should be stored.");
            AssertEqual(0, state.Blockers.Count, "Blocker should be removed.");
            AssertContains(stateOutput, "Project State 관리");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that project state is injected into chat provider requests.
    /// </summary>
    private static void CliChatInjectsProjectState()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "StateChatProject"]);
            AddDefaultCriterion(application, "StateChatProject");
            application.Run([
                "state",
                "set",
                "StateChatProject",
                "--stage",
                "phase5",
                "--current-task",
                "Project State 관리"]);
            Console.SetOut(output);
            int chatExitCode = application.Run(["chat", "StateChatProject", "hello", "--debug"]);
            string chatOutput = output.ToString();

            AssertEqual(0, chatExitCode, "Chat with project state should succeed.");
            AssertContains(chatOutput, "Use this current project state as context");
            AssertContains(chatOutput, "Stage: phase5");
            AssertContains(chatOutput, "Current task: Project State 관리");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that JSON serialization keeps Korean text readable.
    /// </summary>
    private static void JsonOptionsPreserveKoreanText()
    {
        string json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["text"] = "항상 한국어로 답한다"
        }, JsonOptions.Default);

        AssertContains(json, "항상 한국어로 답한다");
        AssertFalse(json.Contains("\\uD56D", StringComparison.OrdinalIgnoreCase), "Korean text should not be unicode escaped.");
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
        IProviderSettingsStore providerSettingsStore = new JsonProviderSettingsStore();
        CriteriaService criteriaService = new CriteriaService(new JsonCriteriaStore());
        ProjectStateService projectStateService = new ProjectStateService(new JsonProjectStateStore());
        ProjectRegistryService registry = CreateProjectRegistryService(ideProgramRoot);
        ProjectInitializer initializer = new ProjectInitializer(projectStore, registry, ideProgramRoot);
        ChatService chatService = new ChatService(
            providerSettingsStore,
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = new FakeChatProvider()
            },
            new CompositeConversationLogStore(
            [
                new JsonlConversationLogStore(),
                new SqliteConversationLogStore()
            ]),
            criteriaService,
            projectStateService);
        ProviderSettingsService providerSettingsService = new ProviderSettingsService(
            providerSettingsStore,
            new Dictionary<string, IModelProvider>
            {
                ["deepseek"] = new FakeModelProvider()
            });

        return new CliApplication(
            projectStore,
            initializer,
            registry,
            chatService,
            providerSettingsStore,
            providerSettingsService,
            criteriaService,
            projectStateService);
    }

    /// <summary>
    /// Adds a default active criterion to a test project.
    /// </summary>
    /// <param name="application">The CLI application.</param>
    /// <param name="projectName">The project name.</param>
    private static void AddDefaultCriterion(CliApplication application, string projectName)
    {
        application.Run([
            "criteria",
            "add",
            projectName,
            "--title",
            "Default",
            "--description",
            "Use project criteria"]);
    }

    /// <summary>
    /// Counts rows in a known SQLite table.
    /// </summary>
    /// <param name="databasePath">The SQLite database path.</param>
    /// <param name="tableName">The known table name.</param>
    /// <returns>The row count.</returns>
    private static int CountRows(string databasePath, string tableName)
    {
        using SqliteConnection connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = tableName switch
        {
            "conversation_turns" => "select count(*) from conversation_turns;",
            "conversation_messages" => "select count(*) from conversation_messages;",
            "context_packages" => "select count(*) from context_packages;",
            _ => throw new InvalidOperationException($"Unexpected table: {tableName}")
        };

        object? result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Writes an API key into a project's provider settings.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="apiKey">The API key.</param>
    private static void WriteApiKey(string projectRoot, string apiKey)
    {
        JsonProviderSettingsStore providerSettingsStore = new JsonProviderSettingsStore();
        ProviderSettingsDocument settings = providerSettingsStore.Load(projectRoot);
        settings.Providers[0].ApiKey = apiKey;
        providerSettingsStore.Save(projectRoot, settings);
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
            Content = $"ok:{request.Messages.Count}",
            Provider = settings.Name,
            Model = settings.Model
        });
    }

    /// <summary>
    /// Streams a fake chat response.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The fake response chunks.</returns>
    public async IAsyncEnumerable<string> StreamAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return $"ok:{request.Messages.Count}";
    }
}

/// <summary>
/// Provides fake provider models for CLI tests.
/// </summary>
public sealed class FakeModelProvider : IModelProvider
{
    /// <summary>
    /// Lists fake provider models.
    /// </summary>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available fake models.</returns>
    public Task<IReadOnlyList<ProviderModel>> ListModelsAsync(
        ProviderSettings settings,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProviderModel> models =
        [
            new ProviderModel
            {
                Id = "deepseek-chat"
            },
            new ProviderModel
            {
                Id = "deepseek-reasoner"
            }
        ];

        return Task.FromResult(models);
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
