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
    /// Initializes a new instance of the <see cref="ContextBuilder"/> class.
    /// </summary>
    /// <param name="criteriaService">The criteria service.</param>
    /// <param name="projectStateService">The project state service.</param>
    /// <param name="rollingContextStore">The rolling context store.</param>
    /// <param name="systemRuleStore">The system rule store.</param>
    public ContextBuilder(
        CriteriaService criteriaService,
        ProjectStateService projectStateService,
        IRollingContextStore rollingContextStore,
        ISystemRuleStore systemRuleStore)
    {
        this.criteriaService = criteriaService;
        this.projectStateService = projectStateService;
        this.rollingContextStore = rollingContextStore;
        this.systemRuleStore = systemRuleStore;
    }

    /// <summary>
    /// Builds a context package and provider messages.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="message">The current user message.</param>
    /// <returns>The built context.</returns>
    public ContextBuildResult Build(string projectRoot, string requestId, string message)
    {
        rollingContextStore.EnsureInitialized(projectRoot);

        string systemRule = systemRuleStore.Load(projectRoot);
        IReadOnlyList<Criterion> activeCriteria = criteriaService.ListActive(projectRoot);
        ProjectState projectState = projectStateService.Get(projectRoot);
        RollingContextSummary? rollingContext = rollingContextStore.LoadCurrent(projectRoot);

        if (activeCriteria.Count == 0)
        {
            throw new InvalidOperationException("No active criteria. Add or activate at least one criterion before chat.");
        }

        ContextPackage contextPackage = CreateContextPackage(
            requestId,
            message,
            systemRule,
            activeCriteria,
            projectState,
            rollingContext);

        return new ContextBuildResult
        {
            ContextPackage = contextPackage,
            Messages = BuildProviderMessages(contextPackage)
        };
    }

    /// <summary>
    /// Builds provider messages from a context package.
    /// </summary>
    /// <param name="contextPackage">The context package.</param>
    /// <returns>The provider messages.</returns>
    private static List<ChatMessage> BuildProviderMessages(ContextPackage contextPackage)
    {
        List<ChatMessage> messages = [];
        AppendSystemMessage(messages, contextPackage.SystemRule);
        AppendSystemMessage(messages, BuildCriteriaMessage(contextPackage.ActiveCriteria));
        AppendSystemMessage(messages, BuildProjectStateMessage((ProjectState)contextPackage.ProjectState));
        AppendSystemMessage(messages, BuildRollingContextMessage(contextPackage.RollingContextSummary));

        messages.Add(new ChatMessage
        {
            Role = "user",
            Content = contextPackage.UserRequest
        });

        return messages;
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
        builder.AppendLine("Follow these active project criteria for this response:");

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
        builder.AppendLine("Use this current project state as context:");
        AppendStateValue(builder, "Stage", projectState.Stage);
        AppendStateValue(builder, "Current task", projectState.CurrentTask);
        AppendStateList(builder, "Completed items", projectState.CompletedItems);
        AppendStateList(builder, "In-progress items", projectState.InProgressItems);
        AppendStateList(builder, "Next actions", projectState.NextActions);
        AppendStateList(builder, "Blockers", projectState.Blockers);
        AppendStateValue(builder, "Last decision", projectState.LastDecision);
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
        builder.AppendLine("Use this compressed prior conversation context:");
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
    /// <param name="activeCriteria">The active criteria.</param>
    /// <param name="projectState">The project state.</param>
    /// <param name="rollingContext">The rolling context summary.</param>
    /// <returns>The context package.</returns>
    private static ContextPackage CreateContextPackage(
        string requestId,
        string message,
        string systemRule,
        IReadOnlyList<Criterion> activeCriteria,
        ProjectState projectState,
        RollingContextSummary? rollingContext)
    {
        return new ContextPackage
        {
            RequestId = requestId,
            SystemRule = systemRule,
            UserRequest = message,
            ProjectState = projectState,
            UsedRollingContextId = rollingContext?.RollingContextId ?? string.Empty,
            RollingContextPath = rollingContext?.ContentPath ?? string.Empty,
            RollingContextSummary = rollingContext?.Content ?? string.Empty,
            RecentTurnCount = 0,
            ActiveCriteria = activeCriteria
                .Select(FormatCriterion)
                .ToList()
        };
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
