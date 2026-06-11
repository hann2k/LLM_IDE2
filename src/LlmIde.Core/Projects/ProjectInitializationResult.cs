using Framework.Common.Logger;
namespace LlmIde.Core.Projects;

/// <summary>
/// Describes the result of a project initialization request.
/// </summary>
public sealed class ProjectInitializationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectInitializationResult"/> class.
    /// </summary>
    /// <param name="projectRoot">The normalized project root path.</param>
    /// <param name="projectInfo">The project information.</param>
    /// <param name="created">Whether a new metadata folder was created.</param>
    public ProjectInitializationResult(string projectRoot, ProjectInfo projectInfo, bool created)
    {
        Log.Ins.Debug("시작");
        ProjectRoot = projectRoot;
        ProjectInfo = projectInfo;
        Created = created;
    }

    /// <summary>
    /// Gets the normalized project root path.
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// Gets the project information.
    /// </summary>
    public ProjectInfo ProjectInfo { get; }

    /// <summary>
    /// Gets a value indicating whether the project metadata was newly created.
    /// </summary>
    public bool Created { get; }
}
