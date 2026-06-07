using LlmIde.Cli;
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
            CliChatInjectsRollingSummary,
            CliChatDebugPrintsSentRequest,
            CliChatStopsWithoutActiveCriteria,
            CliChatInjectsSystemRule,
            CliModelsListPrintsAvailableModels,
            CliModelsSetUpdatesProviderSettings,
            CliCriteriaAddListUpdateDeactivateRemove,
            CliChatInjectsActiveCriteria,
            CliStateShowSetAddRemove,
            CliChatInjectsProjectState,
            ArtifactTagParserExtractsCandidates,
            CliArtifactsAddListShowUpdateRemove,
            CliArtifactsExtractPrintsCandidates,
            CliChatAttachesArtifact,
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
        AssertFileExists(result.ProjectRoot, ".llmide/conversations/rolling-context/current.md");
        AssertFileExists(result.ProjectRoot, ".llmide/conversations/rolling-context/index.jsonl");
        AssertFileExists(result.ProjectRoot, ".llmide/policies/system-rule.md");
        AssertFileExists(result.ProjectRoot, ".llmide/policies/compression-rule.md");
        AssertFileExists(result.ProjectRoot, ".llmide/policies/artifact-rule.md");
        AssertFileExists(result.ProjectRoot, ".llmide/policies/importance-rule.md");
        AssertFileExists(result.ProjectRoot, ".llmide/policies/context-policy.json");
        AssertFileExists(result.ProjectRoot, ".llmide/policies/provider-policy.json");
        AssertFileExists(workspace.Root, "project/projects.json");

        string policyRoot = Path.Combine(result.ProjectRoot, ".llmide", "policies");
        AssertContains(File.ReadAllText(Path.Combine(policyRoot, "system-rule.md")), "기본 응답 언어는 한국어다.");
        AssertContains(File.ReadAllText(Path.Combine(policyRoot, "compression-rule.md")), "너는 대화 맥락 압축기다.");
        AssertContains(File.ReadAllText(Path.Combine(policyRoot, "artifact-rule.md")), "산출물 태그");
        AssertContains(File.ReadAllText(Path.Combine(policyRoot, "importance-rule.md")), "대화 중요도");
        AssertContains(File.ReadAllText(Path.Combine(policyRoot, "context-policy.json")), "summary_plus_window");
        AssertContains(File.ReadAllText(Path.Combine(policyRoot, "provider-policy.json")), "deepseek");
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
            AssertContains(usage, "llmide artifacts list <project-name>");
            AssertContains(usage, "llmide artifacts show <project-name> <artifact-id>");
            AssertContains(usage, "llmide artifacts add <project-name> --title <title> --type <type> --content <content>");
            AssertContains(usage, "llmide artifacts update <project-name> <artifact-id> --content <content>");
            AssertContains(usage, "llmide artifacts remove <project-name> <artifact-id>");
            AssertContains(usage, "llmide artifacts extract <project-name> --content <response-text>");
            AssertContains(usage, "llmide chat <project-name> <message>");
            AssertContains(usage, "llmide chat <project-name> <message> --artifact <artifact-id>");
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
        AssertEqual(2, CountRows(databasePath, "conversation_turns"), "Chat and compression requests should be stored.");
        AssertEqual(2, CountRows(databasePath, "conversation_messages"), "User and assistant messages should be stored in SQLite.");
        AssertEqual(2, CountRows(databasePath, "context_packages"), "Chat and compression context packages should be stored in SQLite.");
        AssertEqual(1, CountRows(databasePath, "rolling_context_summaries"), "One rolling context summary should be stored in SQLite.");
        AssertEqual(2, File.ReadAllLines(messagesPath).Length, "User and assistant messages should be stored.");
        AssertEqual(2, File.ReadAllLines(requestsPath).Length, "Chat and compression requests should be stored.");
        AssertEqual(2, Directory.GetFiles(packagesPath, "*.json").Length, "Chat and compression context packages should be stored.");
        ConversationMessageRecord[] messages = ReadJsonLines<ConversationMessageRecord>(messagesPath);
        ConversationRequestRecord[] requests = ReadJsonLines<ConversationRequestRecord>(requestsPath);
        AssertEqual("1", messages[0].MessageId, "First stored user message should use a readable sequence identifier.");
        AssertEqual("2", messages[1].MessageId, "First stored assistant message should use a readable sequence identifier.");
        AssertEqual("1", requests[0].RequestId, "First chat request should use a readable sequence identifier.");
        AssertEqual("2", requests[1].RequestId, "First compression request should use a readable sequence identifier.");
        AssertEqual(7, requests[0].ImportanceWeight, "Chat request should store the parsed importance weight.");
        AssertEqual(0, requests[1].ImportanceWeight, "Compression request should not store chat importance.");
        AssertFileExists(projectRoot, ".llmide/conversations/context-packages/1.json");
        AssertFileExists(projectRoot, ".llmide/conversations/context-packages/2.json");
        AssertFileExists(projectRoot, ".llmide/conversations/rolling-context/current.md");
        string rollingContext = File.ReadAllText(Path.Combine(projectRoot, ".llmide", "conversations", "rolling-context", "current.md"));
        AssertFalse(string.IsNullOrWhiteSpace(rollingContext), "Current rolling context should be updated.");
        AssertContains(rollingContext, "ok:");
    }

    /// <summary>
    /// Verifies that CLI chat injects the rolling summary into the next provider request.
    /// </summary>
    private static void CliChatInjectsRollingSummary()
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
            string rollingContextPath = Path.Combine(
                workspace.Root,
                "MemoryProject",
                ".llmide",
                "conversations",
                "rolling-context",
                "current.md");
            string rollingContext = File.ReadAllText(rollingContextPath);
            Console.SetOut(output);
            int exitCode = application.Run(["chat", "MemoryProject", "second", "--debug"]);
            string chatOutput = output.ToString();

            AssertEqual(0, exitCode, "Second chat should succeed.");
            AssertFalse(string.IsNullOrWhiteSpace(rollingContext), "Rolling context should be created after first chat.");
            AssertContains(chatOutput, "압축된 이전 대화 맥락을 사용한다.");
            AssertContains(chatOutput, rollingContext);
            AssertContains(chatOutput, "\"content\": \"second\"");
            AssertFalse(chatOutput.Contains("\"content\": \"first\"", StringComparison.Ordinal), "Previous raw user message should not be sent.");
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
            AssertContains(chatOutput, "디버그:");
            AssertContains(chatOutput, "\"messages\"");
            AssertContains(chatOutput, "\"role\": \"user\"");
            AssertContains(chatOutput, "\"content\": \"hello\"");
            AssertContains(chatOutput, "응답:");
            AssertContains(chatOutput, "압축_디버그:");
            AssertContains(chatOutput, "압축_응답:");
            AssertContains(chatOutput, "압축_상태: completed");
            AssertContains(chatOutput, "[원본 대화 로그]");
            AssertContains(chatOutput, "ok:");
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
    /// Verifies that the project system rule is injected into chat provider requests.
    /// </summary>
    private static void CliChatInjectsSystemRule()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "SystemRuleProject"]);
            AddDefaultCriterion(application, "SystemRuleProject");
            File.WriteAllText(
                Path.Combine(workspace.Root, "SystemRuleProject", ".llmide", "policies", "system-rule.md"),
                "System says: always preserve user approval boundaries.");
            Console.SetOut(output);
            int chatExitCode = application.Run(["chat", "SystemRuleProject", "hello", "--debug"]);
            string chatOutput = output.ToString();

            AssertEqual(0, chatExitCode, "Chat with system rule should succeed.");
            AssertContains(chatOutput, "System says: always preserve user approval boundaries.");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
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
            AssertContains(chatOutput, "이번 응답에서는 다음 활성 프로젝트 기준을 따른다.");
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
            AssertContains(chatOutput, "현재 프로젝트 상태를 맥락으로 사용한다.");
            AssertContains(chatOutput, "단계: phase5");
            AssertContains(chatOutput, "현재 작업: Project State 관리");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies that explicit artifact tags are extracted.
    /// </summary>
    private static void ArtifactTagParserExtractsCandidates()
    {
        string response = """
            일반 답변입니다.
            <artifact type="markdown" title="README 초안" path="README.md">
            # README 초안

            프로젝트 설명입니다.
            </artifact>
            """;

        IReadOnlyList<ArtifactCandidate> candidates = ArtifactTagParser.Extract(response);

        AssertEqual(1, candidates.Count, "One artifact candidate should be extracted.");
        AssertEqual("README 초안", candidates[0].Title, "Artifact title should be parsed.");
        AssertEqual("markdown", candidates[0].Type, "Artifact type should be parsed.");
        AssertEqual("README.md", candidates[0].TargetPath, "Artifact target path should be parsed.");
        AssertContains(candidates[0].Content, "프로젝트 설명입니다.");
    }

    /// <summary>
    /// Verifies artifact CLI storage commands.
    /// </summary>
    private static void CliArtifactsAddListShowUpdateRemove()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "ArtifactProject"]);
            Console.SetOut(output);
            int addExitCode = application.Run([
                "artifacts",
                "add",
                "ArtifactProject",
                "--title",
                "README 초안",
                "--type",
                "markdown",
                "--path",
                "README.md",
                "--content",
                "# README"]);
            string artifactId = ExtractArtifactId(output.ToString());

            output.GetStringBuilder().Clear();
            int listExitCode = application.Run(["artifacts", "list", "ArtifactProject"]);
            string listOutput = output.ToString();

            output.GetStringBuilder().Clear();
            int showExitCode = application.Run(["artifacts", "show", "ArtifactProject", artifactId]);
            string showOutput = output.ToString();

            output.GetStringBuilder().Clear();
            int updateExitCode = application.Run([
                "artifacts",
                "update",
                "ArtifactProject",
                artifactId,
                "--title",
                "수정된 README",
                "--content",
                "# Updated"]);

            output.GetStringBuilder().Clear();
            int removeExitCode = application.Run(["artifacts", "remove", "ArtifactProject", artifactId]);

            AssertEqual(0, addExitCode, "Artifact add should succeed.");
            AssertEqual(0, listExitCode, "Artifact list should succeed.");
            AssertEqual(0, showExitCode, "Artifact show should succeed.");
            AssertEqual(0, updateExitCode, "Artifact update should succeed.");
            AssertEqual(0, removeExitCode, "Artifact remove should succeed.");
            AssertContains(listOutput, artifactId);
            AssertContains(showOutput, "# README");

            string projectRoot = Path.Combine(workspace.Root, "ArtifactProject");
            string indexPath = Path.Combine(projectRoot, ".llmide", "artifacts", "artifacts.index.json");
            string indexJson = File.ReadAllText(indexPath);
            AssertFalse(indexJson.Contains(artifactId, StringComparison.Ordinal), "Removed artifact should be removed from index.");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies artifact candidate extraction through CLI.
    /// </summary>
    private static void CliArtifactsExtractPrintsCandidates()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "ExtractProject"]);
            Console.SetOut(output);
            int exitCode = application.Run([
                "artifacts",
                "extract",
                "ExtractProject",
                "--content",
                "<artifact type=\"note\" title=\"메모\">중요 내용</artifact>"]);
            string extractOutput = output.ToString();

            AssertEqual(0, exitCode, "Artifact extract should succeed.");
            AssertContains(extractOutput, "메모");
            AssertContains(extractOutput, "중요 내용");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Verifies stored artifacts can be attached to chat context.
    /// </summary>
    private static void CliChatAttachesArtifact()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        CliApplication application = CreateCliApplication(workspace.Root);
        StringWriter output = new StringWriter();
        TextWriter originalOutput = Console.Out;

        try
        {
            application.Run(["init", "--name", "AttachProject"]);
            AddDefaultCriterion(application, "AttachProject");
            WriteArtifactRule(
                Path.Combine(workspace.Root, "AttachProject"),
                "응답에 재사용 가능한 중요한 산출물 후보가 있으면 artifact 태그로 감싼다.");
            Console.SetOut(output);
            application.Run([
                "artifacts",
                "add",
                "AttachProject",
                "--title",
                "설계 메모",
                "--type",
                "markdown",
                "--content",
                "아티팩트 본문"]);
            string artifactId = ExtractArtifactId(output.ToString());

            output.GetStringBuilder().Clear();
            int chatExitCode = application.Run(["chat", "AttachProject", "요약해줘", "--artifact", artifactId, "--debug"]);
            string chatOutput = output.ToString();

            AssertEqual(0, chatExitCode, "Chat with artifact should succeed.");
            AssertContains(chatOutput, "사용자가 첨부한 다음 산출물을 맥락으로 사용한다.");
            AssertContains(chatOutput, "설계 메모");
            AssertContains(chatOutput, "아티팩트 본문");
            AssertContains(chatOutput, "응답에 재사용 가능한 중요한 산출물 후보가 있으면");
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
        return new ProjectInitializer(new JsonFileProjectStore(ideProgramRoot), registry, ideProgramRoot);
    }

    /// <summary>
    /// Creates a CLI application for tests.
    /// </summary>
    /// <param name="ideProgramRoot">The test IDE program root.</param>
    /// <returns>The CLI application.</returns>
    private static CliApplication CreateCliApplication(string ideProgramRoot)
    {
        IProjectStore projectStore = new JsonFileProjectStore(ideProgramRoot);
        IProviderSettingsStore providerSettingsStore = new JsonProviderSettingsStore();
        CriteriaService criteriaService = new CriteriaService(new JsonCriteriaStore());
        ProjectStateService projectStateService = new ProjectStateService(new JsonProjectStateStore());
        FileRollingContextStore rollingContextStore = new FileRollingContextStore(ideProgramRoot);
        ArtifactService artifactService = new ArtifactService(new FileArtifactStore());
        ContextBuilder contextBuilder = new ContextBuilder(
            criteriaService,
            projectStateService,
            rollingContextStore,
            new FileSystemRuleStore(),
            new FileArtifactRuleStore(),
            new FileImportanceRuleStore(ideProgramRoot),
            artifactService);
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
            contextBuilder,
            rollingContextStore,
            artifactService);
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
            projectStateService,
            artifactService);
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
    /// Extracts an artifact id from CLI output.
    /// </summary>
    /// <param name="output">The CLI output.</param>
    /// <returns>The artifact id.</returns>
    private static string ExtractArtifactId(string output)
    {
        string prefix = "산출물: ";
        string line = output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("산출물 ID가 출력되지 않았습니다.");
        return line[prefix.Length..].Trim();
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
            "rolling_context_summaries" => "select count(*) from rolling_context_summaries;",
            _ => throw new InvalidOperationException($"Unexpected table: {tableName}")
        };

        object? result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Reads JSONL records from a file.
    /// </summary>
    /// <typeparam name="TValue">The record type.</typeparam>
    /// <param name="path">The JSONL path.</param>
    /// <returns>The parsed records.</returns>
    private static TValue[] ReadJsonLines<TValue>(string path)
    {
        List<TValue> values = [];

        foreach (string line in File.ReadAllLines(path))
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

        return values.ToArray();
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
    /// Writes the artifact tagging rule for a test project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="content">The artifact rule content.</param>
    private static void WriteArtifactRule(string projectRoot, string content)
    {
        string path = Path.Combine(projectRoot, ".llmide", "policies", "artifact-rule.md");
        File.WriteAllText(path, content);
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
        string content = CreateContent(request);
        return Task.FromResult(new ChatProviderResponse
        {
            Content = content,
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
        yield return CreateContent(request);
    }

    /// <summary>
    /// Creates fake response content.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <returns>The fake response content.</returns>
    private static string CreateContent(ChatProviderRequest request)
    {
        string content = $"ok:{request.Messages.Count}";

        foreach (ChatMessage message in request.Messages)
        {
            if (!message.Content.Contains("llmide_importance_weight", StringComparison.Ordinal))
            {
                continue;
            }

            return content + Environment.NewLine + "<llmide_importance_weight>7</llmide_importance_weight>";
        }

        return content;
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
        CreatePolicyTemplates(root);
        return new TestWorkspace(root);
    }

    /// <summary>
    /// Creates program policy templates in the test workspace.
    /// </summary>
    /// <param name="root">The workspace root path.</param>
    private static void CreatePolicyTemplates(string root)
    {
        string policyRoot = Path.Combine(root, "policies");
        Directory.CreateDirectory(policyRoot);
        File.WriteAllText(Path.Combine(policyRoot, "system-rule.md"), """
            기본 응답 언어는 한국어다.
            사용자가 다른 언어를 명시적으로 요청하지 않는 한 한국어로 답한다.
            """);
        File.WriteAllText(Path.Combine(policyRoot, "compression-rule.md"), """
            # 압축 규칙

            너는 대화 맥락 압축기다.
            """);
        File.WriteAllText(Path.Combine(policyRoot, "artifact-rule.md"), """
            응답에 재사용 가능한 중요한 산출물 후보가 있으면 산출물 태그로 감싼다.
            """);
        File.WriteAllText(Path.Combine(policyRoot, "importance-rule.md"), """
            대화 중요도 측정 규칙
            응답 마지막에 <llmide_importance_weight>N</llmide_importance_weight> 태그를 추가한다.
            """);
        File.WriteAllText(Path.Combine(policyRoot, "context-policy.json"), """
            {
              "context_management_strategy": "summary_plus_window",
              "include_rolling_context_summary": true,
              "rolling_context_path": "conversations/rolling-context/current.md",
              "recent_turn_count": 0,
              "max_recent_turn_chars": 0,
              "max_rolling_context_chars": 24000,
              "compression_enabled": true,
              "compression_provider": "deepseek",
              "compression_model": "deepseek-chat",
              "compression_after_chat": true,
              "compression_failure_strategy": "keep_previous",
              "rag_enabled": false
            }
            """);
        File.WriteAllText(Path.Combine(policyRoot, "provider-policy.json"), """
            {
              "default_provider": "deepseek",
              "allowed_providers": [
                "deepseek"
              ],
              "api_key_storage": "project_local_settings",
              "allow_empty_api_key": true,
              "network_required": true
            }
            """);
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
