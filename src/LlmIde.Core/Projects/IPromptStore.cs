namespace LlmIde.Core.Projects;

/// <summary>
/// Loads the combined prompt templates (key to instruction text) from project policy files.
/// </summary>
public interface IPromptStore
{
    /// <summary>
    /// Loads all prompt templates for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The prompt templates keyed by <see cref="PromptKeys"/> identifiers (empty when none configured).</returns>
    IReadOnlyDictionary<string, string> Load(string projectRoot);
}

/// <summary>
/// Defines the keys used in the combined prompt templates file.
/// </summary>
public static class PromptKeys
{
    /// <summary>
    /// The standalone agent loop system prompt.
    /// </summary>
    public const string AgentSystem = "agent_system";

    /// <summary>
    /// The in-chat tool instruction injected into a normal chat request when tools are available.
    /// </summary>
    public const string ChatToolInstruction = "chat_tool_instruction";

    /// <summary>
    /// The extra in-chat instruction appended only when the writing body tools
    /// (get_body / propose_body_edit) are registered, forcing the body-edit tool flow.
    /// </summary>
    public const string ChatBodyToolInstruction = "chat_body_tool_instruction";

    /// <summary>
    /// The intro line before the active project criteria in a chat request.
    /// </summary>
    public const string ContextCriteriaIntro = "context_criteria_intro";

    /// <summary>
    /// The intro line before the project state in a chat request.
    /// </summary>
    public const string ContextStateIntro = "context_state_intro";

    /// <summary>
    /// The intro line before the rolling context summary in a chat request.
    /// </summary>
    public const string ContextRollingIntro = "context_rolling_intro";

    /// <summary>
    /// The intro line before user-attached artifacts in a chat request.
    /// </summary>
    public const string ContextArtifactsIntro = "context_artifacts_intro";

    /// <summary>
    /// The intro line before the active project criteria in a compression request.
    /// </summary>
    public const string CompressionCriteriaIntro = "compression_criteria_intro";

    /// <summary>
    /// The compression prompt that merges the previous summary with the current turn.
    /// Placeholders: {previous_summary}, {user_message}, {assistant_message}.
    /// </summary>
    public const string CompressionMerge = "compression_merge";

    /// <summary>
    /// The bootstrap compression prompt that summarizes the raw conversation log.
    /// Placeholder: {conversation_log}.
    /// </summary>
    public const string CompressionBootstrap = "compression_bootstrap";
}
