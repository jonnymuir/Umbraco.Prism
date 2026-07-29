using Wayfinder.Models.ServiceDesign;

namespace Wayfinder.Engine.Abstractions;

/// <summary>
/// Persists authored/runtime service blueprints, keyed by <see cref="ServiceBlueprint.DefinitionKey"/>.
/// This is the toolkit's extension point for AI-driven and human authoring alike — host apps
/// implement it against their own persistence (filesystem, database, CMS, ...).
/// </summary>
public interface IServiceBlueprintSourceStore
{
    Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default);

    Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default);

    /// <summary>
    /// Saves <paramref name="blueprint"/> only if <paramref name="expectedVersion"/> still matches
    /// the currently-persisted version — the same optimistic-concurrency guarantee
    /// <see cref="IProcessManager.Advance"/> already gives running instances via
    /// <c>expectedStateVersion</c>, extended to definitions. Pass <c>0</c> to mean "I expect this
    /// blueprint doesn't exist yet." The stored copy's <see cref="ServiceBlueprint.Version"/>
    /// is authoritatively set to <paramref name="expectedVersion"/> + 1 by the store on success —
    /// whatever <c>Version</c> is on the incoming <paramref name="blueprint"/> is ignored, so a
    /// caller can't set an arbitrary version number.
    /// <para>
    /// Implementations MUST perform the compare-and-write atomically. An in-memory store can use
    /// a <c>lock</c>; a database-backed store should use an atomic
    /// <c>UPDATE ... WHERE Version = @expectedVersion</c> (the <c>WHERE</c> clause IS the atomic
    /// compare — never read-then-compare-then-write as separate steps, which races).
    /// </para>
    /// </summary>
    Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint blueprint, int expectedVersion, CancellationToken ct = default);

    /// <summary>
    /// Removes <paramref name="definitionKey"/> permanently. Returns <c>false</c> if it didn't
    /// exist (idempotent — a caller retrying a timed-out delete shouldn't get an error for it).
    /// Implementations that keep a live runtime engine in sync with saves (see
    /// <see cref="SaveAsync"/>'s remarks) must remove the definition from that engine too, so an
    /// in-progress instance can't keep advancing against a definition its own authoring store no
    /// longer has.
    /// </summary>
    Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default);
}

/// <summary>Lightweight listing entry for discovering available blueprints before loading one in full.</summary>
public sealed record ServiceBlueprintSourceSummary(string DefinitionKey, string DisplayName);

/// <summary>
/// Result of an <see cref="IServiceBlueprintSourceStore.SaveAsync"/> call. <see cref="Saved"/> distinguishes
/// success from a version conflict; <see cref="CurrentVersion"/> is always the version now actually
/// persisted (the new version on success, the version that beat the caller's expectation on conflict)
/// and <see cref="Location"/> is an implementation-defined save location (a file path, a
/// <c>memory://</c> URI, ...).
/// </summary>
public sealed record ServiceBlueprintSaveResult(bool Saved, int CurrentVersion, string Location);
