using System.Text.Json;
using LlmIde.Core.Agents;
using LlmIde.Core.Artifacts;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Agents;

/// <summary>
/// Reads a stored artifact's content and metadata by artifact id, via the artifact service.
/// </summary>
public sealed class ReadArtifactTool : IAgentTool
{
    /// <summary>
    /// The artifact service.
    /// </summary>
    private readonly ArtifactService artifactService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadArtifactTool"/> class.
    /// </summary>
    /// <param name="artifactService">The artifact service.</param>
    public ReadArtifactTool(ArtifactService artifactService)
    {
        Log.Ins.Debug("시작");
        this.artifactService = artifactService;
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name => "read_artifact";

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description => "아티팩트 ID로 저장된 아티팩트의 내용과 메타데이터를 반환한다.";

    /// <summary>
    /// Gets the tool argument summary.
    /// </summary>
    public string Arguments => "{\"artifact_id\":\"필수\"}";

    /// <summary>
    /// Executes the read_artifact tool.
    /// </summary>
    /// <param name="request">The tool request (its WorkingDirectory is the project root).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    public Task<AgentToolResult> ExecuteAsync(AgentToolRequest request, CancellationToken cancellationToken)
    {
        Log.Ins.Debug("시작");
        string artifactId = ReadString(request.Arguments, "artifact_id");

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            return Task.FromResult(Failure(request, artifactId, "작업 폴더가 설정되지 않았습니다."));
        }

        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return Task.FromResult(Failure(request, artifactId, "artifact_id가 비어 있습니다."));
        }

        try
        {
            Artifact artifact = artifactService.Get(request.WorkingDirectory, artifactId);

            // Image artifacts are binary; never read their bytes back as text. Return metadata only.
            string content = ArtifactService.IsImage(artifact)
                ? $"[이미지 아티팩트: {artifact.ContentPath}] 이미지 내용은 텍스트로 제공되지 않는다."
                : artifactService.ReadContent(request.WorkingDirectory, artifact);

            ReadArtifactResult result = new ReadArtifactResult
            {
                Ok = true,
                ArtifactId = artifact.ArtifactId,
                Title = artifact.Title,
                Type = artifact.Type,
                TargetPath = artifact.TargetPath,
                SourceRequestId = artifact.SourceRequestId,
                Content = content
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
            // Unknown id or read failure: report as a failed result, never crash.
            return Task.FromResult(Failure(request, artifactId, ex.Message));
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
    /// Builds a failed read result.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="artifactId">The requested artifact identifier.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed tool result.</returns>
    private AgentToolResult Failure(AgentToolRequest request, string artifactId, string errorMessage)
    {
        Log.Ins.Debug("시작");
        return new AgentToolResult
        {
            Tool = Name,
            RequestId = request.RequestId,
            Ok = false,
            Result = new ReadArtifactResult { Ok = false, ArtifactId = artifactId, ErrorMessage = errorMessage },
            ErrorMessage = errorMessage
        };
    }
}
