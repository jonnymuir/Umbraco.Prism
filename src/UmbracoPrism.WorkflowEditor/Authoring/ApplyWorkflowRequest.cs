namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>Request body for proposal apply-and-publish operations.</summary>
public record ApplyWorkflowRequest
{
    public required ProposalEnvelope Envelope { get; init; }

    public required string Approver { get; init; }
}
