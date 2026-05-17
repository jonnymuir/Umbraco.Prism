using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// The output of <see cref="IWorkflowPreviewService.Preview"/>, combining the projected
/// runtime file, a semantic diff vs the original, and a deterministic happy-path journey trace.
/// </summary>
public record PreviewResult
{
    public required WorkflowDefinitionFile ProjectedFile { get; init; }
    public required string Checksum { get; init; }
    public IReadOnlyList<ProjectionDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<DiffEntry> Diff { get; init; } = [];

    /// <summary>
    /// Ordered list of stage keys from the entry stage to the first terminal stage,
    /// produced by following the first available transition at each step.
    /// </summary>
    public IReadOnlyList<string> JourneyTrace { get; init; } = [];
}
