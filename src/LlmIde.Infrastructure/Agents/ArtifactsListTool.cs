using System.Linq;
using LlmIde.Core.Agents;
using LlmIde.Core.Artifacts;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Lists the project's stored artifacts (metadata only) via the artifact service. Artifacts live under
/// the .llmide system folder, so they are exposed through this dedicated tool rather than raw file access.
/// </summary>
public sealed class ArtifactsListTool : IAgentTool
{
    /// <summary>
    /// The artifact service.
    /// </summary>
    private readonly ArtifactService artifactService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactsListTool"/> class.
    /// </summary>
    /// <param name="artifactService">The artifact service.</param>
    public ArtifactsListTool(ArtifactService artifactService)
    {
        this.artifactService = artifactService;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "artifacts_list";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "프로젝트에 저장된 아티팩트 목록(ID/제목/유형/출처 대화 ID)을 반환한다.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{}";

    /// <summary>
    /// Executes the artifacts_list tool.
    /// </summary>
    /// <param name="request">The tool request (its WorkingDirectory is the project root).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            return Task.FromResult(Failure(request, "작업 폴더가 설정되지 않았습니다."));
        }

        try
        {
            List<ArtifactSummary> items = artifactService.List(request.WorkingDirectory)
                .Select(artifact => new ArtifactSummary
                {
                    ArtifactId = artifact.ArtifactId,
                    Title = artifact.Title,
                    Type = artifact.Type,
                    TargetPath = artifact.TargetPath,
                    SourceRequestId = artifact.SourceRequestId
                })
                .ToList();

            ArtifactsListResult result = new ArtifactsListResult
            {
                Ok = true,
                Items = items
            };

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
            // A tool failure must not crash the app; report it as a failed result.
            return Task.FromResult(Failure(request, ex.Message));
        }
    }

    /// <summary>
    /// Builds a failed listing result.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed tool result.</returns>
    private AgentToolResult Failure(AgentToolRequest request, string errorMessage)
    {
        return new AgentToolResult
        {
            Tool = Name,
            RequestId = request.RequestId,
            Ok = false,
            Result = new ArtifactsListResult { Ok = false, ErrorMessage = errorMessage },
            ErrorMessage = errorMessage
        };
    }
}
