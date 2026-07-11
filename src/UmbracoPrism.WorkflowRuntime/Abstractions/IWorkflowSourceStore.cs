using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowRuntime.Abstractions;

/// <summary>
/// Persists authored/runtime workflow definitions, keyed by <see cref="WorkflowDefinitionFile.DefinitionKey"/>.
/// This is the toolkit's extension point for AI-driven and human authoring alike — host apps
/// implement it against their own persistence (filesystem, database, CMS, ...).
/// </summary>
public interface IWorkflowSourceStore
{
    Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default);

    Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default);

    /// <summary>
    /// Saves <paramref name="workflow"/> only if <paramref name="expectedVersion"/> still matches
    /// the currently-persisted version — the same optimistic-concurrency guarantee
    /// <see cref="IWorkflowRuntimeEngine.Advance"/> already gives running instances via
    /// <c>expectedStateVersion</c>, extended to definitions. Pass <c>0</c> to mean "I expect this
    /// workflow doesn't exist yet." The stored copy's <see cref="WorkflowDefinitionFile.Version"/>
    /// is authoritatively set to <paramref name="expectedVersion"/> + 1 by the store on success —
    /// whatever <c>Version</c> is on the incoming <paramref name="workflow"/> is ignored, so a
    /// caller can't set an arbitrary version number.
    /// <para>
    /// Implementations MUST perform the compare-and-write atomically. An in-memory store can use
    /// a <c>lock</c>; a database-backed store should use an atomic
    /// <c>UPDATE ... WHERE Version = @expectedVersion</c> (the <c>WHERE</c> clause IS the atomic
    /// compare — never read-then-compare-then-write as separate steps, which races).
    /// </para>
    /// </summary>
    Task<WorkflowSaveResult> SaveAsync(WorkflowDefinitionFile workflow, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Lightweight listing entry for discovering available workflows before loading one in full.</summary>
public sealed record WorkflowSourceSummary(string DefinitionKey, string DisplayName);

/// <summary>
/// Result of an <see cref="IWorkflowSourceStore.SaveAsync"/> call. <see cref="Saved"/> distinguishes
/// success from a version conflict; <see cref="CurrentVersion"/> is always the version now actually
/// persisted (the new version on success, the version that beat the caller's expectation on conflict)
/// and <see cref="Location"/> is an implementation-defined save location (a file path, a
/// <c>memory://</c> URI, ...).
/// </summary>
public sealed record WorkflowSaveResult(bool Saved, int CurrentVersion, string Location);
