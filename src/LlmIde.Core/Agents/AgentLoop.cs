using System.Text.Json;
using LlmIde.Core.Providers;

namespace LlmIde.Core.Agents;

/// <summary>
/// Runs a multi-turn agent loop where the model can request tools between turns.
/// </summary>
public sealed class AgentLoop : IAgentLoop
{
    /// <summary>
    /// The model client.
    /// </summary>
    private readonly IChatModelClient modelClient;

    /// <summary>
    /// The tool host.
    /// </summary>
    private readonly IAgentToolHost toolHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentLoop"/> class.
    /// </summary>
    /// <param name="modelClient">The model client.</param>
    /// <param name="toolHost">The tool host.</param>
    public AgentLoop(IChatModelClient modelClient, IAgentToolHost toolHost)
    {
        this.modelClient = modelClient;
        this.toolHost = toolHost;
    }

    /// <summary>
    /// Runs the agent loop for a single user input.
    /// </summary>
    /// <param name="request">The agent run request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent run result.</returns>
    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken)
    {
        AgentLoopOptions options = request.Options ?? new AgentLoopOptions();
        List<AgentTurn> turns = [];
        List<AgentToolResult> toolResults = [];
        List<ChatMessage> messages =
        [
            new ChatMessage { Role = "system", Content = BuildSystemInstructions() },
            new ChatMessage { Role = "user", Content = request.UserInput }
        ];
        turns.Add(new AgentTurn { Role = AgentTurnRole.User, Content = request.UserInput });

        int toolCounter = 0;

        for (int turnIndex = 0; turnIndex < options.MaxTurns; turnIndex++)
        {
            ChatModelResponse response;

            try
            {
                response = await modelClient.CompleteAsync(new ChatModelRequest { Messages = messages }, cancellationToken);
            }
            catch (Exception ex)
            {
                return Failure(turns, toolResults, StopReason.ModelError, ex.Message);
            }

            string content = response.Content ?? string.Empty;
            turns.Add(new AgentTurn { Role = AgentTurnRole.Assistant, Content = content });
            messages.Add(new ChatMessage { Role = "assistant", Content = content });

            ParsedModelMessage parsed = AgentMessageParser.Parse(content);

            if (parsed.Kind == ModelMessageKind.ParseError)
            {
                return Failure(turns, toolResults, StopReason.ModelError, parsed.Error);
            }

            if (parsed.Kind == ModelMessageKind.UnknownType)
            {
                return Failure(turns, toolResults, StopReason.InvalidToolRequest, parsed.Error);
            }

            if (parsed.Kind == ModelMessageKind.Final)
            {
                return new AgentRunResult
                {
                    FinalText = parsed.Answer,
                    Turns = turns,
                    ToolResults = toolResults,
                    StopReason = StopReason.FinalAnswer,
                    IsSuccess = true
                };
            }

            if (parsed.Kind == ModelMessageKind.ListTools)
            {
                string toolListJson = AgentProtocol.ToolList(toolHost.ListTools());
                turns.Add(new AgentTurn { Role = AgentTurnRole.Tool, Content = toolListJson });
                messages.Add(new ChatMessage { Role = "user", Content = toolListJson });
                continue;
            }

            // tool_request
            if (!toolHost.HasTool(parsed.Tool))
            {
                return Failure(turns, toolResults, StopReason.InvalidToolRequest, $"알 수 없는 도구입니다: {parsed.Tool}");
            }

            toolCounter++;
            AgentToolRequest toolRequest = new AgentToolRequest
            {
                Tool = parsed.Tool,
                RequestId = $"tool-{toolCounter:000}",
                Arguments = parsed.Arguments
            };

            AgentToolResult toolResult;

            try
            {
                toolResult = await toolHost.ExecuteAsync(toolRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                // Record the failed execution so no tool usage is omitted, then stop.
                toolResults.Add(new AgentToolResult
                {
                    Tool = toolRequest.Tool,
                    RequestId = toolRequest.RequestId,
                    Ok = false,
                    ErrorMessage = ex.Message
                });
                return Failure(turns, toolResults, StopReason.ToolError, ex.Message);
            }

            toolResults.Add(toolResult);
            string toolResultJson = AgentProtocol.ToolResult(toolResult);
            turns.Add(new AgentTurn { Role = AgentTurnRole.Tool, Content = toolResultJson });
            messages.Add(new ChatMessage { Role = "user", Content = toolResultJson });
        }

        return Failure(turns, toolResults, StopReason.MaxTurnsExceeded, "최대 반복 횟수를 초과했습니다.");
    }

    /// <summary>
    /// Builds the agent system instructions.
    /// </summary>
    /// <returns>The system instructions.</returns>
    private static string BuildSystemInstructions()
    {
        return """
            너는 LLM_IDE의 에이전트다.

            규칙:
            - 너는 URL을 직접 열 수 없다.
            - URL 내용이 필요하면 반드시 tool_request JSON을 반환하라.
            - 도구 결과가 제공되면 그 내용만 근거로 답하라.
            - 도구 결과 없이 URL 내용을 추측하지 마라.
            - 최종 답변은 final JSON으로 반환하라.

            너는 항상 아래 JSON 중 하나만 반환한다. 다른 텍스트는 출력하지 마라.

            도구 목록 요청:
            {"type":"list_tools"}

            도구 사용 요청:
            {"type":"tool_request","tool":"<도구이름>","arguments":{ ... }}

            최종 답변:
            {"type":"final","answer":"사용자에게 보여줄 최종 답변"}

            절차:
            - 도구가 필요하면 먼저 list_tools로 도구 목록을 요청하라.
            - tool_list 응답에서 도구 이름과 인자를 확인한 뒤 tool_request로 사용하라.
            - tool_result가 제공되면 그 내용만 근거로 답하라. 결과 없이 추측하지 마라.
            - 충분하면 final로 종료하라.
            """;
    }

    /// <summary>
    /// Builds a failed agent run result.
    /// </summary>
    /// <param name="turns">The turns so far.</param>
    /// <param name="toolResults">The tool results so far.</param>
    /// <param name="stopReason">The stop reason.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed result.</returns>
    private static AgentRunResult Failure(
        List<AgentTurn> turns,
        List<AgentToolResult> toolResults,
        StopReason stopReason,
        string errorMessage)
    {
        return new AgentRunResult
        {
            Turns = turns,
            ToolResults = toolResults,
            StopReason = stopReason,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
