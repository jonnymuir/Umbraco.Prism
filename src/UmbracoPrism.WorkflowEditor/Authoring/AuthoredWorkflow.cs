namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Editor-native representation of a workflow — the human and agent authoring source of truth.
/// Never loaded directly by the Prism runtime; compiled to <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile"/>
/// by <see cref="IWorkflowProjector"/>.
/// </summary>
public record AuthoredWorkflow
{
    /// <summary>
    /// Stable surrogate identifier. Unlike <see cref="DefinitionKey"/>, this is never repurposed
    /// even if the key is renamed (migration scenario).
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Slug that maps to <c>WorkflowDefinitionFile.DefinitionKey</c> on projection.
    /// Once set, must not change without a migration step.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>Human-readable display name. Maps to <c>WorkflowDefinitionFile.DisplayName</c>.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Monotonically increasing version. Incremented by <c>ApplyPatch</c> on each committed edit.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Optional free-text description of the workflow's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>Authored schema version (separate from business version). Used for migration guards.</summary>
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// The <see cref="AuthoredStage.StageKey"/> of the stage that becomes <c>WorkflowDefinitionFile.InitialState</c>.
    /// Must reference a stage in <see cref="Stages"/>.
    /// </summary>
    public required string InitialStageKey { get; init; }

    /// <summary>Instance creation policy forwarded verbatim to the runtime.</summary>
    public string InstancePolicy { get; init; } = "single";

    /// <summary>All stages in this workflow. Order is informational; graph edges define execution order.</summary>
    public IReadOnlyList<AuthoredStage> Stages { get; init; } = [];

    /// <summary>All transitions (graph edges). Projected 1:1 to <c>WorkflowTransitionFile</c>.</summary>
    public IReadOnlyList<AuthoredTransition> Transitions { get; init; } = [];

    /// <summary>Named handoff boundaries between stages, used as agent insertion points.</summary>
    public IReadOnlyList<AuthoredHandoff> Handoffs { get; init; } = [];

    /// <summary>Arbitrary string key–value metadata (e.g. owner, service-area tags).</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Editor comment for the current revision. Stripped during projection.</summary>
    public string? AuthorNote { get; init; }
}
