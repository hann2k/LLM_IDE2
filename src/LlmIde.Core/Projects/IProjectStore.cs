namespace LlmIde.Core.Projects;

/// <summary>
/// Provides project metadata storage operations.
/// </summary>
public interface IProjectStore
{
    /// <summary>
    /// Initializes a project at the supplied path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="projectName">The project name.</param>
    /// <returns>The initialization result.</returns>
    ProjectInitializationResult Initialize(string projectRoot, string projectName);

    /// <summary>
    /// Determines whether a path is an initialized project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>True when the path contains project metadata.</returns>
    bool IsInitialized(string projectRoot);

    /// <summary>
    /// Reads project information from an initialized project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project information.</returns>
    ProjectInfo ReadProjectInfo(string projectRoot);

    /// <summary>
    /// Saves project information for an initialized project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="projectInfo">The project information.</param>
    void SaveProjectInfo(string projectRoot, ProjectInfo projectInfo);
}
