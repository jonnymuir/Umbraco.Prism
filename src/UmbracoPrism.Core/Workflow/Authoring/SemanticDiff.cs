using System.Text.Json.Serialization;

namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>Base type for all semantic diff entries produced by <see cref="IWorkflowPreviewService"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StageAdded),         "stageAdded")]
[JsonDerivedType(typeof(StageRemoved),        "stageRemoved")]
[JsonDerivedType(typeof(StageUpdated),        "stageUpdated")]
[JsonDerivedType(typeof(HandoffAdded),        "handoffAdded")]
[JsonDerivedType(typeof(HandoffRemoved),      "handoffRemoved")]
[JsonDerivedType(typeof(TransitionChanged),   "transitionChanged")]
public abstract record DiffEntry;

/// <summary>A stage present in the patched workflow but absent from the original.</summary>
public record StageAdded(string Key, string Title) : DiffEntry;

/// <summary>A stage present in the original but absent from the patched workflow.</summary>
public record StageRemoved(string Key) : DiffEntry;

/// <summary>A stage present in both workflows but with at least one changed property.</summary>
public record StageUpdated(string Key, IReadOnlyList<string> FieldChanges) : DiffEntry;

/// <summary>A handoff present in the patched workflow but absent from the original.</summary>
public record HandoffAdded(string Id, string Label) : DiffEntry;

/// <summary>A handoff present in the original but absent from the patched workflow.</summary>
public record HandoffRemoved(string Id) : DiffEntry;

/// <summary>A transition that appeared, disappeared, or changed between the two versions.</summary>
public record TransitionChanged(string FromStage, string ToStage, string Action) : DiffEntry;
