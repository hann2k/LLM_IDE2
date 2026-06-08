namespace LlmIde.Core.Agents;

/// <summary>
/// Identifies who produced an agent loop turn.
/// </summary>
public enum AgentTurnRole
{
    /// <summary>
    /// The user input turn.
    /// </summary>
    User,

    /// <summary>
    /// An assistant (LLM) turn.
    /// </summary>
    Assistant,

    /// <summary>
    /// A tool result turn.
    /// </summary>
    Tool
}
