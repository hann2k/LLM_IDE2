namespace LlmIde.Core.Projects;

/// <summary>
/// Defines the metadata paths used by an LLM IDE project.
/// </summary>
public static class LlmIdeLayout
{
    /// <summary>
    /// Gets the metadata directory name.
    /// </summary>
    public const string MetadataDirectoryName = ".llmide";

    /// <summary>
    /// Gets the project information file name.
    /// </summary>
    public const string ProjectFileName = "project.json";

    /// <summary>
    /// Gets the init progress file name.
    /// </summary>
    public const string InitProgressFileName = "init-progress.json";

    /// <summary>
    /// Gets the project state file name.
    /// </summary>
    public const string ProjectStateFileName = "project-state.json";

    /// <summary>
    /// Gets the criteria file name.
    /// </summary>
    public const string CriteriaFileName = "criteria.json";

    /// <summary>
    /// Gets the policies directory name.
    /// </summary>
    public const string PoliciesDirectoryName = "policies";

    /// <summary>
    /// Gets the system rule file name.
    /// </summary>
    public const string SystemRuleFileName = "system-rule.md";

    /// <summary>
    /// Gets the compression rule file name.
    /// </summary>
    public const string CompressionRuleFileName = "compression-rule.md";

    /// <summary>
    /// Gets the context policy file name.
    /// </summary>
    public const string ContextPolicyFileName = "context-policy.json";

    /// <summary>
    /// Gets the provider policy file name.
    /// </summary>
    public const string ProviderPolicyFileName = "provider-policy.json";

    /// <summary>
    /// Gets the conversations directory name.
    /// </summary>
    public const string ConversationsDirectoryName = "conversations";

    /// <summary>
    /// Gets the conversation database file name.
    /// </summary>
    public const string ConversationDatabaseFileName = "conversation.db";

    /// <summary>
    /// Gets the messages log file name.
    /// </summary>
    public const string MessagesFileName = "messages.jsonl";

    /// <summary>
    /// Gets the requests log file name.
    /// </summary>
    public const string RequestsFileName = "requests.jsonl";

    /// <summary>
    /// Gets the context packages directory name.
    /// </summary>
    public const string ContextPackagesDirectoryName = "context-packages";

    /// <summary>
    /// Gets the rolling context directory name.
    /// </summary>
    public const string RollingContextDirectoryName = "rolling-context";

    /// <summary>
    /// Gets the current rolling context file name.
    /// </summary>
    public const string CurrentRollingContextFileName = "current.md";

    /// <summary>
    /// Gets the rolling context history directory name.
    /// </summary>
    public const string RollingContextHistoryDirectoryName = "history";

    /// <summary>
    /// Gets the rolling context index file name.
    /// </summary>
    public const string RollingContextIndexFileName = "index.jsonl";

    /// <summary>
    /// Gets the settings directory name.
    /// </summary>
    public const string SettingsDirectoryName = "settings";

    /// <summary>
    /// Gets the provider settings file name.
    /// </summary>
    public const string ProvidersFileName = "providers.json";

    /// <summary>
    /// Gets the artifacts directory name.
    /// </summary>
    public const string ArtifactsDirectoryName = "artifacts";

    /// <summary>
    /// Gets the artifact index file name.
    /// </summary>
    public const string ArtifactsIndexFileName = "artifacts.index.json";
}
