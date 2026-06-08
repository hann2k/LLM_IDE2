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
    /// Gets the next sequential request identifier number.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next request sequence number.</returns>
    long GetNextRequestSequence(string projectRoot);

    /// <summary>
    /// Gets the next sequential message identifier number.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next message sequence number.</returns>
    long GetNextMessageSequence(string projectRoot);

    /// <summary>
    /// Appends a request record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="request">The request record.</param>
    void AppendRequest(string projectRoot, ConversationRequestRecord request);

    /// <summary>
    /// Updates the importance weight for a stored request.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="importanceWeight">The importance weight from 0 to 10.</param>
    void UpdateRequestImportanceWeight(string projectRoot, string requestId, int importanceWeight);

    /// <summary>
    /// Appends a message record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The message record.</param>
    void AppendMessage(string projectRoot, ConversationMessageRecord message);

    /// <summary>
    /// Appends an agent tool call record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="toolCall">The tool call record.</param>
    void AppendToolCall(string projectRoot, ConversationToolCallRecord toolCall);

    /// <summary>
    /// Gets recent conversation messages in chronological order.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="maxMessages">The maximum number of messages to return.</param>
    /// <returns>The recent messages.</returns>
    IReadOnlyList<ConversationMessageRecord> GetRecentMessages(string projectRoot, int maxMessages);
}
