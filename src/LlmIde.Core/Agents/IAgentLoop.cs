namespace LlmIde.Core.Agents;

/// <summary>
/// Runs the user-input → multi-turn model/tool exchange loop.
/// </summary>
public interface IAgentLoop
{
    /// <summary>
    /// Runs the agent loop for a single user input.
    /// </summary>
    /// <param name="request">The agent run request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent run result.</returns>
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken);
}
