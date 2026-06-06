using System.Text.Json;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Microsoft.Data.Sqlite;

namespace LlmIde.Infrastructure.Conversations;

/// <summary>
/// Stores conversation logs in a project-level SQLite database.
/// </summary>
public sealed class SqliteConversationLogStore : IConversationLogStore
{
    /// <summary>
    /// Saves a context package.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="contextPackage">The context package.</param>
    /// <returns>The database file path.</returns>
    public string SaveContextPackage(string projectRoot, ContextPackage contextPackage)
    {
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            insert or replace into context_packages
            (
                request_id,
                created_at,
                package_json
            )
            values
            (
                $request_id,
                $created_at,
                $package_json
            );
            """;
        command.Parameters.AddWithValue("$request_id", contextPackage.RequestId);
        command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$package_json", JsonSerializer.Serialize(contextPackage, JsonOptions.Default));
        command.ExecuteNonQuery();
        return databasePath;
    }

    /// <summary>
    /// Appends a request record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="request">The request record.</param>
    public void AppendRequest(string projectRoot, ConversationRequestRecord request)
    {
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            insert into conversation_turns
            (
                turn_id,
                request_id,
                request_type,
                source_chat_request_id,
                created_at,
                provider,
                model,
                context_package_path,
                used_rolling_context_id,
                rolling_context_path,
                recent_turn_count,
                compression_request_id,
                compression_status,
                compression_error,
                status,
                error
            )
            values
            (
                $turn_id,
                $request_id,
                $request_type,
                $source_chat_request_id,
                $created_at,
                $provider,
                $model,
                $context_package_path,
                $used_rolling_context_id,
                $rolling_context_path,
                $recent_turn_count,
                $compression_request_id,
                $compression_status,
                $compression_error,
                $status,
                $error
            );
            """;
        command.Parameters.AddWithValue("$turn_id", request.RequestId);
        command.Parameters.AddWithValue("$request_id", request.RequestId);
        command.Parameters.AddWithValue("$request_type", request.RequestType);
        command.Parameters.AddWithValue("$source_chat_request_id", request.SourceChatRequestId);
        command.Parameters.AddWithValue("$created_at", request.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$provider", request.Provider);
        command.Parameters.AddWithValue("$model", request.Model);
        command.Parameters.AddWithValue("$context_package_path", request.ContextPackagePath);
        command.Parameters.AddWithValue("$used_rolling_context_id", request.UsedRollingContextId);
        command.Parameters.AddWithValue("$rolling_context_path", request.RollingContextPath);
        command.Parameters.AddWithValue("$recent_turn_count", request.RecentTurnCount);
        command.Parameters.AddWithValue("$compression_request_id", request.CompressionRequestId);
        command.Parameters.AddWithValue("$compression_status", request.CompressionStatus);
        command.Parameters.AddWithValue("$compression_error", request.CompressionError);
        command.Parameters.AddWithValue("$status", request.Status);
        command.Parameters.AddWithValue("$error", request.Error);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Appends a message record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The message record.</param>
    public void AppendMessage(string projectRoot, ConversationMessageRecord message)
    {
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            insert into conversation_messages
            (
                message_id,
                turn_id,
                request_id,
                role,
                content,
                provider,
                model,
                created_at
            )
            values
            (
                $message_id,
                $turn_id,
                $request_id,
                $role,
                $content,
                $provider,
                $model,
                $created_at
            );
            """;
        command.Parameters.AddWithValue("$message_id", message.MessageId);
        command.Parameters.AddWithValue("$turn_id", message.RequestId);
        command.Parameters.AddWithValue("$request_id", message.RequestId);
        command.Parameters.AddWithValue("$role", message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$provider", message.Provider);
        command.Parameters.AddWithValue("$model", message.Model);
        command.Parameters.AddWithValue("$created_at", message.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets recent conversation messages from SQLite.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="maxMessages">The maximum number of messages to return.</param>
    /// <returns>The recent messages in chronological order.</returns>
    public IReadOnlyList<ConversationMessageRecord> GetRecentMessages(string projectRoot, int maxMessages)
    {
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            select
                message_id,
                request_id,
                role,
                content,
                provider,
                model,
                created_at
            from conversation_messages
            order by created_at desc, message_id desc
            limit $limit;
            """;
        command.Parameters.AddWithValue("$limit", maxMessages);

        List<ConversationMessageRecord> messages = [];
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            messages.Add(new ConversationMessageRecord
            {
                MessageId = reader.GetString(0),
                RequestId = reader.GetString(1),
                Role = reader.GetString(2),
                Content = reader.GetString(3),
                Provider = reader.GetString(4),
                Model = reader.GetString(5),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(6))
            });
        }

        messages.Reverse();
        return messages;
    }

    /// <summary>
    /// Ensures the conversation database exists and has the current schema.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The database file path.</returns>
    public static string EnsureDatabase(string projectRoot)
    {
        string databasePath = GetDatabasePath(projectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? string.Empty);

        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists conversation_turns
            (
                turn_id text primary key,
                request_id text not null unique,
                request_type text not null default 'chat',
                source_chat_request_id text not null default '',
                created_at text not null,
                provider text not null,
                model text not null,
                context_package_path text not null,
                used_rolling_context_id text not null default '',
                rolling_context_path text not null default '',
                recent_turn_count integer not null default 0,
                compression_request_id text not null default '',
                compression_status text not null default '',
                compression_error text not null default '',
                status text not null default 'completed',
                error text not null default ''
            );

            create table if not exists conversation_messages
            (
                message_id text primary key,
                turn_id text not null,
                request_id text not null,
                role text not null,
                content text not null,
                provider text not null,
                model text not null,
                created_at text not null,
                foreign key(turn_id) references conversation_turns(turn_id)
            );

            create table if not exists context_packages
            (
                request_id text primary key,
                created_at text not null,
                package_json text not null
            );
            """;
        command.ExecuteNonQuery();
        EnsureConversationTurnColumns(connection);
        return databasePath;
    }

    /// <summary>
    /// Ensures new conversation_turns columns exist for older databases.
    /// </summary>
    /// <param name="connection">The SQLite connection.</param>
    private static void EnsureConversationTurnColumns(SqliteConnection connection)
    {
        Dictionary<string, string> columns = new Dictionary<string, string>
        {
            ["request_type"] = "text not null default 'chat'",
            ["source_chat_request_id"] = "text not null default ''",
            ["used_rolling_context_id"] = "text not null default ''",
            ["rolling_context_path"] = "text not null default ''",
            ["recent_turn_count"] = "integer not null default 0",
            ["compression_request_id"] = "text not null default ''",
            ["compression_status"] = "text not null default ''",
            ["compression_error"] = "text not null default ''",
            ["status"] = "text not null default 'completed'",
            ["error"] = "text not null default ''"
        };
        HashSet<string> existingColumns = GetColumns(connection, "conversation_turns");

        foreach (KeyValuePair<string, string> column in columns)
        {
            if (existingColumns.Contains(column.Key))
            {
                continue;
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"alter table conversation_turns add column {column.Key} {column.Value};";
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Gets existing table columns.
    /// </summary>
    /// <param name="connection">The SQLite connection.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The column names.</returns>
    private static HashSet<string> GetColumns(SqliteConnection connection, string tableName)
    {
        HashSet<string> columns = new HashSet<string>(StringComparer.Ordinal);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"pragma table_info({tableName});";
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    /// <summary>
    /// Gets the conversation database path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The conversation database path.</returns>
    private static string GetDatabasePath(string projectRoot)
    {
        return Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.ConversationsDirectoryName,
            LlmIdeLayout.ConversationDatabaseFileName);
    }

    /// <summary>
    /// Opens a SQLite connection.
    /// </summary>
    /// <param name="databasePath">The database path.</param>
    /// <returns>The open SQLite connection.</returns>
    private static SqliteConnection OpenConnection(string databasePath)
    {
        SqliteConnection connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }
}
