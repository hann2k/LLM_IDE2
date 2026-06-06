using System.Text.Json;
using LlmIde.Core.Providers;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Conversations;
using LlmIde.Infrastructure.Json;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores project metadata on the local file system.
/// </summary>
public sealed class JsonFileProjectStore : IProjectStore
{
    /// <summary>
    /// Initializes a project at the supplied path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="projectName">The project name.</param>
    /// <returns>The initialization result.</returns>
    public ProjectInitializationResult Initialize(string projectRoot, string projectName)
    {
        string normalizedRoot = Path.GetFullPath(projectRoot);
        Directory.CreateDirectory(normalizedRoot);

        string metadataRoot = Path.Combine(normalizedRoot, LlmIdeLayout.MetadataDirectoryName);
        bool created = !Directory.Exists(metadataRoot);
        Directory.CreateDirectory(metadataRoot);

        try
        {
            WriteProgress(metadataRoot, "metadata-directory", "running", string.Empty);
            CreateDirectories(metadataRoot);
            WriteProgress(metadataRoot, "directories", "running", string.Empty);
            ProjectInfo projectInfo = EnsureProjectInfo(normalizedRoot, metadataRoot, projectName);
            WriteProgress(metadataRoot, "project-info", "running", string.Empty);
            EnsureJsonFile(Path.Combine(metadataRoot, LlmIdeLayout.ProjectStateFileName), CreateDefaultProjectState());
            WriteProgress(metadataRoot, "project-state", "running", string.Empty);
            EnsureJsonFile(Path.Combine(metadataRoot, LlmIdeLayout.CriteriaFileName), Array.Empty<Criterion>());
            WriteProgress(metadataRoot, "criteria", "running", string.Empty);
            EnsureTextFile(Path.Combine(metadataRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.SystemRuleFileName), string.Empty);
            EnsureJsonFile(Path.Combine(metadataRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.ContextPolicyFileName), new Dictionary<string, object>());
            EnsureJsonFile(Path.Combine(metadataRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.ProviderPolicyFileName), new Dictionary<string, object>());
            WriteProgress(metadataRoot, "policies", "running", string.Empty);
            EnsureTextFile(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName, LlmIdeLayout.MessagesFileName), string.Empty);
            EnsureTextFile(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName, LlmIdeLayout.RequestsFileName), string.Empty);
            SqliteConversationLogStore.EnsureDatabase(normalizedRoot);
            WriteProgress(metadataRoot, "conversations", "running", string.Empty);
            EnsureJsonFile(Path.Combine(metadataRoot, LlmIdeLayout.SettingsDirectoryName, LlmIdeLayout.ProvidersFileName), CreateDefaultProviderSettings());
            WriteProgress(metadataRoot, "settings", "running", string.Empty);
            EnsureJsonFile(Path.Combine(metadataRoot, LlmIdeLayout.ArtifactsDirectoryName, LlmIdeLayout.ArtifactsIndexFileName), Array.Empty<object>());
            WriteProgress(metadataRoot, "completed", "completed", string.Empty);

            return new ProjectInitializationResult(normalizedRoot, projectInfo, created);
        }
        catch (Exception ex)
        {
            WriteProgress(metadataRoot, "failed", "failed", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Determines whether a path is an initialized project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>True when the path contains project metadata.</returns>
    public bool IsInitialized(string projectRoot)
    {
        string metadataRoot = Path.Combine(Path.GetFullPath(projectRoot), LlmIdeLayout.MetadataDirectoryName);
        string projectFile = Path.Combine(metadataRoot, LlmIdeLayout.ProjectFileName);
        return File.Exists(projectFile);
    }

    /// <summary>
    /// Reads project information from an initialized project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project information.</returns>
    public ProjectInfo ReadProjectInfo(string projectRoot)
    {
        string projectFile = Path.Combine(Path.GetFullPath(projectRoot), LlmIdeLayout.MetadataDirectoryName, LlmIdeLayout.ProjectFileName);
        string json = File.ReadAllText(projectFile);
        ProjectInfo? projectInfo = JsonSerializer.Deserialize<ProjectInfo>(json, JsonOptions.Default);

        if (projectInfo is null)
        {
            throw new InvalidOperationException("Project metadata could not be read.");
        }

        return projectInfo;
    }

    /// <summary>
    /// Creates the Phase 1 metadata directories.
    /// </summary>
    /// <param name="metadataRoot">The metadata root path.</param>
    private static void CreateDirectories(string metadataRoot)
    {
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.PoliciesDirectoryName));
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName));
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName, LlmIdeLayout.ContextPackagesDirectoryName));
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.SettingsDirectoryName));
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.ArtifactsDirectoryName));
    }

    /// <summary>
    /// Ensures the project information file exists.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="metadataRoot">The metadata root path.</param>
    /// <returns>The project information.</returns>
    private static ProjectInfo EnsureProjectInfo(string projectRoot, string metadataRoot, string projectName)
    {
        string projectFile = Path.Combine(metadataRoot, LlmIdeLayout.ProjectFileName);

        if (File.Exists(projectFile))
        {
            string json = File.ReadAllText(projectFile);
            ProjectInfo? existingProject = JsonSerializer.Deserialize<ProjectInfo>(json, JsonOptions.Default);
            if (existingProject is not null)
            {
                return existingProject;
            }
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectInfo projectInfo = new ProjectInfo
        {
            ProjectId = $"proj_{Guid.NewGuid():N}",
            Title = projectName,
            CreatedAt = now,
            UpdatedAt = now,
            DefaultProvider = "deepseek",
            DefaultModel = string.Empty
        };

        WriteJson(projectFile, projectInfo);
        return projectInfo;
    }

    /// <summary>
    /// Creates the default project state.
    /// </summary>
    /// <returns>The default project state.</returns>
    private static ProjectState CreateDefaultProjectState()
    {
        return new ProjectState();
    }

    /// <summary>
    /// Creates the default provider settings.
    /// </summary>
    /// <returns>The default provider settings.</returns>
    private static ProviderSettingsDocument CreateDefaultProviderSettings()
    {
        return new ProviderSettingsDocument
        {
            DefaultProvider = "deepseek",
            Providers =
            [
                new ProviderSettings
                {
                    Name = "deepseek",
                    ApiKey = string.Empty,
                    Endpoint = "https://api.deepseek.com/chat/completions",
                    Model = "deepseek-chat"
                }
            ]
        };
    }

    /// <summary>
    /// Ensures a JSON file exists.
    /// </summary>
    /// <param name="path">The JSON file path.</param>
    /// <param name="value">The value to write when the file is missing.</param>
    private static void EnsureJsonFile<TValue>(string path, TValue value)
    {
        if (File.Exists(path))
        {
            return;
        }

        WriteJson(path, value);
    }

    /// <summary>
    /// Ensures a text file exists.
    /// </summary>
    /// <param name="path">The text file path.</param>
    /// <param name="content">The content to write when the file is missing.</param>
    private static void EnsureTextFile(string path, string content)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Writes a JSON file.
    /// </summary>
    /// <param name="path">The JSON file path.</param>
    /// <param name="value">The value to serialize.</param>
    private static void WriteJson<TValue>(string path, TValue value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions.Default);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Writes initialization progress.
    /// </summary>
    /// <param name="metadataRoot">The metadata root path.</param>
    /// <param name="step">The latest step.</param>
    /// <param name="status">The progress status.</param>
    /// <param name="errorMessage">The error message.</param>
    private static void WriteProgress(string metadataRoot, string step, string status, string errorMessage)
    {
        ProjectInitProgress progress = new ProjectInitProgress
        {
            Step = step,
            Status = status,
            ErrorMessage = errorMessage,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        WriteJson(Path.Combine(metadataRoot, LlmIdeLayout.InitProgressFileName), progress);
    }
}
