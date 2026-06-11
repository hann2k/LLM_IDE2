using LlmIde.Core.Agents;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Returns the currently selected document section's body from the editor (in-memory, never a file),
/// so the model can read the text it is about to revise.
/// </summary>
public sealed class GetBodyTool : IAgentTool
{
    private readonly IDocumentBodyBridge bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetBodyTool"/> class.
    /// </summary>
    /// <param name="bridge">The document body bridge.</param>
    public GetBodyTool(IDocumentBodyBridge bridge)
    {
        Log.Ins.Debug("시작");
        this.bridge = bridge;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "get_body";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "현재 편집창에서 선택된 목차 항목의 본문 내용을 반환한다. 본문을 수정하려면 먼저 이 도구로 현재 본문을 확인하라.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{}";

    /// <summary>
    /// Executes the get_body tool.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        try
        {
            DocumentBodySnapshot? snapshot = bridge.GetCurrentBody();

            GetBodyResult result = snapshot is null
                ? new GetBodyResult { Ok = true, HasSelection = false }
                : new GetBodyResult { Ok = true, HasSelection = true, Title = snapshot.Title, Body = snapshot.Body };

            return Task.FromResult(new AgentToolResult
            {
                Tool = Name,
                RequestId = request.RequestId,
                Ok = true,
                Result = result
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentToolResult
            {
                Tool = Name,
                RequestId = request.RequestId,
                Ok = false,
                Result = new GetBodyResult { Ok = false, ErrorMessage = ex.Message },
                ErrorMessage = ex.Message
            });
        }
    }
}
