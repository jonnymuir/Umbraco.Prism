using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Result of publishing an authored workflow to the runtime definition store.
/// </summary>
public record PublishResult : PublishPreviewResult
{
    public string? SavedPath { get; init; }

    public string? PublishedPath { get; init; }

    public WorkflowDefinitionFile? VerifiedFile { get; init; }

    public string? VerifiedChecksum { get; init; }

    public bool RoundTripVerified { get; init; }
}
