using LlmIde.Core.Conversations;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Conversations;

/// <summary>
/// Writes conversation records to multiple stores.
/// </summary>
public sealed class CompositeConversationLogStore : IConversationLogStore
{
    /// <summary>
    /// The inner stores.
    /// </summary>
    private readonly IReadOnlyList<IConversationLogStore> stores;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeConversationLogStore"/> class.
    /// </summary>
    /// <param name="stores">The stores to write to.</param>
    public CompositeConversationLogStore(IReadOnlyList<IConversationLogStore> stores)
    {
        Log.Ins.Debug("시작");
        this.stores = stores;
    }

    /// <summary>
    /// Saves a context package.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="contextPackage">The context package.</param>
    /// <returns>The first stored context package path.</returns>
    public string SaveContextPackage(string projectRoot, ContextPackage contextPackage)
    {
        Log.Ins.Debug("시작");
        string firstPath = string.Empty;

        foreach (IConversationLogStore store in stores)
        {
            string path = store.SaveContextPackage(projectRoot, contextPackage);

            if (string.IsNullOrEmpty(firstPath))
            {
                firstPath = path;
            }
        }

        return firstPath;
    }

    /// <summary>
    /// Gets the next sequential request identifier number from all stores.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next request sequence number.</returns>
    public long GetNextRequestSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        long nextSequence = 1;

        foreach (IConversationLogStore store in stores)
        {
            long storeSequence = store.GetNextRequestSequence(projectRoot);

            if (storeSequence > nextSequence)
            {
                nextSequence = storeSequence;
            }
        }

        return nextSequence;
    }

    /// <summary>
    /// Gets the next sequential message identifier number from all stores.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next message sequence number.</returns>
    public long GetNextMessageSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        long nextSequence = 1;

        foreach (IConversationLogStore store in stores)
        {
            long storeSequence = store.GetNextMessageSequence(projectRoot);

            if (storeSequence > nextSequence)
            {
                nextSequence = storeSequence;
            }
        }

        return nextSequence;
    }

    /// <summary>
    /// Gets the next compression request identifier number from all stores (negative sequence,
    /// DEC-085). The most advanced value across stores is the smallest (most negative) one.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The next compression sequence number.</returns>
    public long GetNextCompressionRequestSequence(string projectRoot)
    {
        Log.Ins.Debug("시작");
        long nextSequence = -1;

        foreach (IConversationLogStore store in stores)
        {
            long storeSequence = store.GetNextCompressionRequestSequence(projectRoot);

            if (storeSequence < nextSequence)
            {
                nextSequence = storeSequence;
            }
        }

        return nextSequence;
    }

    /// <summary>
    /// Appends a request record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="request">The request record.</param>
    public void AppendRequest(string projectRoot, ConversationRequestRecord request)
    {
        Log.Ins.Debug("시작");
        foreach (IConversationLogStore store in stores)
        {
            store.AppendRequest(projectRoot, request);
        }
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
        foreach (IConversationLogStore store in stores)
        {
            store.UpdateRequestImportanceWeight(projectRoot, requestId, importanceWeight);
        }
    }

    /// <summary>
    /// Appends a message record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The message record.</param>
    public void AppendMessage(string projectRoot, ConversationMessageRecord message)
    {
        Log.Ins.Debug("시작");
        foreach (IConversationLogStore store in stores)
        {
            store.AppendMessage(projectRoot, message);
        }
    }

    /// <summary>
    /// Appends an agent tool call record to all stores.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="toolCall">The tool call record.</param>
    public void AppendToolCall(string projectRoot, ConversationToolCallRecord toolCall)
    {
        Log.Ins.Debug("시작");
        foreach (IConversationLogStore store in stores)
        {
            store.AppendToolCall(projectRoot, toolCall);
        }
    }

    /// <summary>
    /// Deletes a conversation from all stores.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The chat request identifier.</param>
    public void DeleteConversation(string projectRoot, string requestId)
    {
        Log.Ins.Debug("시작");
        foreach (IConversationLogStore store in stores)
        {
            store.DeleteConversation(projectRoot, requestId);
        }
    }

    /// <summary>
    /// Updates the stored assistant message content for a request in all stores.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="requestId">The conversation request identifier.</param>
    /// <param name="content">The new assistant message content.</param>
    public void UpdateAssistantMessageContent(string projectRoot, string requestId, string content)
    {
        Log.Ins.Debug("시작");
        foreach (IConversationLogStore store in stores)
        {
            store.UpdateAssistantMessageContent(projectRoot, requestId, content);
        }
    }

    /// <summary>
    /// Gets recent conversation messages from the primary readable store.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="maxMessages">The maximum number of messages to return.</param>
    /// <returns>The recent messages.</returns>
    public IReadOnlyList<ConversationMessageRecord> GetRecentMessages(string projectRoot, int maxMessages)
    {
        Log.Ins.Debug("시작");
        foreach (IConversationLogStore store in stores.Reverse())
        {
            IReadOnlyList<ConversationMessageRecord> messages = store.GetRecentMessages(projectRoot, maxMessages);

            if (messages.Count > 0)
            {
                return messages;
            }
        }

        return [];
    }
}
