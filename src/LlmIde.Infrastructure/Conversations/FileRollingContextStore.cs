using System.Text.Json;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Microsoft.Data.Sqlite;

namespace LlmIde.Infrastructure.Conversations;

/// <summary>
/// Stores rolling context summaries in files and SQLite.
/// </summary>
public sealed class FileRollingContextStore : IRollingContextStore
{
    /// <summary>
    /// The IDE program root containing policy templates.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRollingContextStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public FileRollingContextStore(string? programRoot = null)
    {
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <summary>
    /// Ensures rolling context files and directories exist.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    public void EnsureInitialized(string projectRoot)
    {
        string rollingContextDirectory = GetRollingContextDirectory(projectRoot);
        Directory.CreateDirectory(rollingContextDirectory);
        Directory.CreateDirectory(GetHistoryDirectory(projectRoot));
        EnsureTextFile(GetCurrentPath(projectRoot), string.Empty);
        EnsureTextFile(GetIndexPath(projectRoot), string.Empty);
        EnsureRequiredTextFile(
            GetCompressionRulePath(projectRoot),
            LoadProgramPolicyTemplate(LlmIdeLayout.CompressionRuleFileName));
        EnsureContextPolicy(projectRoot);
        EnsureRollingContextTable(projectRoot);
    }

    /// <summary>
    /// Loads the current rolling context summary.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The current summary, or null when none exists.</returns>
    public RollingContextSummary? LoadCurrent(string projectRoot)
    {
        EnsureInitialized(projectRoot);
        string content = File.ReadAllText(GetCurrentPath(projectRoot));

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        RollingContextSummary? current = LoadCurrentFromDatabase(projectRoot);

        if (current is not null)
        {
            current.Content = content;
            return current;
        }

        return new RollingContextSummary
        {
            Content = content,
            ContentPath = GetCurrentRelativePath(),
            Status = "completed",
            IsCurrent = true
        };
    }

    /// <summary>
    /// Loads the compression rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The compression rule.</returns>
    public string LoadCompressionRule(string projectRoot)
    {
        EnsureInitialized(projectRoot);
        return File.ReadAllText(GetCompressionRulePath(projectRoot));
    }

    /// <summary>
    /// Saves a completed rolling context summary.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="sourceRequestId">The source chat request identifier.</param>
    /// <param name="previousRollingContextId">The previous rolling context identifier.</param>
    /// <param name="content">The summary content.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="model">The model name.</param>
    /// <returns>The saved summary.</returns>
    public RollingContextSummary SaveCompleted(
        string projectRoot,
        string sourceRequestId,
        string? previousRollingContextId,
        string content,
        string provider,
        string model)
    {
        EnsureInitialized(projectRoot);
        RollingContextSummary? supersededSummary = LoadCurrentFromDatabase(projectRoot);
        MarkCurrentSummariesSuperseded(projectRoot);

        if (supersededSummary is not null)
        {
            supersededSummary.Status = "superseded";
            supersededSummary.IsCurrent = false;
            AppendIndex(projectRoot, supersededSummary);
        }

        RollingContextSummary summary = CreateSummary(
            sourceRequestId,
            previousRollingContextId,
            content,
            provider,
            model,
            "completed",
            null,
            true);

        string historyPath = Path.Combine(GetHistoryDirectory(projectRoot), $"{summary.RollingContextId}.md");
        File.WriteAllText(historyPath, content);
        File.WriteAllText(GetCurrentPath(projectRoot), content);
        InsertSummary(projectRoot, summary);
        AppendIndex(projectRoot, summary);
        return summary;
    }

    /// <summary>
    /// Saves a failed rolling context summary attempt.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="sourceRequestId">The source chat request identifier.</param>
    /// <param name="previousRollingContextId">The previous rolling context identifier.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="model">The model name.</param>
    /// <param name="error">The error message.</param>
    /// <returns>The failed summary record.</returns>
    public RollingContextSummary SaveFailed(
        string projectRoot,
        string sourceRequestId,
        string? previousRollingContextId,
        string provider,
        string model,
        string error)
    {
        EnsureInitialized(projectRoot);
        RollingContextSummary summary = CreateSummary(
            sourceRequestId,
            previousRollingContextId,
            string.Empty,
            provider,
            model,
            "failed",
            error,
            false);
        InsertSummary(projectRoot, summary);
        AppendIndex(projectRoot, summary);
        return summary;
    }

    /// <summary>
    /// Creates a summary record.
    /// </summary>
    private static RollingContextSummary CreateSummary(
        string sourceRequestId,
        string? previousRollingContextId,
        string content,
        string provider,
        string model,
        string status,
        string? error,
        bool isCurrent)
    {
        string rollingContextId = $"rctx_{Guid.NewGuid():N}";

        return new RollingContextSummary
        {
            RollingContextId = rollingContextId,
            SourceRequestId = sourceRequestId,
            PreviousRollingContextId = previousRollingContextId,
            Content = content,
            ContentPath = status == "completed" ? GetHistoryRelativePath(rollingContextId) : string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Provider = provider,
            Model = model,
            Status = status,
            Error = error,
            IsCurrent = isCurrent
        };
    }

    /// <summary>
    /// Ensures the context policy file contains Phase 6 defaults when it is empty.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    private void EnsureContextPolicy(string projectRoot)
    {
        string path = Path.Combine(
            GetMetadataDirectory(projectRoot),
            LlmIdeLayout.PoliciesDirectoryName,
            LlmIdeLayout.ContextPolicyFileName);

        if (File.Exists(path))
        {
            string existingPolicy = File.ReadAllText(path);

            if (!string.IsNullOrWhiteSpace(existingPolicy) && existingPolicy.Trim() != "{}")
            {
                return;
            }
        }

        File.WriteAllText(path, LoadProgramPolicyTemplate(LlmIdeLayout.ContextPolicyFileName, CreateDefaultContextPolicyJson()));
    }

    /// <summary>
    /// Creates the default context policy JSON.
    /// </summary>
    /// <returns>The default context policy JSON.</returns>
    private static string CreateDefaultContextPolicyJson()
    {
        Dictionary<string, object> policy = new Dictionary<string, object>
        {
            ["context_management_strategy"] = "summary_plus_window",
            ["include_rolling_context_summary"] = true,
            ["rolling_context_path"] = GetCurrentRelativePath(),
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
    /// Loads a policy template from the program policies directory.
    /// </summary>
    /// <param name="fileName">The policy file name.</param>
    /// <returns>The policy template content.</returns>
    private string LoadProgramPolicyTemplate(string fileName)
    {
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
        string path = Path.Combine(programRoot, LlmIdeLayout.PoliciesDirectoryName, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : fallback;
    }

    /// <summary>
    /// Ensures a text file exists.
    /// </summary>
    private static void EnsureTextFile(string path, string content)
    {
        if (File.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Ensures a required text file exists and is not empty.
    /// </summary>
    private static void EnsureRequiredTextFile(string path, string content)
    {
        if (File.Exists(path) && !string.IsNullOrWhiteSpace(File.ReadAllText(path)))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Appends a summary index record.
    /// </summary>
    private static void AppendIndex(string projectRoot, RollingContextSummary summary)
    {
        string json = JsonSerializer.Serialize(summary, JsonOptions.Compact);
        File.AppendAllText(GetIndexPath(projectRoot), json + Environment.NewLine);
    }

    /// <summary>
    /// Ensures the rolling context SQLite table exists.
    /// </summary>
    private static void EnsureRollingContextTable(string projectRoot)
    {
        string databasePath = SqliteConversationLogStore.EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists rolling_context_summaries
            (
                rolling_context_id text primary key,
                source_request_id text not null,
                previous_rolling_context_id text null,
                content text not null,
                content_path text not null,
                created_at text not null,
                provider text not null,
                model text not null,
                status text not null,
                error text null,
                is_current integer not null
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Marks current summaries as superseded.
    /// </summary>
    private static void MarkCurrentSummariesSuperseded(string projectRoot)
    {
        string databasePath = SqliteConversationLogStore.EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            update rolling_context_summaries
            set status = 'superseded',
                is_current = 0
            where is_current = 1;
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts a rolling context summary.
    /// </summary>
    private static void InsertSummary(string projectRoot, RollingContextSummary summary)
    {
        string databasePath = SqliteConversationLogStore.EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            insert into rolling_context_summaries
            (
                rolling_context_id,
                source_request_id,
                previous_rolling_context_id,
                content,
                content_path,
                created_at,
                provider,
                model,
                status,
                error,
                is_current
            )
            values
            (
                $rolling_context_id,
                $source_request_id,
                $previous_rolling_context_id,
                $content,
                $content_path,
                $created_at,
                $provider,
                $model,
                $status,
                $error,
                $is_current
            );
            """;
        command.Parameters.AddWithValue("$rolling_context_id", summary.RollingContextId);
        command.Parameters.AddWithValue("$source_request_id", summary.SourceRequestId);
        command.Parameters.AddWithValue("$previous_rolling_context_id", (object?)summary.PreviousRollingContextId ?? DBNull.Value);
        command.Parameters.AddWithValue("$content", summary.Content);
        command.Parameters.AddWithValue("$content_path", summary.ContentPath);
        command.Parameters.AddWithValue("$created_at", summary.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$provider", summary.Provider);
        command.Parameters.AddWithValue("$model", summary.Model);
        command.Parameters.AddWithValue("$status", summary.Status);
        command.Parameters.AddWithValue("$error", (object?)summary.Error ?? DBNull.Value);
        command.Parameters.AddWithValue("$is_current", summary.IsCurrent ? 1 : 0);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Loads the current summary metadata from SQLite.
    /// </summary>
    private static RollingContextSummary? LoadCurrentFromDatabase(string projectRoot)
    {
        string databasePath = SqliteConversationLogStore.EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            select
                rolling_context_id,
                source_request_id,
                previous_rolling_context_id,
                content,
                content_path,
                created_at,
                provider,
                model,
                status,
                error,
                is_current
            from rolling_context_summaries
            where is_current = 1
            order by created_at desc
            limit 1;
            """;

        using SqliteDataReader reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return new RollingContextSummary
        {
            RollingContextId = reader.GetString(0),
            SourceRequestId = reader.GetString(1),
            PreviousRollingContextId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Content = reader.GetString(3),
            ContentPath = reader.GetString(4),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(5)),
            Provider = reader.GetString(6),
            Model = reader.GetString(7),
            Status = reader.GetString(8),
            Error = reader.IsDBNull(9) ? null : reader.GetString(9),
            IsCurrent = reader.GetInt32(10) == 1
        };
    }

    /// <summary>
    /// Opens a SQLite connection.
    /// </summary>
    private static SqliteConnection OpenConnection(string databasePath)
    {
        SqliteConnection connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Gets the metadata directory.
    /// </summary>
    private static string GetMetadataDirectory(string projectRoot)
    {
        return Path.Combine(Path.GetFullPath(projectRoot), LlmIdeLayout.MetadataDirectoryName);
    }

    /// <summary>
    /// Gets the rolling context directory.
    /// </summary>
    private static string GetRollingContextDirectory(string projectRoot)
    {
        return Path.Combine(
            GetMetadataDirectory(projectRoot),
            LlmIdeLayout.ConversationsDirectoryName,
            LlmIdeLayout.RollingContextDirectoryName);
    }

    /// <summary>
    /// Gets the rolling context history directory.
    /// </summary>
    private static string GetHistoryDirectory(string projectRoot)
    {
        return Path.Combine(GetRollingContextDirectory(projectRoot), LlmIdeLayout.RollingContextHistoryDirectoryName);
    }

    /// <summary>
    /// Gets the current rolling context path.
    /// </summary>
    private static string GetCurrentPath(string projectRoot)
    {
        return Path.Combine(GetRollingContextDirectory(projectRoot), LlmIdeLayout.CurrentRollingContextFileName);
    }

    /// <summary>
    /// Gets the rolling context index path.
    /// </summary>
    private static string GetIndexPath(string projectRoot)
    {
        return Path.Combine(GetRollingContextDirectory(projectRoot), LlmIdeLayout.RollingContextIndexFileName);
    }

    /// <summary>
    /// Gets the compression rule path.
    /// </summary>
    private static string GetCompressionRulePath(string projectRoot)
    {
        return Path.Combine(
            GetMetadataDirectory(projectRoot),
            LlmIdeLayout.PoliciesDirectoryName,
            LlmIdeLayout.CompressionRuleFileName);
    }

    /// <summary>
    /// Gets the current rolling context relative path.
    /// </summary>
    private static string GetCurrentRelativePath()
    {
        return string.Join(
            '/',
            LlmIdeLayout.ConversationsDirectoryName,
            LlmIdeLayout.RollingContextDirectoryName,
            LlmIdeLayout.CurrentRollingContextFileName);
    }

    /// <summary>
    /// Gets a rolling context history relative path.
    /// </summary>
    private static string GetHistoryRelativePath(string rollingContextId)
    {
        return string.Join(
            '/',
            LlmIdeLayout.ConversationsDirectoryName,
            LlmIdeLayout.RollingContextDirectoryName,
            LlmIdeLayout.RollingContextHistoryDirectoryName,
            $"{rollingContextId}.md");
    }
}
