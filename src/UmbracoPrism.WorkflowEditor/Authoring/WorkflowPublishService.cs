using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Deterministic publish pipeline from authored workflow to persisted runtime definition.
/// </summary>
public sealed class WorkflowPublishService(
    IWorkflowProjector projector,
    IPublishedWorkflowStore publishedWorkflowStore) : IWorkflowPublishService
{
    public async Task<PublishPreviewResult> PreviewAsync(AuthoredWorkflow workflow, CancellationToken ct = default)
    {
        var projection = projector.Project(workflow);
        var currentPublishedFile = await publishedWorkflowStore.LoadAsync(workflow.DefinitionKey, ct);

        return new PublishPreviewResult
        {
            File = projection.File,
            Checksum = projection.Checksum,
            Diagnostics = projection.Diagnostics,
            CurrentPublishedFile = currentPublishedFile,
            CurrentPublishedChecksum = currentPublishedFile is null
                ? null
                : WorkflowProjector.ComputeCanonicalChecksum(currentPublishedFile)
        };
    }

    public async Task<PublishResult> PublishAsync(AuthoredWorkflow workflow, CancellationToken ct = default)
    {
        var preview = await PreviewAsync(workflow, ct);
        if (preview.HasErrors)
        {
            return new PublishResult
            {
                File = preview.File,
                Checksum = preview.Checksum,
                Diagnostics = preview.Diagnostics,
                CurrentPublishedFile = preview.CurrentPublishedFile,
                CurrentPublishedChecksum = preview.CurrentPublishedChecksum,
                RoundTripVerified = false
            };
        }

        var publishedPath = await publishedWorkflowStore.SaveAsync(preview.File, ct);
        var verifiedFile = await publishedWorkflowStore.LoadAsync(workflow.DefinitionKey, ct)
            ?? throw new InvalidOperationException(
                $"Published workflow '{workflow.DefinitionKey}' could not be reloaded for verification.");
        var verifiedChecksum = WorkflowProjector.ComputeCanonicalChecksum(verifiedFile);

        return new PublishResult
        {
            File = preview.File,
            Checksum = preview.Checksum,
            Diagnostics = preview.Diagnostics,
            CurrentPublishedFile = preview.CurrentPublishedFile,
            CurrentPublishedChecksum = preview.CurrentPublishedChecksum,
            PublishedPath = publishedPath,
            VerifiedFile = verifiedFile,
            VerifiedChecksum = verifiedChecksum,
            RoundTripVerified = verifiedChecksum == preview.Checksum
                && CanonicalJsonMatches(preview.File, verifiedFile)
        };
    }

    private static bool CanonicalJsonMatches(
        WorkflowDefinitionFile expected,
        WorkflowDefinitionFile actual) =>
        WorkflowProjector.SerializeCanonical(expected).AsSpan()
            .SequenceEqual(WorkflowProjector.SerializeCanonical(actual));
}
