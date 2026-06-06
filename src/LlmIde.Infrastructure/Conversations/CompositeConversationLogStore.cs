using LlmIde.Core.Conversations;

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
    /// Appends a request record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="request">The request record.</param>
    public void AppendRequest(string projectRoot, ConversationRequestRecord request)
    {
        foreach (IConversationLogStore store in stores)
        {
            store.AppendRequest(projectRoot, request);
        }
    }

    /// <summary>
    /// Appends a message record.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="message">The message record.</param>
    public void AppendMessage(string projectRoot, ConversationMessageRecord message)
    {
        foreach (IConversationLogStore store in stores)
        {
            store.AppendMessage(projectRoot, message);
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
