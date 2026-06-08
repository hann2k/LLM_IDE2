using LlmIde.Core.Artifacts;
using LlmIde.Core.Projects;
using LlmIde.Core.Providers;
using System.Text;

namespace LlmIde.Core.Conversations;

/// <summary>
/// Builds the context sent to a provider request.
/// </summary>
public sealed class ContextBuilder
{
    /// <summary>
    /// The criteria service.
    /// </summary>
    private readonly CriteriaService criteriaService;

    /// <summary>
    /// The project state service.
    /// </summary>
    private readonly ProjectStateService projectStateService;

    /// <summary>
    /// The rolling context store.
    /// </summary>
    private readonly IRollingContextStore rollingContextStore;

    /// <summary>
    /// The system rule store.
    /// </summary>
    private readonly ISystemRuleStore systemRuleStore;

    /// <summary>
    /// The artifact rule store.
    /// </summary>
    private readonly IArtifactRuleStore artifactRuleStore;

    /// <summary>
    /// The importance rule store.
    /// </summary>
    private readonly IImportanceRuleStore importanceRuleStore;

    /// <summary>
    /// The artifact service.
    /// </summary>
    private readonly ArtifactService artifactService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextBuilder"/> class.
    /// </summary>
    /// <param name="criteriaService">The criteria service.</param>
    /// <param name="projectStateService">The project state service.</param>
    /// <param name="rollingContextStore">The rolling context store.</param>
    /// <param name="systemRuleStore">The system rule store.</param>
    /// <param name="artifactRuleStore">The artifact rule store.</param>
    /// <param name="importanceRuleStore">The importance rule store.</param>
    /// <param name="artifactService">The artifact service.</param>
    public ContextBuilder(
        CriteriaService criteriaService,
        ProjectStateService projectStateService,
        IRollingContextStore rollingContextStore,
        ISystemRuleStore systemRuleStore,
        IArtifactRuleStore artifactRuleStore,
        IImportanceRuleStore importanceRuleStore,
        ArtifactService artifactService)
    {
        this.criteriaService = criteriaService;
        this.projectStateService = projectStateService;
        this.rollingContextStore = rollingContextStore;
        this.systemRuleStore = systemRuleStore;
        this.artifactRuleStore = artifactRuleStore;
        this.importanceRuleStore = importanceRuleStore;
        this.artifactService = artifactService;
    }

    /// <summary>
    /// Builds a context package and provider messages.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="message">The current user message.</param>
    /// <param name="artifactIds">The artifact identifiers to attach.</param>
    /// <param name="includeHistory">Whether to inject past context (rolling summary).</param>
    /// <param name="recentMessages">The recent conversation messages to include as a window.</param>
    /// <returns>The built context.</returns>
    public ContextBuildResult Build(
        string projectRoot,
        string requestId,
        string message,
        IReadOnlyList<string>? artifactIds = null,
        bool includeHistory = true,
        IReadOnlyList<ConversationMessageRecord>? recentMessages = null)
    {
        rollingContextStore.EnsureInitialized(projectRoot);

        string systemRule = systemRuleStore.Load(projectRoot);
        string artifactRule = artifactRuleStore.Load(projectRoot);
        string importanceRule = importanceRuleStore.Load(projectRoot);
        IReadOnlyList<Criterion> activeCriteria = criteriaService.ListActive(projectRoot);
        ProjectState projectState = projectStateService.Get(projectRoot);

        // One-time conversations do not inject past context.
        RollingContextSummary? rollingContext = includeHistory ? rollingContextStore.LoadCurrent(projectRoot) : null;

        if (activeCriteria.Count == 0)
        {
            throw new InvalidOperationException("활성 기준이 없습니다. 대화 전에 기준을 하나 이상 추가하거나 활성화하세요.");
        }

        IReadOnlyList<ConversationMessageRecord> recentList = recentMessages ?? [];
        ContextPackage contextPackage = CreateContextPackage(
            requestId,
            message,
            systemRule,
            artifactRule,
            importanceRule,
            activeCriteria,
            projectState,
            rollingContext,
            LoadAttachedArtifacts(projectRoot, artifactIds ?? []),
            recentList);

        return new ContextBuildResult
        {
            ContextPackage = contextPackage,
            Messages = BuildProviderMessages(contextPackage, recentList)
        };
    }

    /// <summary>
    /// Builds provider messages from a context package.
    /// </summary>
    /// <param name="contextPackage">The context package.</param>
    /// <param name="recentMessages">The recent conversation window messages.</param>
    /// <returns>The provider messages.</returns>
    private static List<ChatMessage> BuildProviderMessages(
        ContextPackage contextPackage,
        IReadOnlyList<ConversationMessageRecord> recentMessages)
    {
        List<ChatMessage> messages = [];
        AppendSystemMessage(messages, contextPackage.SystemRule);
        AppendSystemMessage(messages, BuildCriteriaMessage(contextPackage.ActiveCriteria));
        AppendSystemMessage(messages, BuildProjectStateMessage((ProjectState)contextPackage.ProjectState));
        AppendSystemMessage(messages, BuildRollingContextMessage(contextPackage.RollingContextSummary));
        AppendSystemMessage(messages, contextPackage.ArtifactRule);
        AppendSystemMessage(messages, contextPackage.ImportanceRule);
        AppendSystemMessage(messages, BuildAttachedArtifactsMessage(contextPackage.AttachedArtifacts));

        // Include the recent conversation window as raw turns before the current request.
        foreach (ConversationMessageRecord recent in recentMessages)
        {
            messages.Add(new ChatMessage
            {
                Role = recent.Role,
                Content = recent.Content
            });
        }

        messages.Add(new ChatMessage
        {
            Role = "user",
            Content = contextPackage.UserRequest
        });

        return messages;
    }

    /// <summary>
    /// Builds the attached artifacts system message.
    /// </summary>
    /// <param name="attachedArtifacts">The attached artifacts.</param>
    /// <returns>The attached artifacts system message.</returns>
    private static string BuildAttachedArtifactsMessage(IReadOnlyList<string> attachedArtifacts)
    {
        if (attachedArtifacts.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("사용자가 첨부한 다음 산출물을 맥락으로 사용한다.");

        foreach (string artifact in attachedArtifacts)
        {
            builder.AppendLine(artifact);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Appends a system message when content exists.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <param name="content">The message content.</param>
    private static void AppendSystemMessage(List<ChatMessage> messages, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        messages.Add(new ChatMessage
        {
            Role = "system",
            Content = content
        });
    }

    /// <summary>
    /// Builds the criteria system message.
    /// </summary>
    /// <param name="activeCriteria">The active criteria strings.</param>
    /// <returns>The system message content.</returns>
    private static string BuildCriteriaMessage(IReadOnlyList<string> activeCriteria)
    {
        if (activeCriteria.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("이번 응답에서는 다음 활성 프로젝트 기준을 따른다.");

        foreach (string criterion in activeCriteria)
        {
            builder.Append("- ");
            builder.AppendLine(criterion);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds the project state system message.
    /// </summary>
    /// <param name="projectState">The project state.</param>
    /// <returns>The system message content.</returns>
    private static string BuildProjectStateMessage(ProjectState projectState)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("현재 프로젝트 상태를 맥락으로 사용한다.");
        AppendStateValue(builder, "단계", projectState.Stage);
        AppendStateValue(builder, "현재 작업", projectState.CurrentTask);
        AppendStateList(builder, "완료 항목", projectState.CompletedItems);
        AppendStateList(builder, "진행 중 항목", projectState.InProgressItems);
        AppendStateList(builder, "다음 작업", projectState.NextActions);
        AppendStateList(builder, "차단 요소", projectState.Blockers);
        AppendStateValue(builder, "마지막 결정", projectState.LastDecision);
        return builder.ToString();
    }

    /// <summary>
    /// Builds the rolling context system message.
    /// </summary>
    /// <param name="rollingContextSummary">The rolling context summary.</param>
    /// <returns>The system message content.</returns>
    private static string BuildRollingContextMessage(string rollingContextSummary)
    {
        if (string.IsNullOrWhiteSpace(rollingContextSummary))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("압축된 이전 대화 맥락을 사용한다.");
        builder.AppendLine(rollingContextSummary);
        return builder.ToString();
    }

    /// <summary>
    /// Appends a scalar project state value.
    /// </summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="label">The state label.</param>
    /// <param name="value">The state value.</param>
    private static void AppendStateValue(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append("- ");
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(value);
    }

    /// <summary>
    /// Appends a project state list.
    /// </summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="label">The state label.</param>
    /// <param name="items">The state items.</param>
    private static void AppendStateList(StringBuilder builder, string label, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        builder.Append("- ");
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(string.Join("; ", items));
    }

    /// <summary>
    /// Creates a context package.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="message">The user message.</param>
    /// <param name="systemRule">The system rule.</param>
    /// <param name="artifactRule">The artifact rule.</param>
    /// <param name="importanceRule">The importance rule.</param>
    /// <param name="activeCriteria">The active criteria.</param>
    /// <param name="projectState">The project state.</param>
    /// <param name="rollingContext">The rolling context summary.</param>
    /// <param name="attachedArtifacts">The attached artifacts.</param>
    /// <param name="recentMessages">The recent conversation window messages.</param>
    /// <returns>The context package.</returns>
    private static ContextPackage CreateContextPackage(
        string requestId,
        string message,
        string systemRule,
        string artifactRule,
        string importanceRule,
        IReadOnlyList<Criterion> activeCriteria,
        ProjectState projectState,
        RollingContextSummary? rollingContext,
        IReadOnlyList<string> attachedArtifacts,
        IReadOnlyList<ConversationMessageRecord> recentMessages)
    {
        return new ContextPackage
        {
            RequestId = requestId,
            SystemRule = systemRule,
            ArtifactRule = artifactRule,
            ImportanceRule = importanceRule,
            UserRequest = message,
            ProjectState = projectState,
            UsedRollingContextId = rollingContext?.RollingContextId ?? string.Empty,
            RollingContextPath = rollingContext?.ContentPath ?? string.Empty,
            RollingContextSummary = rollingContext?.Content ?? string.Empty,
            RecentTurnCount = recentMessages.Count,
            RecentTurns = recentMessages
                .Select(recent => $"{recent.Role}: {recent.Content}")
                .ToList(),
            AttachedArtifacts = attachedArtifacts.ToList(),
            ActiveCriteria = activeCriteria
                .Select(FormatCriterion)
                .ToList()
        };
    }

    private IReadOnlyList<string> LoadAttachedArtifacts(string projectRoot, IReadOnlyList<string> artifactIds)
    {
        if (artifactIds.Count == 0)
        {
            return [];
        }

        List<string> attachedArtifacts = [];

        foreach (string artifactId in artifactIds)
        {
            Artifact artifact = artifactService.Get(projectRoot, artifactId);
            string content = artifactService.ReadContent(projectRoot, artifact);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"[산출물: {artifact.ArtifactId}]");
            builder.AppendLine($"제목: {artifact.Title}");
            builder.AppendLine($"유형: {artifact.Type}");

            if (!string.IsNullOrWhiteSpace(artifact.TargetPath))
            {
                builder.AppendLine($"경로: {artifact.TargetPath}");
            }

            builder.AppendLine("내용:");
            builder.AppendLine(content);
            attachedArtifacts.Add(builder.ToString());
        }

        return attachedArtifacts;
    }

    /// <summary>
    /// Formats a criterion for context.
    /// </summary>
    /// <param name="criterion">The criterion.</param>
    /// <returns>The formatted criterion.</returns>
    private static string FormatCriterion(Criterion criterion)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(criterion.Title);

        if (!string.IsNullOrWhiteSpace(criterion.Priority))
        {
            builder.Append(" [");
            builder.Append(criterion.Priority);
            builder.Append(']');
        }

        if (!string.IsNullOrWhiteSpace(criterion.Description))
        {
            builder.Append(": ");
            builder.Append(criterion.Description);
        }

        return builder.ToString();
    }
}
