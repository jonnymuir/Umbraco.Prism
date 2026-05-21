namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Persists authoring provenance records for applied workflow changes.
/// </summary>
public interface IWorkflowAuthoringProvenanceStore
{
    Task<string?> SaveAsync(
        string workflowKey,
        ProposalEnvelope envelope,
        string approver,
        CancellationToken ct = default);
}
