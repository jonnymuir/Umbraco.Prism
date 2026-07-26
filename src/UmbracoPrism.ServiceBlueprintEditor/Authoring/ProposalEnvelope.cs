using System.Text.Json;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// The agent or human actor that submitted this proposal. Optional on
/// <see cref="ProposalEnvelope"/>: when omitted the endpoint synthesises one
/// from the authenticated principal.
/// </summary>
public record PatchAgent
{
    /// <summary>
    /// Free-form actor identifier. Historical conventions are
    /// <c>github-copilot</c>, <c>custom-agent</c>, and <c>human-assisted</c>,
    /// but any non-blank label is accepted. The endpoint applies a
    /// cross-stamp check only when this value is <c>human-assisted</c>.
    /// </summary>
    public required string Kind { get; init; }
    public required string Identity { get; init; }
    public string? SessionRef { get; init; }
}

/// <summary>A single mutation to apply to the authored blueprint.</summary>
public record PatchOp
{
    public required string Op { get; init; }   // insert-touchpoint | remove-touchpoint | update-touchpoint | insert-handoff | update-transition
    public string? Path { get; init; }         // JSON Pointer into the authored model
    public JsonElement? Value { get; init; }  // The authored touchpoint / handoff / transition to insert or set
    public string? Before { get; init; }      // Insert before this touchpointKey (insert-touchpoint only)
    public string? After { get; init; }       // Insert after this touchpointKey (insert-touchpoint only)
}

/// <summary>Explicit insertion-point hint for agent-proposed changes.</summary>
public record PatchPlacement
{
    public string? InsertAfterTouchpointKey { get; init; }
    public string? InsertBeforeTouchpointKey { get; init; }
    public string? HandoffId { get; init; }
    public string? TransitionId { get; init; }
}

/// <summary>Cached validation status from a prior <c>blueprint.validate</c> call.</summary>
public record PatchValidationResult
{
    public required string Status { get; init; }   // pass | fail | not-run
    public DateTimeOffset? CheckedAt { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Canonical proposal envelope — the atomic unit of envelope-mediated blueprint
/// changes accepted by <c>POST /api/service-blueprint-authoring/blueprints/{key}/apply</c>.
/// <para>
/// <see cref="Id"/> and <see cref="CreatedAt"/> are required for provenance audit.
/// <see cref="Agent"/> and <see cref="Rationale"/> are optional — when omitted the
/// endpoint synthesises an agent from the authenticated principal. <see cref="Ops"/>
/// must contain at least one operation; an empty envelope is rejected with 400.
/// </para>
/// </summary>
public record ProposalEnvelope
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public PatchAgent? Agent { get; init; }
    public required string TargetServiceBlueprintId { get; init; }
    public string? Rationale { get; init; }
    public IReadOnlyList<PatchOp> Ops { get; init; } = [];
    public PatchPlacement? Placement { get; init; }
    public PatchValidationResult? ValidationResult { get; init; }
    public string? PreviewArtifactRef { get; init; }
}
