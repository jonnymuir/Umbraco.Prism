namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Request body for proposal apply-and-publish operations.
/// The approver identity is derived from the authenticated principal on the request;
/// it is never accepted from the body to prevent authorship spoofing.
/// </summary>
public record ApplyServiceBlueprintRequest
{
    public required ProposalEnvelope Envelope { get; init; }
}
