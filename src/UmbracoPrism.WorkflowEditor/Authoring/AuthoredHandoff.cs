namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A named insertion point between two stages, used by agents to propose structural changes.
/// Handoffs allow agents to locate a specific boundary ("insert before the reviewer handoff") without
/// needing to reason about stage ordering directly.
/// </summary>
public record AuthoredHandoff
{
    /// <summary>Stable identifier for this handoff (unique within the workflow).</summary>
    public required string Id { get; init; }

    /// <summary>The <see cref="AuthoredStage.StageKey"/> of the outgoing stage.</summary>
    public required string FromStage { get; init; }

    /// <summary>The <see cref="AuthoredStage.StageKey"/> of the incoming stage.</summary>
    public required string ToStage { get; init; }

    /// <summary>
    /// Human-readable label describing this handoff boundary (e.g. "applicant-to-reviewer").
    /// Agents reference this label when proposing "insert before/after" operations.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Optional actor change description — the role or person that takes over after this handoff.
    /// Informational only; not projected into the runtime.
    /// </summary>
    public string? ActorChange { get; init; }
}
