using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Side-effect-free publish preview describing what would be written to the runtime store.
/// </summary>
public record PublishPreviewResult : ProjectionResult
{
    public WorkflowDefinitionFile? CurrentPublishedFile { get; init; }

    public string? CurrentPublishedChecksum { get; init; }

    public bool WouldChange =>
        !string.Equals(CurrentPublishedChecksum, Checksum, StringComparison.Ordinal);
}
