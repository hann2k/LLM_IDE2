using System;
using System.Windows.Threading;
using LlmIde.Core.Agents;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Bridges the body-editing agent tools to the WPF editor. Body reads are marshalled to the UI thread;
/// a proposed revision is stored (thread-safe) for the UI to present for approval after the agent loop.
/// </summary>
public sealed class WriterDocumentBodyBridge : IDocumentBodyBridge
{
    private readonly Dispatcher dispatcher;
    private readonly Func<DocumentBodySnapshot?> readCurrentBody;
    private readonly object gate = new object();
    private string? pendingProposal;

    /// <summary>
    /// Initializes a new instance of the <see cref="WriterDocumentBodyBridge"/> class.
    /// </summary>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="readCurrentBody">Reads the current section snapshot (invoked on the UI thread).</param>
    public WriterDocumentBodyBridge(Dispatcher dispatcher, Func<DocumentBodySnapshot?> readCurrentBody)
    {
        Log.Ins.Debug("시작");
        this.dispatcher = dispatcher;
        this.readCurrentBody = readCurrentBody;
    }

    /// <summary>
    /// Reads the current section body on the UI thread.
    /// </summary>
    /// <returns>The body snapshot, or null when no section is selected.</returns>
    public DocumentBodySnapshot? GetCurrentBody()
    {
        Log.Ins.Debug("시작");
        return dispatcher.Invoke(readCurrentBody);
    }

    /// <summary>
    /// Stores a proposed body revision (called from the agent/background thread).
    /// </summary>
    /// <param name="revisedBody">The proposed full body.</param>
    public void ProposeBodyRevision(string revisedBody)
    {
        Log.Ins.Debug("시작");
        lock (gate)
        {
            pendingProposal = revisedBody;
        }
    }

    /// <summary>
    /// Returns and clears the pending proposal (called on the UI thread after the agent loop).
    /// </summary>
    /// <returns>The pending proposal, or null when there is none.</returns>
    public string? TakePendingProposal()
    {
        Log.Ins.Debug("시작");
        lock (gate)
        {
            string? proposal = pendingProposal;
            pendingProposal = null;
            return proposal;
        }
    }
}
