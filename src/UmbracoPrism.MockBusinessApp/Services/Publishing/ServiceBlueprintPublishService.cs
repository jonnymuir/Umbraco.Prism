using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;
using UmbracoPrism.ProcessManager.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services.Publishing;

/// <summary>
/// Deterministic publish pipeline from authored workflow to persisted runtime definition.
/// </summary>
public sealed class ServiceBlueprintPublishService(
    IServiceBlueprintProjector projector,
    IServiceBlueprintSourceStore publishedWorkflowStore) : IServiceBlueprintPublishService
{
    public async Task<PublishPreviewResult> PreviewAsync(AuthoredServiceBlueprint workflow, CancellationToken ct = default)
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
                : ServiceBlueprintProjector.ComputeCanonicalChecksum(currentPublishedFile)
        };
    }

    public async Task<PublishResult> PublishAsync(AuthoredServiceBlueprint workflow, CancellationToken ct = default)
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

        // This pipeline predates optimistic concurrency and isn't wired to a live endpoint —
        // it publishes unconditionally against whatever it just loaded in PreviewAsync, mirroring
        // its prior last-write-wins behavior. A live host wiring this up should thread a real
        // caller-supplied expected version through instead.
        var expectedVersion = preview.CurrentPublishedFile?.Version ?? 0;
        var saveResult = await publishedWorkflowStore.SaveAsync(preview.File, expectedVersion, ct);
        if (!saveResult.Saved)
        {
            throw new InvalidOperationException(
                $"Published workflow '{workflow.DefinitionKey}' could not be saved: expected version " +
                $"{expectedVersion} but current version is {saveResult.CurrentVersion}.");
        }

        var publishedPath = saveResult.Location;
        var verifiedFile = await publishedWorkflowStore.LoadAsync(workflow.DefinitionKey, ct)
            ?? throw new InvalidOperationException(
                $"Published workflow '{workflow.DefinitionKey}' could not be reloaded for verification.");
        var verifiedChecksum = ServiceBlueprintProjector.ComputeCanonicalChecksum(verifiedFile);

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
        ServiceBlueprint expected,
        ServiceBlueprint actual) =>
        ServiceBlueprintProjector.SerializeCanonical(expected).AsSpan()
            .SequenceEqual(ServiceBlueprintProjector.SerializeCanonical(actual));
}
