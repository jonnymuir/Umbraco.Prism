namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// A single stage in the authored workflow. Maps to one or more runtime states on projection.
/// The <see cref="Kind"/> property drives component shell selection; shell type is ultimately
/// confirmed by <see cref="UmbracoPrism.Shared.Extensions.PrismComponentExtensions.InferStepType"/>.
/// </summary>
public record AuthoredStage
{
    /// <summary>Stable key unique within this workflow. Maps to <c>StepDefinition.StateKey</c> on projection.</summary>
    public required string StageKey { get; init; }

    /// <summary>User-facing display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Shell intent used by the projector to choose which components to emit.</summary>
    public StageKind Kind { get; init; } = StageKind.Question;

    /// <summary>
    /// The role or persona responsible for acting on this stage (e.g. "applicant", "caseworker").
    /// Informational; not projected into the runtime.
    /// </summary>
    public string? Actor { get; init; }

    /// <summary>
    /// Fields collected or displayed in this stage.
    /// For <see cref="StageKind.Question"/> stages these become InputComponents inside a FieldsetComponent.
    /// For <see cref="StageKind.CheckAnswers"/> stages on the parent workflow they populate the SummaryListComponent.
    /// </summary>
    public IReadOnlyList<AuthoredField> Fields { get; init; } = [];

    /// <summary>Roles that may enter this stage. Empty means any authenticated principal.</summary>
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    /// <summary>Waiting configuration, required for <see cref="StageKind.Waiting"/> and <see cref="StageKind.StatusTimeline"/> stages.</summary>
    public WaitingMetadata? Waiting { get; init; }

    /// <summary>Editor-only comment, stripped during projection.</summary>
    public string? EditorComment { get; init; }
}
