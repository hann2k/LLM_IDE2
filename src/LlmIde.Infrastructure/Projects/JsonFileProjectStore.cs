using System.Text.Json;
using LlmIde.Core.Providers;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Conversations;
using LlmIde.Infrastructure.Json;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Stores project metadata on the local file system.
/// </summary>
public sealed class JsonFileProjectStore : IProjectStore
{
    /// <summary>
    /// The IDE program root containing policy templates.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileProjectStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public JsonFileProjectStore(string? programRoot = null)
    {
        Log.Ins.Debug("시작");
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <summary>
    /// Initializes a project at the supplied path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="projectName">The project name.</param>
    /// <returns>The initialization result.</returns>
    public ProjectInitializationResult Initialize(string projectRoot, string projectName)
    {
        Log.Ins.Debug("시작");
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
            // Policies and provider settings are common (app-root /policies, /settings); not seeded per project.
            WriteProgress(metadataRoot, "policies", "running", string.Empty);
            EnsureTextFile(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName, LlmIdeLayout.MessagesFileName), string.Empty);
            EnsureTextFile(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName, LlmIdeLayout.RequestsFileName), string.Empty);
            SqliteConversationLogStore.EnsureDatabase(normalizedRoot);
            new FileRollingContextStore(programRoot).EnsureInitialized(normalizedRoot);
            WriteProgress(metadataRoot, "conversations", "running", string.Empty);
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
    /// Saves project information for an initialized project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="projectInfo">The project information.</param>
    public void SaveProjectInfo(string projectRoot, ProjectInfo projectInfo)
    {
        Log.Ins.Debug("시작");
        string projectFile = Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.ProjectFileName);
        WriteJson(projectFile, projectInfo);
    }

    /// <summary>
    /// Creates the Phase 1 metadata directories.
    /// </summary>
    /// <param name="metadataRoot">The metadata root path.</param>
    private static void CreateDirectories(string metadataRoot)
    {
        Log.Ins.Debug("시작");
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.PoliciesDirectoryName));
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName));
        Directory.CreateDirectory(Path.Combine(metadataRoot, LlmIdeLayout.ConversationsDirectoryName, LlmIdeLayout.ContextPackagesDirectoryName));
        Directory.CreateDirectory(Path.Combine(
            metadataRoot,
            LlmIdeLayout.ConversationsDirectoryName,
            LlmIdeLayout.RollingContextDirectoryName,
            LlmIdeLayout.RollingContextHistoryDirectoryName));
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        return new ProjectState();
    }

    /// <summary>
    /// Creates the default provider settings.
    /// </summary>
    /// <returns>The default provider settings.</returns>
    private static ProviderSettingsDocument CreateDefaultProviderSettings()
    {
        Log.Ins.Debug("시작");
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
    /// Creates the default context policy JSON.
    /// </summary>
    /// <returns>The default context policy JSON.</returns>
    private static string CreateDefaultContextPolicyJson()
    {
        Log.Ins.Debug("시작");
        Dictionary<string, object> policy = new Dictionary<string, object>
        {
            ["context_management_strategy"] = "summary_plus_window",
            ["include_rolling_context_summary"] = true,
            ["rolling_context_path"] = string.Join(
                '/',
                LlmIdeLayout.ConversationsDirectoryName,
                LlmIdeLayout.RollingContextDirectoryName,
                LlmIdeLayout.CurrentRollingContextFileName),
            ["recent_turn_count"] = 0,
            ["max_recent_turn_chars"] = 0,
            ["max_rolling_context_chars"] = 24000,
            ["compression_enabled"] = true,
            ["compression_provider"] = "deepseek",
            ["compression_model"] = "deepseek-chat",
            ["compression_after_chat"] = true,
            ["compression_failure_strategy"] = "keep_previous",
            ["rag_enabled"] = false
        };

        return JsonSerializer.Serialize(policy, JsonOptions.Default);
    }

    /// <summary>
    /// Creates the default provider policy JSON.
    /// </summary>
    /// <returns>The default provider policy JSON.</returns>
    private static string CreateDefaultProviderPolicyJson()
    {
        Log.Ins.Debug("시작");
        Dictionary<string, object> policy = new Dictionary<string, object>
        {
            ["default_provider"] = "deepseek",
            ["allowed_providers"] = new[] { "deepseek" },
            ["api_key_storage"] = "project_local_settings",
            ["allow_empty_api_key"] = true,
            ["network_required"] = true
        };

        return JsonSerializer.Serialize(policy, JsonOptions.Default);
    }

    /// <summary>
    /// Ensures a JSON file exists.
    /// </summary>
    /// <param name="path">The JSON file path.</param>
    /// <param name="value">The value to write when the file is missing.</param>
    private static void EnsureJsonFile<TValue>(string path, TValue value)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Ensures a JSON text file exists.
    /// </summary>
    /// <param name="path">The JSON file path.</param>
    /// <param name="content">The JSON text to write when the file is missing.</param>
    private static void EnsureJsonTextFile(string path, string content)
    {
        Log.Ins.Debug("시작");
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Loads a policy template from the program policies directory.
    /// </summary>
    /// <param name="fileName">The policy file name.</param>
    /// <returns>The policy template content.</returns>
    private string LoadProgramPolicyTemplate(string fileName)
    {
        Log.Ins.Debug("시작");
        return LoadProgramPolicyTemplate(fileName, string.Empty);
    }

    /// <summary>
    /// Loads a policy template from the program policies directory.
    /// </summary>
    /// <param name="fileName">The policy file name.</param>
    /// <param name="fallback">The fallback content.</param>
    /// <returns>The policy template content.</returns>
    private string LoadProgramPolicyTemplate(string fileName, string fallback)
    {
        Log.Ins.Debug("시작");
        string path = Path.Combine(programRoot, LlmIdeLayout.PoliciesDirectoryName, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : fallback;
    }

    /// <summary>
    /// Writes a JSON file.
    /// </summary>
    /// <param name="path">The JSON file path.</param>
    /// <param name="value">The value to serialize.</param>
    private static void WriteJson<TValue>(string path, TValue value)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
