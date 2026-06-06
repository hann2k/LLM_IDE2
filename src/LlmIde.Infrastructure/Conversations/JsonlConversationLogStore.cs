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
}
