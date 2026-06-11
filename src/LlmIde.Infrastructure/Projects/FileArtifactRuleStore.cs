using LlmIde.Core.Projects;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Loads artifact tagging rules from project policy files.
/// </summary>
public sealed class FileArtifactRuleStore : IArtifactRuleStore
{
    /// <summary>
    /// The IDE program root containing the common policy files.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileArtifactRuleStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public FileArtifactRuleStore(string? programRoot = null)
    {
        Log.Ins.Debug("시작");
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <inheritdoc />
    public string Load(string projectRoot)
    {
        // Policies are common to all projects: read from the program's /policies folder.
        Log.Ins.Debug("시작");
        string path = Path.Combine(programRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.ArtifactRuleFileName);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
