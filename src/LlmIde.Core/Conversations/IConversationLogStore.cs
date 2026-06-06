namespace LlmIde.Core.Conversations;

/// <summary>
/// Stores conversation logs and context packages.
/// </summary>
public interface IConversationLogStore
{
    /// <summary>
    /// Saves a context package.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="contextPackage">The context package.</param>
    /// <returns>The stored context package path.</returns>
    string SaveContextPackage(string projectRoot, ContextPackage contextPackage);

    /// <summary>
    /// Appends a request record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="request">The request record.</param>
    void AppendRequest(string projectRoot, ConversationRequestRecord request);

    /// <summary>
    /// Appends a message record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The message record.</param>
    void AppendMessage(string projectRoot, ConversationMessageRecord message);

    /// <summary>
    /// Gets recent conversation messages in chronological order.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="maxMessages">The maximum number of messages to return.</param>
    /// <returns>The recent messages.</returns>
    IReadOnlyList<ConversationMessageRecord> GetRecentMessages(string projectRoot, int maxMessages);
}
