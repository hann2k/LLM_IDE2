using System.Text.Json;
using LlmIde.Core.Agents;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Records a proposed full-body revision for the selected document section. The revision is not applied
/// until the user approves it in the editor's before/after window — this tool only stores the proposal.
/// </summary>
public sealed class ProposeBodyEditTool : IAgentTool
{
    private readonly IDocumentBodyBridge bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProposeBodyEditTool"/> class.
    /// </summary>
    /// <param name="bridge">The document body bridge.</param>
    public ProposeBodyEditTool(IDocumentBodyBridge bridge)
    {
        Log.Ins.Debug("시작");
        this.bridge = bridge;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "propose_body_edit";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "선택된 목차 항목 본문의 수정 전문을 제안한다. 사용자가 [변경/취소]로 승인해야 실제 반영된다. body 인자에 수정된 본문 전체를 담아라.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{\"body\":\"수정된 본문 전체\"}";

    /// <summary>
    /// Executes the propose_body_edit tool.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        string body = ReadString(request.Arguments, "body");

        if (body.Length == 0)
        {
            return Task.FromResult(Failure(request, "body 인자가 비어 있습니다."));
        }

        try
        {
            bridge.ProposeBodyRevision(body);

            return Task.FromResult(new AgentToolResult
            {
                Tool = Name,
                RequestId = request.RequestId,
                Ok = true,
                Result = new ProposeBodyEditResult { Ok = true, Length = body.Length }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failure(request, ex.Message));
        }
    }

    /// <summary>
    /// Reads a string argument.
    /// </summary>
    /// <param name="arguments">The arguments element.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The string value, or empty.</returns>
    private static string ReadString(JsonElement arguments, string name)
    {
        Log.Ins.Debug("시작");
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds a failed proposal result.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed tool result.</returns>
    private AgentToolResult Failure(AgentToolRequest request, string errorMessage)
    {
        Log.Ins.Debug("시작");
        return new AgentToolResult
        {
            Tool = Name,
            RequestId = request.RequestId,
            Ok = false,
            Result = new ProposeBodyEditResult { Ok = false, ErrorMessage = errorMessage },
            ErrorMessage = errorMessage
        };
    }
}
