using System.Text.Json;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Microsoft.Data.Sqlite;
using Framework.Common.Logger;

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
        Log.Ins.Debug("시작");
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
    /// Gets the next sequential request identifier number.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next request sequence number.</returns>
    public long GetNextRequestSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        return GetNextSequence(connection, "select request_id from conversation_turns;");
    }

    /// <summary>
    /// Gets the next sequential message identifier number.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next message sequence number.</returns>
    public long GetNextMessageSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        return GetNextSequence(connection, "select message_id from conversation_messages;");
    }

    /// <summary>
    /// Gets the next compression request identifier number (negative sequence, DEC-085).
    /// Scans stored compression_request_id values too, so identifiers assigned to skipped or
    /// failed compressions are never reused.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next compression sequence number.</returns>
    public long GetNextCompressionRequestSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        return GetNextCompressionSequence(
            connection,
            """
            select request_id from conversation_turns
            union all
            select compression_request_id from conversation_turns;
            """);
    }

    /// <summary>
    /// Appends a request record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="request">The request record.</param>
    public void AppendRequest(string projectRoot, ConversationRequestRecord request)
    {
        Log.Ins.Debug("시작");
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
                importance_weight,
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
                $importance_weight,
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
        command.Parameters.AddWithValue("$importance_weight", request.ImportanceWeight);
        command.Parameters.AddWithValue("$compression_request_id", request.CompressionRequestId);
        command.Parameters.AddWithValue("$compression_status", request.CompressionStatus);
        command.Parameters.AddWithValue("$compression_error", request.CompressionError);
        command.Parameters.AddWithValue("$status", request.Status);
        command.Parameters.AddWithValue("$error", request.Error);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Updates the importance weight for a stored request.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="importanceWeight">The importance weight from 0 to 10.</param>
    public void UpdateRequestImportanceWeight(string projectRoot, string requestId, int importanceWeight)
    {
        Log.Ins.Debug("시작");
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            update conversation_turns
            set importance_weight = $importance_weight
            where request_id = $request_id;
            """;
        command.Parameters.AddWithValue("$importance_weight", importanceWeight);
        command.Parameters.AddWithValue("$request_id", requestId);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Appends a message record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The message record.</param>
    public void AppendMessage(string projectRoot, ConversationMessageRecord message)
    {
        Log.Ins.Debug("시작");
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
    /// Appends an agent tool call record to SQLite.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="toolCall">The tool call record.</param>
    public void AppendToolCall(string projectRoot, ConversationToolCallRecord toolCall)
    {
        Log.Ins.Debug("시작");
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            insert into agent_tool_calls
            (
                request_id,
                sequence,
                tool,
                target,
                ok,
                result_summary,
                error_message,
                created_at
            )
            values
            (
                $request_id,
                $sequence,
                $tool,
                $target,
                $ok,
                $result_summary,
                $error_message,
                $created_at
            );
            """;
        command.Parameters.AddWithValue("$request_id", toolCall.RequestId);
        command.Parameters.AddWithValue("$sequence", toolCall.Sequence);
        command.Parameters.AddWithValue("$tool", toolCall.Tool);
        command.Parameters.AddWithValue("$target", toolCall.Target);
        command.Parameters.AddWithValue("$ok", toolCall.Ok ? 1 : 0);
        command.Parameters.AddWithValue("$result_summary", toolCall.ResultSummary);
        command.Parameters.AddWithValue("$error_message", toolCall.ErrorMessage);
        command.Parameters.AddWithValue("$created_at", toolCall.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes a conversation and its compression turn from SQLite. Rolling context is left intact.
    /// The compression turn is resolved via the linkage fields (compression_request_id /
    /// source_chat_request_id, DEC-085); the legacy 'c'-suffix identifier is still removed for old data.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The chat request identifier.</param>
    public void DeleteConversation(string projectRoot, string requestId)
    {
        Log.Ins.Debug("시작");
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);

        // Collect all identifiers to remove: the chat itself, its linked compression turn(s),
        // and the legacy 'c'-suffix compression identifier.
        List<string> targetIds = [requestId, requestId + "c"];

        using (SqliteCommand lookup = connection.CreateCommand())
        {
            lookup.CommandText = """
                select compression_request_id from conversation_turns where request_id = $request_id
                union
                select request_id from conversation_turns where source_chat_request_id = $request_id;
                """;
            lookup.Parameters.AddWithValue("$request_id", requestId);
            using SqliteDataReader reader = lookup.ExecuteReader();

            while (reader.Read())
            {
                string linkedId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);

                if (!string.IsNullOrWhiteSpace(linkedId) && !targetIds.Contains(linkedId))
                {
                    targetIds.Add(linkedId);
                }
            }
        }

        string parameterList = string.Join(", ", Enumerable.Range(0, targetIds.Count).Select(index => $"$id{index}"));
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            delete from conversation_messages where request_id in ({parameterList});
            delete from agent_tool_calls where request_id in ({parameterList});
            delete from context_packages where request_id in ({parameterList});
            delete from conversation_turns where request_id in ({parameterList});
            """;

        for (int index = 0; index < targetIds.Count; index++)
        {
            command.Parameters.AddWithValue($"$id{index}", targetIds[index]);
        }

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Updates the stored assistant message content for a request in SQLite.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The conversation request identifier.</param>
    /// <param name="content">The new assistant message content.</param>
    public void UpdateAssistantMessageContent(string projectRoot, string requestId, string content)
    {
        Log.Ins.Debug("시작");
        string databasePath = EnsureDatabase(projectRoot);
        using SqliteConnection connection = OpenConnection(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            update conversation_messages
            set content = $content
            where request_id = $request_id and role = 'assistant';
            """;
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$request_id", requestId);
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
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
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
                importance_weight integer not null default 0,
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

            create table if not exists agent_tool_calls
            (
                id integer primary key autoincrement,
                request_id text not null,
                sequence integer not null,
                tool text not null,
                target text not null default '',
                ok integer not null default 0,
                result_summary text not null default '',
                error_message text not null default '',
                created_at text not null
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
        Log.Ins.Debug("시작");
        Dictionary<string, string> columns = new Dictionary<string, string>
        {
            ["request_type"] = "text not null default 'chat'",
            ["source_chat_request_id"] = "text not null default ''",
            ["used_rolling_context_id"] = "text not null default ''",
            ["rolling_context_path"] = "text not null default ''",
            ["recent_turn_count"] = "integer not null default 0",
            ["importance_weight"] = "integer not null default 0",
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
        Log.Ins.Debug("시작");
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
    /// Gets the next sequence number from stored identifiers.
    /// </summary>
    /// <param name="connection">The SQLite connection.</param>
    /// <param name="selectIdentifiersSql">The SQL that selects identifier values.</param>
    /// <returns>The next sequence number.</returns>
    private static long GetNextSequence(SqliteConnection connection, string selectIdentifiersSql)
    {
        Log.Ins.Debug("시작");
        long maxSequence = 0;
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = selectIdentifiersSql;
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (!ConversationSequence.TryParse(reader.GetString(0), out long sequence))
            {
                continue;
            }

            if (sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return maxSequence + 1;
    }

    /// <summary>
    /// Gets the next compression sequence number from stored identifiers: one below the most
    /// negative stored sequence, starting at -1 (DEC-085). Positive and legacy identifiers are ignored.
    /// </summary>
    /// <param name="connection">The SQLite connection.</param>
    /// <param name="selectIdentifiersSql">The SQL that selects identifier values.</param>
    /// <returns>The next compression sequence number.</returns>
    private static long GetNextCompressionSequence(SqliteConnection connection, string selectIdentifiersSql)
    {
        Log.Ins.Debug("시작");
        long minSequence = 0;
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = selectIdentifiersSql;
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (reader.IsDBNull(0) || !ConversationSequence.TryParse(reader.GetString(0), out long sequence))
            {
                continue;
            }

            if (sequence < minSequence)
            {
                minSequence = sequence;
            }
        }

        return minSequence - 1;
    }

    /// <summary>
    /// Gets the conversation database path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The conversation database path.</returns>
    private static string GetDatabasePath(string projectRoot)
    {
        Log.Ins.Debug("시작");
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
        Log.Ins.Debug("시작");
        SqliteConnection connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }
}
