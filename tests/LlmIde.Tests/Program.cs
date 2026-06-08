using LlmIde.Cli;
using LlmIde.Core.Agents;
using LlmIde.Core.Artifacts;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using LlmIde.Infrastructure.Agents;
using LlmIde.Infrastructure.Artifacts;
using LlmIde.Infrastructure.Conversations;
using LlmIde.Infrastructure.Json;
using LlmIde.Infrastructure.Projects;
using LlmIde.Infrastructure.Providers;
using Microsoft.Data.Sqlite;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
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
            CliWithoutOptionsPrintsFullUsage,
            AgentLoopReturnsFinalAnswer,
            AgentLoopRunsToolThenFinal,
            AgentLoopListToolsThenAnswers,
            AgentLoopToolErrorIsRecorded,
            AgentLoopFetchUrlThenFinal,
            AgentLoopMaxTurnsExceeded,
            AgentLoopUnknownToolInvalid,
            AgentLoopInvalidJsonFails,
            FetchUrlBlocksLocalhost,
            FetchUrlBlocksPrivateIp,
            FetchUrlBlocksFileScheme,
            FetchUrlTruncatesLongResponse,
            FetchUrlToolFailureDoesNotThrow,
            ChatServiceRunsToolThenAnswers,
            WebSearchParsesResults,
            WebSearchEmptyQueryFails,
            ConversationDeleteRemovesTurnAndMessages,
            ManualConversationIsStoredAsCompletedTurn,
            UpdateAssistantMessageContentReplacesContent
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
            PId = "Alpha",
            Name = "알파 프로젝트",
            HasExplicitPId = true,
            Path = projectRoot,
            HasExplicitName = true
        });

        ProjectRegistryService registry = CreateProjectRegistryService(workspace.Root);
        IReadOnlyList<ProjectRegistryEntry> projects = registry.List();

        AssertEqual(Path.GetFullPath(projectRoot), result.ProjectRoot, "Project data should be stored at the requested path.");
        AssertEqual("알파 프로젝트", result.ProjectInfo.Title, "Project metadata should store the display name.");
        AssertEqual(1, projects.Count, "Registry should contain one project.");
        AssertEqual("Alpha", projects[0].PId, "Registry should store the project identifier.");
        AssertEqual("알파 프로젝트", projects[0].Name, "Registry should store the display name.");
        AssertEqual(Path.GetFullPath(projectRoot), projects[0].Path, "Registry should store the project path.");
        AssertTrue(projects[0].CreatedAt != default, "Registry should store the creation date.");
        AssertFileExists(workspace.Root, "project/projects.json");
        string registryJson = File.ReadAllText(Path.Combine(workspace.Root, "project", "projects.json"));
        AssertContains(registryJson, "\"pID\": \"Alpha\"");
        AssertContains(registryJson, "\"name\": \"알파 프로젝트\"");
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
    /// Verifies that registry display name, project identifier, and path updates work.
    /// </summary>
    private static void RegistryRenameAndMoveUpdateProjectInfo()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.Root);
        ProjectRegistryService registry = CreateProjectRegistryService(workspace.Root);

        initializer.Initialize(new ProjectInitializationRequest
        {
            PId = "Alpha",
            HasExplicitPId = true
        });

        string movedPath = Path.Combine(workspace.Root, "MovedAlpha");
        registry.Rename("Alpha", "베타 프로젝트");
        registry.ChangePId("Alpha", "Beta");
        registry.Move("Beta", movedPath);

        ProjectRegistryEntry project = registry.GetRequired("Beta");
        AssertEqual("Beta", project.PId, "Project identifier should be updated.");
        AssertEqual("베타 프로젝트", project.Name, "Display name should be updated.");
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
            AssertContains(usage, "llmide init --pid <project-id>");
            AssertContains(usage, "llmide init --pid <project-id> --name <project-name>");
            AssertContains(usage, "llmide init --path <project-path>");
            AssertContains(usage, "llmide init --pid <project-id> --name <project-name> --path <project-path>");
            AssertContains(usage, "llmide projects list");
            AssertContains(usage, "llmide projects open <project-id>");
            AssertContains(usage, "llmide projects remove <project-id>");
            AssertContains(usage, "llmide projects delete <project-id> --confirm <project-id>");
            AssertContains(usage, "llmide projects delete <project-id> --confirm <project-id> --confirm-api-key-delete");
            AssertContains(usage, "llmide projects rename <project-id> <new-project-name>");
            AssertContains(usage, "llmide projects repid <project-id> <new-project-id>");
            AssertContains(usage, "llmide projects move <project-id> <new-project-path>");
            AssertContains(usage, "llmide models list <project-id>");
            AssertContains(usage, "llmide models set <project-id> <model>");
            AssertContains(usage, "llmide criteria list <project-id>");
            AssertContains(usage, "llmide criteria add <project-id> --title <title> --description <description>");
            AssertContains(usage, "llmide state show <project-id>");
            AssertContains(usage, "llmide artifacts list <project-id>");
            AssertContains(usage, "llmide chat <project-id> <message>");
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

        int initExitCode = application.Run(["init", "--pid", "DeleteMe"]);
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

        int initExitCode = application.Run(["init", "--pid", "KeepMe"]);
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

        application.Run(["init", "--pid", "KeyProject"]);
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
            application.Run(["init", "--pid", "ChatProject"]);
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

        application.Run(["init", "--pid", "LogProject"]);
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
        AssertEqual("1c", requests[1].RequestId, "First compression request should use a readable sequence identifier.");
        AssertEqual(7, requests[0].ImportanceWeight, "Chat request should store the parsed importance weight.");
        AssertEqual(0, requests[1].ImportanceWeight, "Compression request should not store chat importance.");
        AssertFileExists(projectRoot, ".llmide/conversations/context-packages/1.json");
        AssertFileExists(projectRoot, ".llmide/conversations/context-packages/1c.json");
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
            application.Run(["init", "--pid", "MemoryProject"]);
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
            // Long-term chat now also sends the recent conversation window verbatim.
            AssertContains(chatOutput, "\"content\": \"first\"");
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
            application.Run(["init", "--pid", "DebugProject"]);
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

        application.Run(["init", "--pid", "NoCriteriaProject"]);
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
            application.Run(["init", "--pid", "SystemRuleProject"]);
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
            application.Run(["init", "--pid", "ModelsProject"]);
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

        application.Run(["init", "--pid", "ModelSetProject"]);
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
            application.Run(["init", "--pid", "CriteriaProject"]);
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
            application.Run(["init", "--pid", "CriteriaChatProject"]);
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
            application.Run(["init", "--pid", "StateProject"]);
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
            application.Run(["init", "--pid", "StateChatProject"]);
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
            application.Run(["init", "--pid", "ArtifactProject"]);
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
            application.Run(["init", "--pid", "ExtractProject"]);
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
            application.Run(["init", "--pid", "AttachProject"]);
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
    /// <summary>
    /// Verifies the agent loop returns immediately on a final answer.
    /// </summary>
    private static void AgentLoopReturnsFinalAnswer()
    {
        FakeChatModelClient client = new FakeChatModelClient("{\"type\":\"final\",\"answer\":\"hi\"}");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([]));

        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = "q" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.IsSuccess, "Final answer run should succeed.");
        AssertTrue(result.StopReason == StopReason.FinalAnswer, "Stop reason should be FinalAnswer.");
        AssertEqual("hi", result.FinalText, "Final text should be returned.");
        AssertEqual(0, result.ToolResults.Count, "No tools should run for an immediate final answer.");
    }

    /// <summary>
    /// Verifies a tool request runs the tool host and the model is called again.
    /// </summary>
    private static void AgentLoopRunsToolThenFinal()
    {
        FakeAgentTool tool = new FakeAgentTool();
        FakeChatModelClient client = new FakeChatModelClient(
            "{\"type\":\"tool_request\",\"tool\":\"fake_tool\",\"arguments\":{}}",
            "{\"type\":\"final\",\"answer\":\"done\"}");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([tool]));

        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = "q" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.IsSuccess, "Tool then final run should succeed.");
        AssertEqual(1, tool.CallCount, "Tool should be executed once.");
        AssertEqual(1, result.ToolResults.Count, "One tool result should be recorded.");
        AssertEqual("done", result.FinalText, "Final text should be returned after the tool.");
    }

    /// <summary>
    /// Verifies the agent loop answers a list_tools request, then runs a tool, then finalizes.
    /// </summary>
    private static void AgentLoopListToolsThenAnswers()
    {
        FakeAgentTool tool = new FakeAgentTool();
        FakeChatModelClient client = new FakeChatModelClient(
            "{\"type\":\"list_tools\"}",
            "{\"type\":\"tool_request\",\"tool\":\"fake_tool\",\"arguments\":{}}",
            "{\"type\":\"final\",\"answer\":\"done\"}");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([tool]));

        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = "q" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.IsSuccess, "list_tools then tool then final should succeed.");
        AssertEqual(1, tool.CallCount, "Tool should run once after discovery.");
        AssertEqual("done", result.FinalText, "Final text should be returned.");
        AssertEqual(2, result.ToolCalls.Count, "Both discovery and execution must be recorded in order.");
        AssertEqual("list_tools", result.ToolCalls[0].Tool, "First recorded step should be tool discovery.");
        AssertEqual("fake_tool", result.ToolCalls[1].Tool, "Second recorded step should be the tool execution.");
        AssertEqual(1, result.ToolResults.Count, "ToolResults should contain executions only (no discovery).");
    }

    /// <summary>
    /// Verifies a tool execution failure is still recorded in the result (no omission).
    /// </summary>
    private static void AgentLoopToolErrorIsRecorded()
    {
        FakeChatModelClient client = new FakeChatModelClient(
            "{\"type\":\"tool_request\",\"tool\":\"throwing_tool\",\"arguments\":{}}");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([new ThrowingAgentTool()]));

        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = "q" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertTrue(!result.IsSuccess, "Tool error should fail the run.");
        AssertEqual(StopReason.ToolError, result.StopReason, "Stop reason should be ToolError.");
        AssertEqual(1, result.ToolResults.Count, "The failed tool execution must still be recorded.");
        AssertTrue(!result.ToolResults[0].Ok, "Recorded tool result should be marked as failed.");
    }

    /// <summary>
    /// Verifies fetch_url result is used before a final answer.
    /// </summary>
    private static void AgentLoopFetchUrlThenFinal()
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(
            "<html><title>T</title><body>Hello <b>World</b></body></html>",
            "text/html",
            200);
        FetchUrlTool tool = new FetchUrlTool(new HttpClient(handler));
        FakeChatModelClient client = new FakeChatModelClient(
            "{\"type\":\"tool_request\",\"tool\":\"fetch_url\",\"arguments\":{\"url\":\"https://example.com\"}}",
            "{\"type\":\"final\",\"answer\":\"ok\"}");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([tool]));

        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = "q" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.IsSuccess, "Fetch then final run should succeed.");
        AssertEqual(1, result.ToolResults.Count, "One tool result should be recorded.");
        FetchUrlResult fetchResult = (FetchUrlResult)result.ToolResults[0].Result!;
        AssertTrue(fetchResult.Ok, "Fetch should succeed.");
        AssertContains(fetchResult.Text, "World");
        AssertEqual("ok", result.FinalText, "Final text should be returned after fetch_url.");
    }

    /// <summary>
    /// Verifies the agent loop stops with MaxTurnsExceeded.
    /// </summary>
    private static void AgentLoopMaxTurnsExceeded()
    {
        FakeChatModelClient client = new FakeChatModelClient("{\"type\":\"tool_request\",\"tool\":\"fake_tool\",\"arguments\":{}}");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([new FakeAgentTool()]));

        AgentRunResult result = loop.RunAsync(
            new AgentRunRequest { UserInput = "q", Options = new AgentLoopOptions { MaxTurns = 3 } },
            CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertFalse(result.IsSuccess, "Endless tool requests should not succeed.");
        AssertTrue(result.StopReason == StopReason.MaxTurnsExceeded, "Stop reason should be MaxTurnsExceeded.");
    }

    /// <summary>
    /// Verifies an unknown tool request stops with InvalidToolRequest.
    /// </summary>
    private static void AgentLoopUnknownToolInvalid()
    {
        FakeChatModelClient client = new FakeChatModelClient("{\"type\":\"tool_request\",\"tool\":\"nope\",\"arguments\":{}}");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([new FakeAgentTool()]));

        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = "q" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertFalse(result.IsSuccess, "Unknown tool should not succeed.");
        AssertTrue(result.StopReason == StopReason.InvalidToolRequest, "Stop reason should be InvalidToolRequest.");
    }

    /// <summary>
    /// Verifies invalid JSON model output fails safely.
    /// </summary>
    private static void AgentLoopInvalidJsonFails()
    {
        FakeChatModelClient client = new FakeChatModelClient("this is not json");
        AgentLoop loop = new AgentLoop(client, new AgentToolHost([]));

        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = "q" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertFalse(result.IsSuccess, "Invalid JSON should not succeed.");
        AssertTrue(result.StopReason == StopReason.ModelError, "Stop reason should be ModelError.");
    }

    /// <summary>
    /// Verifies fetch_url blocks localhost.
    /// </summary>
    private static void FetchUrlBlocksLocalhost()
    {
        AssertFetchBlocked("http://localhost:8080/admin");
    }

    /// <summary>
    /// Verifies fetch_url blocks private IP ranges.
    /// </summary>
    private static void FetchUrlBlocksPrivateIp()
    {
        AssertFetchBlocked("http://10.0.0.5/secret");
    }

    /// <summary>
    /// Verifies fetch_url blocks the file scheme.
    /// </summary>
    private static void FetchUrlBlocksFileScheme()
    {
        AssertFetchBlocked("file:///etc/passwd");
    }

    /// <summary>
    /// Verifies fetch_url truncates an oversized response.
    /// </summary>
    private static void FetchUrlTruncatesLongResponse()
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(new string('a', 50000), "text/plain", 200);
        FetchUrlTool tool = new FetchUrlTool(new HttpClient(handler), defaultMaxChars: 100);

        AgentToolResult result = tool.ExecuteAsync(CreateFetchRequest("https://example.com"), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        FetchUrlResult fetchResult = (FetchUrlResult)result.Result!;
        AssertTrue(fetchResult.Truncated, "Long response should be truncated.");
        AssertEqual(100, fetchResult.Text.Length, "Text should be truncated to maxChars.");
    }

    /// <summary>
    /// Verifies a tool failure does not throw.
    /// </summary>
    private static void FetchUrlToolFailureDoesNotThrow()
    {
        FetchUrlTool tool = new FetchUrlTool(new HttpClient(new ThrowingHttpMessageHandler()));

        AgentToolResult result = tool.ExecuteAsync(CreateFetchRequest("https://example.com"), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        FetchUrlResult fetchResult = (FetchUrlResult)result.Result!;
        AssertFalse(result.Ok, "Failed fetch should report not ok.");
        AssertFalse(fetchResult.Ok, "Failed fetch result should report not ok.");
    }

    /// <summary>
    /// Asserts that a fetch_url request to the given URL is blocked.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    private static void AssertFetchBlocked(string url)
    {
        FetchUrlTool tool = new FetchUrlTool(new HttpClient(new ThrowingHttpMessageHandler()));

        AgentToolResult result = tool.ExecuteAsync(CreateFetchRequest(url), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        FetchUrlResult fetchResult = (FetchUrlResult)result.Result!;
        AssertFalse(result.Ok, $"Fetch should be blocked: {url}");
        AssertFalse(fetchResult.Ok, $"Fetch result should be blocked: {url}");
    }

    /// <summary>
    /// Creates a fetch_url tool request for a URL.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>The tool request.</returns>
    private static AgentToolRequest CreateFetchRequest(string url)
    {
        JsonElement arguments = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(new { url }));
        return new AgentToolRequest { Tool = "fetch_url", RequestId = "tool-001", Arguments = arguments };
    }

    /// <summary>
    /// Verifies web_search parses result titles, URLs, and snippets.
    /// </summary>
    private static void WebSearchParsesResults()
    {
        string html =
            "<div class=\"result\"><a class=\"result__a\" href=\"//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fa\">First &amp; Title</a>" +
            "<a class=\"result__snippet\">Snippet <b>one</b></a></div>" +
            "<div class=\"result\"><a class=\"result__a\" href=\"//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.org%2Fb\">Second</a>" +
            "<a class=\"result__snippet\">Snippet two</a></div>";
        WebSearchTool tool = new WebSearchTool(new HttpClient(new FakeHttpMessageHandler(html, "text/html", 200)));

        AgentToolResult result = tool.ExecuteAsync(CreateSearchRequest("example"), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        WebSearchResult searchResult = (WebSearchResult)result.Result!;
        AssertTrue(searchResult.Ok, "Search should succeed.");
        AssertEqual(2, searchResult.Results.Count, "Two results should be parsed.");
        AssertEqual("First & Title", searchResult.Results[0].Title, "Title should be decoded.");
        AssertEqual("https://example.com/a", searchResult.Results[0].Url, "Redirect URL should be resolved.");
        AssertContains(searchResult.Results[0].Snippet, "one");
    }

    /// <summary>
    /// Verifies web_search rejects an empty query.
    /// </summary>
    private static void WebSearchEmptyQueryFails()
    {
        WebSearchTool tool = new WebSearchTool(new HttpClient(new ThrowingHttpMessageHandler()));

        AgentToolResult result = tool.ExecuteAsync(CreateSearchRequest(string.Empty), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        WebSearchResult searchResult = (WebSearchResult)result.Result!;
        AssertFalse(result.Ok, "Empty query should fail.");
        AssertFalse(searchResult.Ok, "Empty query result should fail.");
    }

    /// <summary>
    /// Verifies deleting a conversation removes its turn, messages, and tool calls but keeps others.
    /// </summary>
    private static void ConversationDeleteRemovesTurnAndMessages()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        string root = workspace.Root;
        IConversationLogStore store = new CompositeConversationLogStore(
        [
            new JsonlConversationLogStore(),
            new SqliteConversationLogStore()
        ]);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        store.SaveContextPackage(root, new ContextPackage { RequestId = "1" });
        store.AppendRequest(root, new ConversationRequestRecord { RequestId = "1", RequestType = "chat", CreatedAt = now });
        store.AppendMessage(root, new ConversationMessageRecord { MessageId = "1", RequestId = "1", Role = "user", Content = "hi", CreatedAt = now });
        store.AppendMessage(root, new ConversationMessageRecord { MessageId = "2", RequestId = "1", Role = "assistant", Content = "yo", CreatedAt = now });
        store.AppendToolCall(root, new ConversationToolCallRecord { RequestId = "1", Sequence = 1, Tool = "web_search", Ok = true, CreatedAt = now });

        store.AppendRequest(root, new ConversationRequestRecord { RequestId = "2", RequestType = "chat", CreatedAt = now });
        store.AppendMessage(root, new ConversationMessageRecord { MessageId = "3", RequestId = "2", Role = "user", Content = "keep", CreatedAt = now });

        store.DeleteConversation(root, "1");

        IReadOnlyList<ConversationMessageRecord> messages = store.GetRecentMessages(root, int.MaxValue);
        AssertEqual(1, messages.Count, "Only the surviving conversation's messages should remain.");
        AssertEqual("2", messages[0].RequestId, "Remaining message should belong to conversation 2.");
    }

    /// <summary>
    /// Verifies updating assistant message content replaces only the assistant message of the request.
    /// </summary>
    private static void UpdateAssistantMessageContentReplacesContent()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        string root = workspace.Root;
        IConversationLogStore store = new CompositeConversationLogStore(
        [
            new JsonlConversationLogStore(),
            new SqliteConversationLogStore()
        ]);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        store.AppendRequest(root, new ConversationRequestRecord { RequestId = "1", RequestType = "chat", CreatedAt = now });
        store.AppendMessage(root, new ConversationMessageRecord { MessageId = "1", RequestId = "1", Role = "user", Content = "질문", CreatedAt = now });
        store.AppendMessage(root, new ConversationMessageRecord { MessageId = "2", RequestId = "1", Role = "assistant", Content = "원래 응답 본문", CreatedAt = now });

        store.UpdateAssistantMessageContent(root, "1", "원래 [아티팩트: 제목] 본문");

        IReadOnlyList<ConversationMessageRecord> messages = store.GetRecentMessages(root, int.MaxValue);
        ConversationMessageRecord user = messages.First(message => message.Role == "user");
        ConversationMessageRecord assistant = messages.First(message => message.Role == "assistant");
        AssertEqual("원래 [아티팩트: 제목] 본문", assistant.Content, "Assistant content should be updated.");
        AssertEqual("질문", user.Content, "User content should be unchanged.");
    }

    /// <summary>
    /// Verifies a manually added conversation is stored as a completed user+assistant turn without an LLM call.
    /// </summary>
    private static void ManualConversationIsStoredAsCompletedTurn()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        string root = workspace.Root;
        ProjectInitializer initializer = CreateProjectInitializer(root);
        ProjectInitializationResult init = initializer.Initialize(new ProjectInitializationRequest
        {
            PId = "Manual",
            HasExplicitPId = true
        });
        string projectRoot = init.ProjectRoot;

        CriteriaService criteriaService = new CriteriaService(new JsonCriteriaStore());
        ProjectStateService projectStateService = new ProjectStateService(new JsonProjectStateStore());
        FileRollingContextStore rollingContextStore = new FileRollingContextStore(root);
        ArtifactService artifactService = new ArtifactService(new FileArtifactStore());
        ContextBuilder contextBuilder = new ContextBuilder(
            criteriaService,
            projectStateService,
            rollingContextStore,
            new FileSystemRuleStore(),
            new FileArtifactRuleStore(),
            new FileImportanceRuleStore(root),
            artifactService);
        IConversationLogStore logStore = new CompositeConversationLogStore(
        [
            new JsonlConversationLogStore(),
            new SqliteConversationLogStore()
        ]);
        ChatService chatService = new ChatService(
            new JsonProviderSettingsStore(),
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = new ScriptedChatProvider("unused")
            },
            logStore,
            contextBuilder,
            rollingContextStore,
            artifactService,
            new JsonFileProjectStore(root),
            new AgentToolHost([]));

        string requestId = chatService.AddManualConversation(projectRoot, "수동 사용자 발화", "수동 어시스턴트 응답");

        IReadOnlyList<ConversationMessageRecord> messages = logStore.GetRecentMessages(projectRoot, int.MaxValue);
        AssertEqual(2, messages.Count, "Manual conversation should store user and assistant messages.");
        ConversationMessageRecord user = messages.First(message => message.Role == "user");
        ConversationMessageRecord assistant = messages.First(message => message.Role == "assistant");
        AssertEqual(requestId, user.RequestId, "User message should use the new request id.");
        AssertEqual(requestId, assistant.RequestId, "Assistant message should use the new request id.");
        AssertEqual("수동 사용자 발화", user.Content, "User content should be preserved.");
        AssertEqual("수동 어시스턴트 응답", assistant.Content, "Assistant content should be preserved.");
    }

    /// <summary>
    /// Creates a web_search tool request for a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The tool request.</returns>
    private static AgentToolRequest CreateSearchRequest(string query)
    {
        JsonElement arguments = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(new { query }));
        return new AgentToolRequest { Tool = "web_search", RequestId = "tool-001", Arguments = arguments };
    }

    /// <summary>
    /// Verifies the chat pipeline runs an in-chat tool then produces a final answer.
    /// </summary>
    private static void ChatServiceRunsToolThenAnswers()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        string root = workspace.Root;
        ProjectInitializer initializer = CreateProjectInitializer(root);
        ProjectInitializationResult init = initializer.Initialize(new ProjectInitializationRequest
        {
            PId = "ToolChat",
            HasExplicitPId = true
        });
        string projectRoot = init.ProjectRoot;

        CriteriaService criteriaService = new CriteriaService(new JsonCriteriaStore());
        criteriaService.Add(projectRoot, "기준", string.Empty, "normal");
        ProjectStateService projectStateService = new ProjectStateService(new JsonProjectStateStore());
        FileRollingContextStore rollingContextStore = new FileRollingContextStore(root);
        ArtifactService artifactService = new ArtifactService(new FileArtifactStore());
        ContextBuilder contextBuilder = new ContextBuilder(
            criteriaService,
            projectStateService,
            rollingContextStore,
            new FileSystemRuleStore(),
            new FileArtifactRuleStore(),
            new FileImportanceRuleStore(root),
            artifactService);
        FetchUrlTool fetchTool = new FetchUrlTool(
            new HttpClient(new FakeHttpMessageHandler("<html><body>Hello World</body></html>", "text/html", 200)));
        ScriptedChatProvider provider = new ScriptedChatProvider(
            "{\"type\":\"tool_request\",\"tool\":\"fetch_url\",\"arguments\":{\"url\":\"https://example.com\"}}",
            "요약: Hello World");
        ChatService chatService = new ChatService(
            new JsonProviderSettingsStore(),
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = provider
            },
            new CompositeConversationLogStore(
            [
                new JsonlConversationLogStore(),
                new SqliteConversationLogStore()
            ]),
            contextBuilder,
            rollingContextStore,
            artifactService,
            new JsonFileProjectStore(root),
            new AgentToolHost([fetchTool]));

        ChatProviderResponse response = chatService.SendAsync(
            projectRoot,
            "이 URL 요약: https://example.com",
            CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AssertEqual(1, response.ToolResults.Count, "fetch_url should run once during chat.");
        AssertContains(response.Content, "Hello World");
    }

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
            artifactService,
            projectStore,
            new AgentToolHost([]));
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
            artifactService,
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = new FakeChatProvider()
            },
            new AgentToolHost([]),
            new CompositeConversationLogStore(
            [
                new JsonlConversationLogStore(),
                new SqliteConversationLogStore()
            ]));
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
/// <summary>
/// A scripted model client for agent loop tests.
/// </summary>
public sealed class FakeChatModelClient : IChatModelClient
{
    /// <summary>
    /// The scripted responses.
    /// </summary>
    private readonly IReadOnlyList<string> responses;

    /// <summary>
    /// The current response index.
    /// </summary>
    private int index;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeChatModelClient"/> class.
    /// </summary>
    /// <param name="responses">The scripted responses (last one repeats).</param>
    public FakeChatModelClient(params string[] responses)
    {
        this.responses = responses;
    }

    /// <summary>
    /// Returns the next scripted response.
    /// </summary>
    /// <param name="request">The model request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model response.</returns>
    public Task<ChatModelResponse> CompleteAsync(ChatModelRequest request, CancellationToken cancellationToken)
    {
        string content = responses.Count == 0
            ? string.Empty
            : responses[Math.Min(index, responses.Count - 1)];
        index++;
        return Task.FromResult(new ChatModelResponse { Content = content });
    }
}

/// <summary>
/// A fake agent tool used in agent loop tests.
/// </summary>
public sealed class FakeAgentTool : IAgentTool
{
    /// <summary>
    /// Gets the number of times the tool was executed.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "fake_tool";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "테스트 도구";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{}";

    /// <summary>
    /// Executes the fake tool.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(new AgentToolResult
        {
            Tool = Name,
            RequestId = request.RequestId,
            Ok = true,
            Result = new FetchUrlResult { Ok = true, Text = "fake" }
        });
    }
}

/// <summary>
/// A fake agent tool that always throws when executed.
/// </summary>
public sealed class ThrowingAgentTool : IAgentTool
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "throwing_tool";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "항상 실패하는 테스트 도구";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{}";

    /// <summary>
    /// Always throws to simulate a tool execution failure.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Never returns; always throws.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("도구 실행 실패");
    }
}

/// <summary>
/// A fake HTTP handler returning a fixed response.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    /// The response body.
    /// </summary>
    private readonly string body;

    /// <summary>
    /// The response content type.
    /// </summary>
    private readonly string contentType;

    /// <summary>
    /// The response status code.
    /// </summary>
    private readonly int statusCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeHttpMessageHandler"/> class.
    /// </summary>
    /// <param name="body">The response body.</param>
    /// <param name="contentType">The response content type.</param>
    /// <param name="statusCode">The response status code.</param>
    public FakeHttpMessageHandler(string body, string contentType, int statusCode)
    {
        this.body = body;
        this.contentType = contentType;
        this.statusCode = statusCode;
    }

    /// <summary>
    /// Returns the fixed response.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        StringContent content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        HttpResponseMessage response = new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            Content = content
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// A fake HTTP handler that always throws (verifies tool failure handling).
/// </summary>
public sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    /// Throws to simulate a network failure.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Never returns.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("network blocked in test");
    }
}

/// <summary>
/// A scripted chat provider for in-chat tool integration tests.
/// </summary>
public sealed class ScriptedChatProvider : IChatProvider
{
    /// <summary>
    /// The scripted responses.
    /// </summary>
    private readonly IReadOnlyList<string> responses;

    /// <summary>
    /// The current response index.
    /// </summary>
    private int index;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptedChatProvider"/> class.
    /// </summary>
    /// <param name="responses">The scripted responses (last one repeats).</param>
    public ScriptedChatProvider(params string[] responses)
    {
        this.responses = responses;
    }

    /// <summary>
    /// Returns the next scripted response.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider response.</returns>
    public Task<ChatProviderResponse> SendAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        CancellationToken cancellationToken)
    {
        string content = responses.Count == 0
            ? string.Empty
            : responses[Math.Min(index, responses.Count - 1)];
        index++;
        return Task.FromResult(new ChatProviderResponse
        {
            Content = content,
            Provider = settings.Name,
            Model = settings.Model
        });
    }

    /// <summary>
    /// Streams the next scripted response as a single chunk.
    /// </summary>
    /// <param name="request">The provider request.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The streamed response.</returns>
    public async IAsyncEnumerable<string> StreamAsync(
        ChatProviderRequest request,
        ProviderSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatProviderResponse response = await SendAsync(request, settings, cancellationToken);
        yield return response.Content;
    }
}

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
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
