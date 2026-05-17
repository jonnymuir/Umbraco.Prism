using System.Text.Json;

namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>The agent or human actor that submitted this proposal.</summary>
public record PatchAgent
{
    public required string Kind { get; init; }       // github-copilot | custom-agent | human-assisted
    public required string Identity { get; init; }
    public string? SessionRef { get; init; }
}

/// <summary>A single mutation to apply to the authored workflow.</summary>
public record PatchOp
{
    public required string Op { get; init; }   // insert-stage | remove-stage | update-stage | insert-handoff | update-transition
    public string? Path { get; init; }         // JSON Pointer into the authored model
    public JsonElement? Value { get; init; }  // The authored stage / handoff / transition to insert or set
    public string? Before { get; init; }      // Insert before this stageKey (insert-stage only)
    public string? After { get; init; }       // Insert after this stageKey (insert-stage only)
}

/// <summary>Explicit insertion-point hint for agent-proposed changes.</summary>
public record PatchPlacement
{
    public string? InsertAfterStageKey { get; init; }
    public string? InsertBeforeStageKey { get; init; }
    public string? HandoffId { get; init; }
    public string? TransitionId { get; init; }
}

/// <summary>Cached validation status from a prior <c>workflow.validate</c> call.</summary>
public record PatchValidationResult
{
    public required string Status { get; init; }   // pass | fail | not-run
    public DateTimeOffset? CheckedAt { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Canonical proposal envelope — the atomic unit of all agent-initiated workflow changes.
/// Defined in <c>.squad/decisions.md</c> under "Workflow editor V1 agentic surfaces".
/// </summary>
public record ProposalEnvelope
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required PatchAgent Agent { get; init; }
    public required string TargetWorkflowId { get; init; }
    public required string Rationale { get; init; }
    public IReadOnlyList<PatchOp> Ops { get; init; } = [];
    public PatchPlacement? Placement { get; init; }
    public PatchValidationResult? ValidationResult { get; init; }
    public string? PreviewArtifactRef { get; init; }
}
