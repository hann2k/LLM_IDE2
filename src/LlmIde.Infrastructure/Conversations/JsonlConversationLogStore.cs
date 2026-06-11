using System.Text.Json;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Conversations;

/// <summary>
/// Stores conversation logs as JSON and JSONL files.
/// </summary>
public sealed class JsonlConversationLogStore : IConversationLogStore
{
    /// <summary>
    /// Saves a context package.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="contextPackage">The context package.</param>
    /// <returns>The stored context package path.</returns>
    public string SaveContextPackage(string projectRoot, ContextPackage contextPackage)
    {
        Log.Ins.Debug("시작");
        string packageDirectory = Path.Combine(
            GetConversationsDirectory(projectRoot),
            LlmIdeLayout.ContextPackagesDirectoryName);
        Directory.CreateDirectory(packageDirectory);

        string packagePath = Path.Combine(packageDirectory, $"{contextPackage.RequestId}.json");
        string json = JsonSerializer.Serialize(contextPackage, JsonOptions.Default);
        File.WriteAllText(packagePath, json);
        return packagePath;
    }

    /// <summary>
    /// Gets the next sequential request identifier number.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next request sequence number.</returns>
    public long GetNextRequestSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string requestPath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.RequestsFileName);
        long maxSequence = 0;

        foreach (ConversationRequestRecord request in ReadJsonLines<ConversationRequestRecord>(requestPath))
        {
            if (!ConversationSequence.TryParse(request.RequestId, out long sequence))
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
    /// Gets the next sequential message identifier number.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next message sequence number.</returns>
    public long GetNextMessageSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string messagePath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.MessagesFileName);
        long maxSequence = 0;

        foreach (ConversationMessageRecord message in ReadJsonLines<ConversationMessageRecord>(messagePath))
        {
            if (!ConversationSequence.TryParse(message.MessageId, out long sequence))
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
    /// Appends a request record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="request">The request record.</param>
    public void AppendRequest(string projectRoot, ConversationRequestRecord request)
    {
        Log.Ins.Debug("시작");
        string requestPath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.RequestsFileName);
        AppendJsonLine(requestPath, request);
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
        string requestPath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.RequestsFileName);
        List<ConversationRequestRecord> requests = ReadJsonLines<ConversationRequestRecord>(requestPath);
        bool updated = false;

        foreach (ConversationRequestRecord request in requests)
        {
            if (!string.Equals(request.RequestId, requestId, StringComparison.Ordinal))
            {
                continue;
            }

            request.ImportanceWeight = importanceWeight;
            updated = true;
            break;
        }

        if (!updated)
        {
            return;
        }

        WriteJsonLines(requestPath, requests);
    }

    /// <summary>
    /// Appends a message record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The message record.</param>
    public void AppendMessage(string projectRoot, ConversationMessageRecord message)
    {
        Log.Ins.Debug("시작");
        string messagePath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.MessagesFileName);
        AppendJsonLine(messagePath, message);
    }

    /// <summary>
    /// Appends an agent tool call record to the JSONL tool call log.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="toolCall">The tool call record.</param>
    public void AppendToolCall(string projectRoot, ConversationToolCallRecord toolCall)
    {
        Log.Ins.Debug("시작");
        string toolCallPath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.ToolCallsFileName);
        AppendJsonLine(toolCallPath, toolCall);
    }

    /// <summary>
    /// Deletes a conversation and its compression turn from the JSONL logs and context packages.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The chat request identifier.</param>
    public void DeleteConversation(string projectRoot, string requestId)
    {
        Log.Ins.Debug("시작");
        string compressionRequestId = requestId + "c";
        string conversationsDirectory = GetConversationsDirectory(projectRoot);

        string messagePath = Path.Combine(conversationsDirectory, LlmIdeLayout.MessagesFileName);
        RemoveJsonLines<ConversationMessageRecord>(
            messagePath,
            record => IsTargetRequest(record.RequestId, requestId, compressionRequestId));

        string requestPath = Path.Combine(conversationsDirectory, LlmIdeLayout.RequestsFileName);
        RemoveJsonLines<ConversationRequestRecord>(
            requestPath,
            record => IsTargetRequest(record.RequestId, requestId, compressionRequestId));

        string toolCallPath = Path.Combine(conversationsDirectory, LlmIdeLayout.ToolCallsFileName);
        RemoveJsonLines<ConversationToolCallRecord>(
            toolCallPath,
            record => IsTargetRequest(record.RequestId, requestId, compressionRequestId));

        string packageDirectory = Path.Combine(conversationsDirectory, LlmIdeLayout.ContextPackagesDirectoryName);
        DeleteFileIfExists(Path.Combine(packageDirectory, $"{requestId}.json"));
        DeleteFileIfExists(Path.Combine(packageDirectory, $"{compressionRequestId}.json"));
    }

    /// <summary>
    /// Updates the stored assistant message content for a request in the JSONL message log.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The conversation request identifier.</param>
    /// <param name="content">The new assistant message content.</param>
    public void UpdateAssistantMessageContent(string projectRoot, string requestId, string content)
    {
        Log.Ins.Debug("시작");
        string messagePath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.MessagesFileName);

        if (!File.Exists(messagePath))
        {
            return;
        }

        List<ConversationMessageRecord> messages = ReadJsonLines<ConversationMessageRecord>(messagePath);
        bool updated = false;

        foreach (ConversationMessageRecord message in messages)
        {
            if (string.Equals(message.RequestId, requestId, StringComparison.Ordinal)
                && string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                message.Content = content;
                updated = true;
                break;
            }
        }

        if (updated)
        {
            WriteJsonLines(messagePath, messages);
        }
    }

    /// <summary>
    /// Determines whether a record's request identifier is the chat or its compression request.
    /// </summary>
    /// <param name="recordRequestId">The record's request identifier.</param>
    /// <param name="requestId">The chat request identifier.</param>
    /// <param name="compressionRequestId">The compression request identifier.</param>
    /// <returns>True when the record belongs to the conversation being deleted.</returns>
    private static bool IsTargetRequest(string recordRequestId, string requestId, string compressionRequestId)
    {
        Log.Ins.Debug("시작");
        return string.Equals(recordRequestId, requestId, StringComparison.Ordinal)
            || string.Equals(recordRequestId, compressionRequestId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rewrites a JSONL file without the records matching a predicate.
    /// </summary>
    /// <typeparam name="TValue">The record type.</typeparam>
    /// <param name="path">The JSONL file path.</param>
    /// <param name="shouldRemove">The predicate selecting records to remove.</param>
    private static void RemoveJsonLines<TValue>(string path, Func<TValue, bool> shouldRemove)
    {
        Log.Ins.Debug("시작");
        if (!File.Exists(path))
        {
            return;
        }

        List<TValue> kept = [];

        foreach (TValue value in ReadJsonLines<TValue>(path))
        {
            if (!shouldRemove(value))
            {
                kept.Add(value);
            }
        }

        WriteJsonLines(path, kept);
    }

    /// <summary>
    /// Deletes a file when it exists.
    /// </summary>
    /// <param name="path">The file path.</param>
    private static void DeleteFileIfExists(string path)
    {
        Log.Ins.Debug("시작");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Gets recent conversation messages from the JSONL message log.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="maxMessages">The maximum number of messages to return.</param>
    /// <returns>The recent messages.</returns>
    public IReadOnlyList<ConversationMessageRecord> GetRecentMessages(string projectRoot, int maxMessages)
    {
        Log.Ins.Debug("시작");
        string messagePath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.MessagesFileName);

        if (!File.Exists(messagePath))
        {
            return [];
        }

        return File.ReadLines(messagePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<ConversationMessageRecord>(line, JsonOptions.Compact))
            .Where(message => message is not null)
            .Cast<ConversationMessageRecord>()
            .TakeLast(maxMessages)
            .ToList();
    }

    /// <summary>
    /// Reads JSONL records from a file.
    /// </summary>
    /// <typeparam name="TValue">The record type.</typeparam>
    /// <param name="path">The JSONL file path.</param>
    /// <returns>The parsed records.</returns>
    private static List<TValue> ReadJsonLines<TValue>(string path)
    {
        Log.Ins.Debug("시작");
        List<TValue> values = [];

        if (!File.Exists(path))
        {
            return values;
        }

        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            TValue? value = JsonSerializer.Deserialize<TValue>(line, JsonOptions.Compact);

            if (value is null)
            {
                continue;
            }

            values.Add(value);
        }

        return values;
    }

    /// <summary>
    /// Gets the conversations directory path.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The conversations directory path.</returns>
    private static string GetConversationsDirectory(string projectRoot)
    {
        Log.Ins.Debug("시작");
        string conversationsDirectory = Path.Combine(
            Path.GetFullPath(projectRoot),
            LlmIdeLayout.MetadataDirectoryName,
            LlmIdeLayout.ConversationsDirectoryName);
        Directory.CreateDirectory(conversationsDirectory);
        return conversationsDirectory;
    }

    /// <summary>
    /// Appends one JSON line to a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="value">The value to append.</param>
    private static void AppendJsonLine<TValue>(string path, TValue value)
    {
        Log.Ins.Debug("시작");
        string json = JsonSerializer.Serialize(value, JsonOptions.Compact);
        File.AppendAllText(path, json + Environment.NewLine);
    }

    /// <summary>
    /// Writes JSONL records to a file.
    /// </summary>
    /// <typeparam name="TValue">The record type.</typeparam>
    /// <param name="path">The JSONL file path.</param>
    /// <param name="values">The records to write.</param>
    private static void WriteJsonLines<TValue>(string path, IReadOnlyList<TValue> values)
    {
        Log.Ins.Debug("시작");
        List<string> lines = [];

        foreach (TValue value in values)
        {
            lines.Add(JsonSerializer.Serialize(value, JsonOptions.Compact));
        }

        File.WriteAllLines(path, lines);
    }
}
