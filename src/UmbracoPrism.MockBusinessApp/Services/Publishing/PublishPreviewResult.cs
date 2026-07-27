using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.MockBusinessApp.Services.Publishing;

/// <summary>
/// Side-effect-free publish preview describing what would be written to the runtime store.
/// </summary>
public record PublishPreviewResult : ProjectionResult
{
    public ServiceBlueprint? CurrentPublishedFile { get; init; }

    public string? CurrentPublishedChecksum { get; init; }

    public bool WouldChange =>
        !string.Equals(CurrentPublishedChecksum, Checksum, StringComparison.Ordinal);
}
