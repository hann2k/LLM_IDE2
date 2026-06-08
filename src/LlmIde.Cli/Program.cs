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
using System.Net.Http;
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
        DeepSeekChatProvider deepSeekProvider = new DeepSeekChatProvider(new HttpClient());
        ProjectRegistryService projectRegistryService = new ProjectRegistryService(new JsonProjectRegistryStore(ideProgramRoot));
        ProjectInitializer projectInitializer = new ProjectInitializer(projectStore, projectRegistryService, ideProgramRoot);
        HttpClient fetchHttpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        IAgentToolHost agentToolHost = AgentToolHostFactory.Create(ideProgramRoot, fetchHttpClient);
        IConversationLogStore conversationLogStore = new CompositeConversationLogStore(
        [
            new JsonlConversationLogStore(),
            new SqliteConversationLogStore()
        ]);
        ChatService chatService = new ChatService(
            providerSettingsStore,
            new Dictionary<string, IChatProvider>
            {
                ["deepseek"] = deepSeekProvider
            },
            conversationLogStore,
            contextBuilder,
            rollingContextStore,
            artifactService,
            projectStore,
            agentToolHost);
        ProviderSettingsService providerSettingsService = new ProviderSettingsService(
            providerSettingsStore,
            new Dictionary<string, IModelProvider>
            {
                ["deepseek"] = deepSeekProvider
            });
        Dictionary<string, IChatProvider> chatProviders = new Dictionary<string, IChatProvider>
        {
            ["deepseek"] = deepSeekProvider
        };
        CliApplication application = new CliApplication(
            projectStore,
            projectInitializer,
            projectRegistryService,
            chatService,
            providerSettingsStore,
            providerSettingsService,
            criteriaService,
            projectStateService,
            artifactService,
            chatProviders,
            agentToolHost,
            conversationLogStore);

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
    /// The artifact service.
    /// </summary>
    private readonly ArtifactService artifactService;

    /// <summary>
    /// The chat providers by name.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IChatProvider> chatProviders;

    /// <summary>
    /// The agent tool host.
    /// </summary>
    private readonly IAgentToolHost agentToolHost;

    /// <summary>
    /// The conversation log store.
    /// </summary>
    private readonly IConversationLogStore conversationLogStore;

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
    /// <param name="artifactService">The artifact service.</param>
    /// <param name="chatProviders">The chat providers by name.</param>
    /// <param name="agentToolHost">The agent tool host.</param>
    /// <param name="conversationLogStore">The conversation log store.</param>
    public CliApplication(
        IProjectStore projectStore,
        ProjectInitializer projectInitializer,
        ProjectRegistryService projectRegistryService,
        ChatService chatService,
        IProviderSettingsStore providerSettingsStore,
        ProviderSettingsService providerSettingsService,
        CriteriaService criteriaService,
        ProjectStateService projectStateService,
        ArtifactService artifactService,
        IReadOnlyDictionary<string, IChatProvider> chatProviders,
        IAgentToolHost agentToolHost,
        IConversationLogStore conversationLogStore)
    {
        this.projectStore = projectStore;
        this.projectInitializer = projectInitializer;
        this.projectRegistryService = projectRegistryService;
        this.chatService = chatService;
        this.providerSettingsStore = providerSettingsStore;
        this.providerSettingsService = providerSettingsService;
        this.criteriaService = criteriaService;
        this.projectStateService = projectStateService;
        this.artifactService = artifactService;
        this.chatProviders = chatProviders;
        this.agentToolHost = agentToolHost;
        this.conversationLogStore = conversationLogStore;
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
            Console.Error.WriteLine($"오류: {ex.Message}");
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

        if (command == "artifacts")
        {
            return RunArtifacts(args);
        }

        if (command == "agent-run")
        {
            return RunAgent(args);
        }

        PrintUsage();
        return 1;
    }

    /// <summary>
    /// Runs the agent loop for one user input.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunAgent(string[] args)
    {
        if (args.Length < 3)
        {
            PrintAgentUsage();
            return 1;
        }

        string projectName = args[1];
        string message = string.Join(' ', args.Skip(2));

        if (string.IsNullOrWhiteSpace(message))
        {
            PrintAgentUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(projectName);
        ProviderSettings settings = GetDefaultProviderSettings(providerSettingsStore.Load(project.Path));

        if (!chatProviders.TryGetValue(settings.Name, out IChatProvider? provider))
        {
            throw new InvalidOperationException($"Provider is not available: {settings.Name}");
        }

        IChatModelClient client = new DeepSeekChatModelClient(provider, settings);
        AgentLoop loop = new AgentLoop(client, agentToolHost);
        AgentRunResult result = loop.RunAsync(new AgentRunRequest { UserInput = message }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        LogAgentToolCalls(project.Path, settings, result);
        PrintAgentResult(result);
        return result.IsSuccess ? 0 : 1;
    }

    /// <summary>
    /// Persists every tool execution from an agent run to the conversation tool call log.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="settings">The provider settings used for the run.</param>
    /// <param name="result">The agent run result.</param>
    private void LogAgentToolCalls(string projectRoot, ProviderSettings settings, AgentRunResult result)
    {
        if (result.ToolResults.Count == 0)
        {
            return;
        }

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        string requestId = ConversationSequence.ToId(conversationLogStore.GetNextRequestSequence(projectRoot));

        // Record the agent run as a request so the tool call ids are traceable and unique.
        conversationLogStore.AppendRequest(projectRoot, new ConversationRequestRecord
        {
            RequestId = requestId,
            RequestType = "agent",
            Provider = settings.Name,
            Model = settings.Model,
            Status = result.IsSuccess ? "completed" : "failed",
            Error = result.ErrorMessage,
            CreatedAt = createdAt
        });

        int sequence = 0;

        foreach (AgentToolResult toolResult in result.ToolResults)
        {
            sequence++;
            conversationLogStore.AppendToolCall(
                projectRoot,
                ConversationToolCallRecord.Create(requestId, sequence, toolResult, createdAt));
        }
    }

    /// <summary>
    /// Prints the agent run result. Only host names are printed, never full URLs.
    /// </summary>
    /// <param name="result">The agent run result.</param>
    private static void PrintAgentResult(AgentRunResult result)
    {
        foreach (AgentToolResult toolResult in result.ToolResults)
        {
            string info = string.Empty;

            if (toolResult.Result is FetchUrlResult fetchResult
                && Uri.TryCreate(fetchResult.Url, UriKind.Absolute, out Uri? uri))
            {
                info = "host=" + uri.Host;
            }
            else if (toolResult.Result is WebSearchResult searchResult)
            {
                info = "query=" + searchResult.Query;
            }

            Console.WriteLine($"[도구] {toolResult.Tool} {info} ok={toolResult.Ok}");
        }

        Console.WriteLine($"stop_reason: {result.StopReason}");

        if (result.IsSuccess)
        {
            Console.WriteLine(result.FinalText);
        }
        else
        {
            Console.WriteLine($"오류: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// Gets the default provider settings.
    /// </summary>
    /// <param name="settingsDocument">The settings document.</param>
    /// <returns>The default provider settings.</returns>
    private static ProviderSettings GetDefaultProviderSettings(ProviderSettingsDocument settingsDocument)
    {
        ProviderSettings? settings = settingsDocument.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Name, settingsDocument.DefaultProvider, StringComparison.OrdinalIgnoreCase));

        if (settings is null)
        {
            throw new InvalidOperationException($"Provider settings are missing: {settingsDocument.DefaultProvider}");
        }

        return settings;
    }

    /// <summary>
    /// Prints agent command usage.
    /// </summary>
    private static void PrintAgentUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide agent-run <project-id> <message>");
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
        string action = result.Created ? "초기화됨" : "열림";

        Console.WriteLine($"{action}: {result.ProjectRoot}");
        Console.WriteLine($"프로젝트: {result.ProjectInfo.Title}");
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
        ParsedChatArguments chatArguments = ParseChatArguments(args);
        string message = chatArguments.Message;

        if (string.IsNullOrWhiteSpace(message))
        {
            PrintChatUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(projectName);

        if (noStream)
        {
            ChatProviderResponse response = chatService.SendAsync(
                project.Path,
                message,
                CancellationToken.None,
                chatArguments.ArtifactIds,
                debug ? PrintChatDebugWithResponse : null,
                debug ? PrintCompressionDebugStart : null,
                debug ? Console.Write : null)
                .GetAwaiter()
                .GetResult();

            if (debug)
            {
                PrintCompressionDebugEnd(response);
                PrintArtifactCandidates(response);
            }
            else
            {
                Console.WriteLine(response.Content);
            }

            return 0;
        }

        ChatProviderResponse streamedResponse = chatService.StreamAsync(
            project.Path,
            message,
            debug ? PrintChatDebug : null,
            Console.Write,
            CancellationToken.None,
            chatArguments.ArtifactIds,
            debug ? PrintCompressionDebugStart : null,
            debug ? Console.Write : null).GetAwaiter().GetResult();

        if (debug)
        {
            PrintCompressionDebugEnd(streamedResponse);
            PrintArtifactCandidates(streamedResponse);
        }
        else
        {
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// Determines whether an argument belongs to the user chat message.
    /// </summary>
    /// <param name="arg">The argument.</param>
    /// <returns>True when the argument is part of the user message.</returns>
    private static ParsedChatArguments ParseChatArguments(string[] args)
    {
        List<string> messageParts = [];
        List<string> artifactIds = [];

        for (int index = 2; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "--debug", StringComparison.Ordinal)
                || string.Equals(arg, "--no-stream", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(arg, "--artifact", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length)
                {
                    throw new InvalidOperationException("--artifact 옵션에는 산출물 ID가 필요합니다.");
                }

                artifactIds.Add(args[++index]);
                continue;
            }

            messageParts.Add(arg);
        }

        return new ParsedChatArguments(string.Join(' ', messageParts), artifactIds);
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
        Console.WriteLine("디버그:");
        Console.WriteLine(JsonSerializer.Serialize(debugView, JsonOptions.Default));
        Console.WriteLine("응답:");
    }

    /// <summary>
    /// Prints chat debug data and the completed non-streamed chat response.
    /// </summary>
    /// <param name="response">The chat response.</param>
    private static void PrintChatDebugWithResponse(ChatProviderResponse response)
    {
        PrintChatDebug(response);
        Console.WriteLine(response.Content);
    }

    /// <summary>
    /// Prints the compression provider request used after a chat command.
    /// </summary>
    /// <param name="response">The chat response.</param>
    private static void PrintCompressionDebug(ChatProviderResponse response)
    {
        if (response.CompressionRequest is null)
        {
            return;
        }

        CompressionDebugView debugView = new CompressionDebugView
        {
            RequestId = response.CompressionRequestId,
            Status = response.CompressionStatus,
            Error = response.CompressionError,
            Model = response.CompressionRequest.Model,
            Messages = response.CompressionRequest.Messages,
            Response = response.CompressionContent
        };

        Console.WriteLine("압축_디버그:");
        Console.WriteLine(JsonSerializer.Serialize(debugView, JsonOptions.Default));
    }

    /// <summary>
    /// Prints the compression provider request before streaming compression starts.
    /// </summary>
    /// <param name="response">The compression preview response.</param>
    private static void PrintCompressionDebugStart(ChatProviderResponse response)
    {
        if (response.CompressionRequest is null)
        {
            return;
        }

        CompressionDebugView debugView = new CompressionDebugView
        {
            RequestId = response.CompressionRequestId,
            Status = response.CompressionStatus,
            Error = response.CompressionError,
            Model = response.CompressionRequest.Model,
            Messages = response.CompressionRequest.Messages,
            Response = string.Empty
        };

        Console.WriteLine();
        Console.WriteLine("압축_디버그:");
        Console.WriteLine(JsonSerializer.Serialize(debugView, JsonOptions.Default));
        Console.WriteLine("압축_응답:");
    }

    /// <summary>
    /// Prints the compression result after streamed compression finishes.
    /// </summary>
    /// <param name="response">The completed chat response.</param>
    private static void PrintCompressionDebugEnd(ChatProviderResponse response)
    {
        if (response.CompressionRequest is null)
        {
            Console.WriteLine();
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"압축_상태: {response.CompressionStatus}");

        if (!string.IsNullOrWhiteSpace(response.CompressionError))
        {
            Console.WriteLine($"압축_오류: {response.CompressionError}");
        }
    }

    /// <summary>
    /// Prints artifact candidates found in the chat response.
    /// </summary>
    /// <param name="response">The completed chat response.</param>
    private static void PrintArtifactCandidates(ChatProviderResponse response)
    {
        if (response.ArtifactCandidates.Count == 0)
        {
            return;
        }

        Console.WriteLine("artifact_candidates:");
        Console.WriteLine(JsonSerializer.Serialize(response.ArtifactCandidates, JsonOptions.Default));
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
        Console.WriteLine(removed ? "삭제됨" : "찾을 수 없음");
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
        Console.WriteLine($"활성화됨: {args[3]}");
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
        Console.WriteLine($"비활성화됨: {args[3]}");
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

            throw new InvalidOperationException($"알 수 없는 기준 옵션입니다: {option}");
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
    /// Runs the artifacts command group.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunArtifacts(string[] args)
    {
        if (args.Length < 3)
        {
            PrintArtifactsUsage();
            return 1;
        }

        string subCommand = args[1].Trim().ToLowerInvariant();

        if (subCommand == "list")
        {
            return RunArtifactsList(args);
        }

        if (subCommand == "show")
        {
            return RunArtifactsShow(args);
        }

        if (subCommand == "add")
        {
            return RunArtifactsAdd(args);
        }

        if (subCommand == "update")
        {
            return RunArtifactsUpdate(args);
        }

        if (subCommand == "remove")
        {
            return RunArtifactsRemove(args);
        }

        if (subCommand == "extract")
        {
            return RunArtifactsExtract(args);
        }

        PrintArtifactsUsage();
        return 1;
    }

    private int RunArtifactsList(string[] args)
    {
        if (args.Length != 3)
        {
            PrintArtifactsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        IReadOnlyList<Artifact> artifacts = artifactService.List(project.Path);

        if (artifacts.Count == 0)
        {
            Console.WriteLine("산출물이 없습니다.");
            return 0;
        }

        foreach (Artifact artifact in artifacts)
        {
            Console.WriteLine($"{artifact.ArtifactId}  {artifact.Type}  {artifact.Title}  {artifact.ContentPath}");
        }

        return 0;
    }

    private int RunArtifactsShow(string[] args)
    {
        if (args.Length != 4)
        {
            PrintArtifactsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        Artifact artifact = artifactService.Get(project.Path, args[3]);
        Console.WriteLine(JsonSerializer.Serialize(artifact, JsonOptions.Default));
        Console.WriteLine("내용:");
        Console.WriteLine(artifactService.ReadContent(project.Path, artifact));
        return 0;
    }

    private int RunArtifactsAdd(string[] args)
    {
        if (args.Length < 5)
        {
            PrintArtifactsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        ArtifactOptions options = ParseArtifactOptions(args.Skip(3).ToArray(), requireContent: true);
        Artifact artifact = artifactService.Save(project.Path, CreateArtifactCandidate(options), options.SourceRequestId ?? string.Empty);
        Console.WriteLine($"산출물: {artifact.ArtifactId}");
        return 0;
    }

    private int RunArtifactsUpdate(string[] args)
    {
        if (args.Length < 5)
        {
            PrintArtifactsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        Artifact existing = artifactService.Get(project.Path, args[3]);
        ArtifactOptions options = ParseArtifactOptions(args.Skip(4).ToArray(), requireContent: false);
        ArtifactCandidate candidate = CreateArtifactCandidate(options, existing, artifactService.ReadContent(project.Path, existing));
        Artifact artifact = artifactService.Update(project.Path, args[3], candidate);
        Console.WriteLine($"산출물: {artifact.ArtifactId}");
        return 0;
    }

    private int RunArtifactsRemove(string[] args)
    {
        if (args.Length != 4)
        {
            PrintArtifactsUsage();
            return 1;
        }

        ProjectRegistryEntry project = projectRegistryService.GetRequired(args[2]);
        artifactService.Remove(project.Path, args[3]);
        Console.WriteLine($"삭제됨: {args[3]}");
        return 0;
    }

    private int RunArtifactsExtract(string[] args)
    {
        if (args.Length < 5)
        {
            PrintArtifactsUsage();
            return 1;
        }

        projectRegistryService.GetRequired(args[2]);
        ArtifactOptions options = ParseArtifactOptions(args.Skip(3).ToArray(), requireContent: true);
        IReadOnlyList<ArtifactCandidate> candidates = artifactService.ExtractCandidates(options.Content ?? string.Empty);
        Console.WriteLine(JsonSerializer.Serialize(candidates, JsonOptions.Default));
        return 0;
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
        Console.WriteLine($"추가됨: {listName}");
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
        Console.WriteLine(removed ? $"삭제됨: {listName}" : "찾을 수 없음");
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

            throw new InvalidOperationException($"알 수 없는 상태 옵션입니다: {option}");
        }

        if (!options.HasAnyValue)
        {
            throw new InvalidOperationException("상태 옵션을 하나 이상 지정해야 합니다.");
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

    private static ArtifactOptions ParseArtifactOptions(string[] args, bool requireContent)
    {
        ArtifactOptions options = new ArtifactOptions();
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

            if (option == "--type")
            {
                options.Type = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            if (option == "--path")
            {
                options.TargetPath = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            if (option == "--content")
            {
                options.Content = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            if (option == "--file")
            {
                string filePath = ReadOptionValue(args, index, option);
                options.Content = File.ReadAllText(filePath);
                index += 2;
                continue;
            }

            if (option == "--source-request")
            {
                options.SourceRequestId = ReadOptionValue(args, index, option);
                index += 2;
                continue;
            }

            throw new InvalidOperationException($"알 수 없는 산출물 옵션입니다: {option}");
        }

        if (requireContent && string.IsNullOrWhiteSpace(options.Content))
        {
            throw new InvalidOperationException("산출물 내용이 필요합니다. --content 또는 --file을 사용하세요.");
        }

        return options;
    }

    private static ArtifactCandidate CreateArtifactCandidate(ArtifactOptions options)
    {
        return CreateArtifactCandidate(options, null, string.Empty);
    }

    private static ArtifactCandidate CreateArtifactCandidate(
        ArtifactOptions options,
        Artifact? existing,
        string existingContent)
    {
        return new ArtifactCandidate
        {
            Title = options.Title ?? existing?.Title ?? string.Empty,
            Type = options.Type ?? existing?.Type ?? string.Empty,
            TargetPath = options.TargetPath ?? existing?.TargetPath ?? string.Empty,
            Content = options.Content ?? existingContent
        };
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

        if (subCommand == "repid")
        {
            return RunProjectsRePId(args);
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
            Console.WriteLine("프로젝트가 없습니다.");
            return 0;
        }

        for (int index = 0; index < projects.Count; index++)
        {
            ProjectRegistryEntry project = projects[index];
            Console.WriteLine($"{index + 1}. {project.PId}  {project.Name}  {project.Path}  {project.CreatedAt:O}");
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
            Console.Error.WriteLine($"오류: 초기화된 LLM IDE 프로젝트가 아닙니다: {normalizedPath}");
            return 1;
        }

        projectStore.ReadProjectInfo(normalizedPath);
        Console.WriteLine($"열림: {normalizedPath}");
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
        Console.WriteLine(removed ? "삭제됨" : "찾을 수 없음");
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
            SqliteConnection.ClearAllPools();
            Directory.Delete(project.Path, true);
        }

        projectRegistryService.Remove(args[2]);
        Console.WriteLine($"deleted: {project.PId}");
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
            throw new InvalidOperationException("삭제하려면 --confirm <project-id> 확인 인자가 필요합니다.");
        }

        if (!string.Equals(args[4], project.PId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Delete confirmation project pID does not match.");
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
    /// Renames the user-visible project name in the registry.
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
        Console.WriteLine($"이름 변경됨: {args[2]} -> {args[3]}");
        return 0;
    }

    /// <summary>
    /// Changes a project identifier in the registry.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private int RunProjectsRePId(string[] args)
    {
        if (args.Length != 4)
        {
            PrintProjectsUsage();
            return 1;
        }

        projectRegistryService.ChangePId(args[2], args[3]);
        Console.WriteLine($"pID 변경됨: {args[2]} -> {args[3]}");
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
        Console.WriteLine($"이동됨: {args[2]} -> {Path.GetFullPath(args[3])}");
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

            if (option == "--pid")
            {
                request.PId = ReadOptionValue(args, index, option);
                request.HasExplicitPId = true;
                index += 2;
                continue;
            }

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

            throw new InvalidOperationException($"알 수 없는 초기화 옵션입니다: {option}");
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
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide init");
        Console.WriteLine("  llmide init --pid <project-id>");
        Console.WriteLine("  llmide init --pid <project-id> --name <project-name>");
        Console.WriteLine("  llmide init --path <project-path>");
        Console.WriteLine("  llmide init --pid <project-id> --name <project-name> --path <project-path>");
        Console.WriteLine("  llmide init --name <project-name>");
        Console.WriteLine("  llmide init -p <project-path>");
        Console.WriteLine("  llmide init -n <project-name>");
        Console.WriteLine("  llmide init --pid <project-id> -n <project-name> -p <project-path>");
        Console.WriteLine("  llmide projects list");
        Console.WriteLine("  llmide projects open <project-id>");
        Console.WriteLine("  llmide projects remove <project-id>");
        Console.WriteLine("  llmide projects delete <project-id> --confirm <project-id>");
        Console.WriteLine("  llmide projects delete <project-id> --confirm <project-id> --confirm-api-key-delete");
        Console.WriteLine("  llmide projects rename <project-id> <new-project-name>");
        Console.WriteLine("  llmide projects repid <project-id> <new-project-id>");
        Console.WriteLine("  llmide projects move <project-id> <new-project-path>");
        Console.WriteLine("  llmide models list <project-id>");
        Console.WriteLine("  llmide models set <project-id> <model>");
        Console.WriteLine("  llmide criteria list <project-id>");
        Console.WriteLine("  llmide criteria add <project-id> --title <title> --description <description>");
        Console.WriteLine("  llmide criteria add <project-id> --title <title> --description <description> --priority <priority>");
        Console.WriteLine("  llmide criteria update <project-id> <criterion-id> --title <title>");
        Console.WriteLine("  llmide criteria update <project-id> <criterion-id> --description <description>");
        Console.WriteLine("  llmide criteria update <project-id> <criterion-id> --priority <priority>");
        Console.WriteLine("  llmide criteria remove <project-id> <criterion-id>");
        Console.WriteLine("  llmide criteria activate <project-id> <criterion-id>");
        Console.WriteLine("  llmide criteria deactivate <project-id> <criterion-id>");
        Console.WriteLine("  llmide state show <project-id>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage>");
        Console.WriteLine("  llmide state set <project-id> --current-task <task>");
        Console.WriteLine("  llmide state set <project-id> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage> --current-task <task>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-id> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state add <project-id> completed <item>");
        Console.WriteLine("  llmide state add <project-id> in-progress <item>");
        Console.WriteLine("  llmide state add <project-id> next-action <item>");
        Console.WriteLine("  llmide state add <project-id> blocker <item>");
        Console.WriteLine("  llmide state remove <project-id> completed <item>");
        Console.WriteLine("  llmide state remove <project-id> in-progress <item>");
        Console.WriteLine("  llmide state remove <project-id> next-action <item>");
        Console.WriteLine("  llmide state remove <project-id> blocker <item>");
        Console.WriteLine("  llmide artifacts list <project-id>");
        Console.WriteLine("  llmide artifacts show <project-id> <artifact-id>");
        Console.WriteLine("  llmide artifacts add <project-id> --title <title> --type <type> --content <content>");
        Console.WriteLine("  llmide artifacts add <project-id> --title <title> --type <type> --file <file-path>");
        Console.WriteLine("  llmide artifacts update <project-id> <artifact-id> --title <title>");
        Console.WriteLine("  llmide artifacts update <project-id> <artifact-id> --content <content>");
        Console.WriteLine("  llmide artifacts remove <project-id> <artifact-id>");
        Console.WriteLine("  llmide artifacts extract <project-id> --content <response-text>");
        Console.WriteLine("  llmide chat <project-id> <message>");
        Console.WriteLine("  llmide chat <project-id> <message> --artifact <artifact-id>");
        Console.WriteLine("  llmide chat <project-id> <message> --debug");
        Console.WriteLine("  llmide chat <project-id> <message> --no-stream");
        Console.WriteLine("  llmide chat <project-id> <message> --debug --no-stream");
        Console.WriteLine("  llmide agent-run <project-id> <message>");
    }

    /// <summary>
    /// Prints projects command usage.
    /// </summary>
    private static void PrintProjectsUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide projects list");
        Console.WriteLine("  llmide projects open <project-id>");
        Console.WriteLine("  llmide projects remove <project-id>");
        Console.WriteLine("  llmide projects delete <project-id> --confirm <project-id>");
        Console.WriteLine("  llmide projects delete <project-id> --confirm <project-id> --confirm-api-key-delete");
        Console.WriteLine("  llmide projects rename <project-id> <new-project-name>");
        Console.WriteLine("  llmide projects repid <project-id> <new-project-id>");
        Console.WriteLine("  llmide projects move <project-id> <new-project-path>");
    }

    /// <summary>
    /// Prints models command usage.
    /// </summary>
    private static void PrintModelsUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide models list <project-id>");
        Console.WriteLine("  llmide models set <project-id> <model>");
    }

    /// <summary>
    /// Prints criteria command usage.
    /// </summary>
    private static void PrintCriteriaUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide criteria list <project-id>");
        Console.WriteLine("  llmide criteria add <project-id> --title <title> --description <description>");
        Console.WriteLine("  llmide criteria add <project-id> --title <title> --description <description> --priority <priority>");
        Console.WriteLine("  llmide criteria update <project-id> <criterion-id> --title <title>");
        Console.WriteLine("  llmide criteria update <project-id> <criterion-id> --description <description>");
        Console.WriteLine("  llmide criteria update <project-id> <criterion-id> --priority <priority>");
        Console.WriteLine("  llmide criteria remove <project-id> <criterion-id>");
        Console.WriteLine("  llmide criteria activate <project-id> <criterion-id>");
        Console.WriteLine("  llmide criteria deactivate <project-id> <criterion-id>");
    }

    /// <summary>
    /// Prints state command usage.
    /// </summary>
    private static void PrintStateUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide state show <project-id>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage>");
        Console.WriteLine("  llmide state set <project-id> --current-task <task>");
        Console.WriteLine("  llmide state set <project-id> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage> --current-task <task>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-id> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state set <project-id> --stage <stage> --current-task <task> --last-decision <decision>");
        Console.WriteLine("  llmide state add <project-id> completed <item>");
        Console.WriteLine("  llmide state add <project-id> in-progress <item>");
        Console.WriteLine("  llmide state add <project-id> next-action <item>");
        Console.WriteLine("  llmide state add <project-id> blocker <item>");
        Console.WriteLine("  llmide state remove <project-id> completed <item>");
        Console.WriteLine("  llmide state remove <project-id> in-progress <item>");
        Console.WriteLine("  llmide state remove <project-id> next-action <item>");
        Console.WriteLine("  llmide state remove <project-id> blocker <item>");
    }

    /// <summary>
    /// Prints chat command usage.
    /// </summary>
    private static void PrintChatUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide chat <project-id> <message>");
        Console.WriteLine("  llmide chat <project-id> <message> --artifact <artifact-id>");
        Console.WriteLine("  llmide chat <project-id> <message> --debug");
        Console.WriteLine("  llmide chat <project-id> <message> --no-stream");
        Console.WriteLine("  llmide chat <project-id> <message> --debug --no-stream");
    }

    /// <summary>
    /// Prints artifacts command usage.
    /// </summary>
    private static void PrintArtifactsUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  llmide artifacts list <project-id>");
        Console.WriteLine("  llmide artifacts show <project-id> <artifact-id>");
        Console.WriteLine("  llmide artifacts add <project-id> --title <title> --type <type> --content <content>");
        Console.WriteLine("  llmide artifacts add <project-id> --title <title> --type <type> --file <file-path>");
        Console.WriteLine("  llmide artifacts add <project-id> --title <title> --type <type> --path <target-path> --content <content>");
        Console.WriteLine("  llmide artifacts update <project-id> <artifact-id> --title <title>");
        Console.WriteLine("  llmide artifacts update <project-id> <artifact-id> --type <type>");
        Console.WriteLine("  llmide artifacts update <project-id> <artifact-id> --path <target-path>");
        Console.WriteLine("  llmide artifacts update <project-id> <artifact-id> --content <content>");
        Console.WriteLine("  llmide artifacts remove <project-id> <artifact-id>");
        Console.WriteLine("  llmide artifacts extract <project-id> --content <response-text>");
        Console.WriteLine("  llmide artifacts extract <project-id> --file <response-file>");
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
/// Describes the compression debug output.
/// </summary>
public sealed class CompressionDebugView
{
    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression error.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sent messages.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the compression response.
    /// </summary>
    public string Response { get; set; } = string.Empty;
}

/// <summary>
/// Describes parsed chat command arguments.
/// </summary>
/// <param name="Message">The user message.</param>
/// <param name="ArtifactIds">The artifact identifiers to attach.</param>
public sealed record ParsedChatArguments(string Message, IReadOnlyList<string> ArtifactIds);

/// <summary>
/// Describes parsed artifact command options.
/// </summary>
public sealed class ArtifactOptions
{
    /// <summary>
    /// Gets or sets the artifact title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the artifact type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the optional intended target path.
    /// </summary>
    public string? TargetPath { get; set; }

    /// <summary>
    /// Gets or sets the artifact content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the optional source request identifier.
    /// </summary>
    public string? SourceRequestId { get; set; }
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
