using System.Collections.Concurrent;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// In-memory implementation of <see cref="IWorkflowAuthoringProvenanceStore"/>.
/// Useful for repeatable demos and tests that should not write to disk.
/// </summary>
public sealed class InMemoryWorkflowAuthoringProvenanceStore : IWorkflowAuthoringProvenanceStore
{
    private readonly ConcurrentDictionary<string, ProvenanceRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public Task<string?> SaveAsync(
        string workflowKey,
        ProposalEnvelope envelope,
        string approver,
        CancellationToken ct = default)
    {
        var location = $"memory://workflow-provenance/{workflowKey}/{Guid.NewGuid():N}";
        _records[location] = new ProvenanceRecord(workflowKey, envelope, approver);
        return Task.FromResult<string?>(location);
    }

    internal bool TryGet(string location, out ProvenanceRecord? record)
    {
        if (_records.TryGetValue(location, out var stored))
        {
            record = stored;
            return true;
        }

        record = null;
        return false;
    }

    internal sealed record ProvenanceRecord(string WorkflowKey, ProposalEnvelope Envelope, string Approver);
}
