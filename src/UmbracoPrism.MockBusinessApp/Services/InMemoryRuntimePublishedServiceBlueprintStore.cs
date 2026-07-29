using Wayfinder.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Demo-focused published workflow store that keeps editor publishes in memory and
/// updates the live runtime engine without mutating the seed files on disk.
/// </summary>
public sealed class InMemoryRuntimePublishedServiceBlueprintStore(BusinessAppProcessManager engine) : IServiceBlueprintSourceStore
{
    private readonly Dictionary<string, ServiceBlueprint> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _saveLock = new();

    public Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        if (_overrides.TryGetValue(definitionKey, out var overridden))
            return Task.FromResult<ServiceBlueprint?>(overridden);

        return Task.FromResult(engine.GetDefinition(definitionKey));
    }

    public Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint workflow, int expectedVersion, CancellationToken ct = default)
    {
        // Synchronous critical section (no I/O here — just dictionary + engine state), so a plain
        // lock is enough; see FilesystemWorkflowSourceStore for the async equivalent.
        lock (_saveLock)
        {
            var current = _overrides.TryGetValue(workflow.DefinitionKey, out var overridden)
                ? overridden
                : engine.GetDefinition(workflow.DefinitionKey);
            var currentVersion = current?.Version ?? 0;

            if (currentVersion != expectedVersion)
            {
                return Task.FromResult(new ServiceBlueprintSaveResult(
                    Saved: false,
                    CurrentVersion: currentVersion,
                    Location: $"memory://published-workflows/{workflow.DefinitionKey}"));
            }

            var newVersion = expectedVersion + 1;
            var toSave = workflow with { Version = newVersion };

            _overrides[workflow.DefinitionKey] = toSave;
            engine.UpdateDefinition(workflow.DefinitionKey, toSave);

            return Task.FromResult(new ServiceBlueprintSaveResult(
                Saved: true,
                CurrentVersion: newVersion,
                Location: $"memory://published-workflows/{workflow.DefinitionKey}"));
        }
    }

    public Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        var byDefinitionKey = engine.GetAllDefinitions()
            .ToDictionary(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase);

        foreach (var (definitionKey, workflow) in _overrides)
        {
            byDefinitionKey[definitionKey] = workflow;
        }

        var summaries = byDefinitionKey.Values
            .OrderBy(workflow => workflow.DefinitionKey, StringComparer.Ordinal)
            .Select(workflow => new ServiceBlueprintSourceSummary(workflow.DefinitionKey, workflow.DisplayName))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ServiceBlueprintSourceSummary>>(summaries);
    }

    public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default)
    {
        lock (_saveLock)
        {
            // Non-short-circuiting | — both removals must run regardless of whether the first
            // one found something, since a definition can exist in one without the other
            // (e.g. a seed-only workflow never overridden by an editor save).
            var existed = _overrides.Remove(definitionKey) | engine.RemoveDefinition(definitionKey);
            return Task.FromResult(existed);
        }
    }
}
