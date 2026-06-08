using System.Text.Json;
using LlmIde.Core.Conversations;
using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Json;

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
        string toolCallPath = Path.Combine(GetConversationsDirectory(projectRoot), LlmIdeLayout.ToolCallsFileName);
        AppendJsonLine(toolCallPath, toolCall);
    }

    /// <summary>
    /// Gets recent conversation messages from the JSONL message log.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="maxMessages">The maximum number of messages to return.</param>
    /// <returns>The recent messages.</returns>
    public IReadOnlyList<ConversationMessageRecord> GetRecentMessages(string projectRoot, int maxMessages)
    {
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
        List<string> lines = [];

        foreach (TValue value in values)
        {
            lines.Add(JsonSerializer.Serialize(value, JsonOptions.Compact));
        }

        File.WriteAllLines(path, lines);
    }
}
