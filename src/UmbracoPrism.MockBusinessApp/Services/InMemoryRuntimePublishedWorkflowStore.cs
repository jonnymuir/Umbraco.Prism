using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Demo-focused published workflow store that keeps editor publishes in memory and
/// updates the live runtime engine without mutating the seed files on disk.
/// </summary>
public sealed class InMemoryRuntimePublishedWorkflowStore(BusinessAppWorkflowEngine engine) : IWorkflowSourceStore
{
    private readonly Dictionary<string, WorkflowDefinitionFile> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _saveLock = new();

    public Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        if (_overrides.TryGetValue(definitionKey, out var overridden))
            return Task.FromResult<WorkflowDefinitionFile?>(overridden);

        return Task.FromResult(engine.GetDefinition(definitionKey));
    }

    public Task<WorkflowSaveResult> SaveAsync(WorkflowDefinitionFile workflow, int expectedVersion, CancellationToken ct = default)
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
                return Task.FromResult(new WorkflowSaveResult(
                    Saved: false,
                    CurrentVersion: currentVersion,
                    Location: $"memory://published-workflows/{workflow.DefinitionKey}"));
            }

            var newVersion = expectedVersion + 1;
            var toSave = workflow with { Version = newVersion };

            _overrides[workflow.DefinitionKey] = toSave;
            engine.UpdateDefinition(workflow.DefinitionKey, toSave);

            return Task.FromResult(new WorkflowSaveResult(
                Saved: true,
                CurrentVersion: newVersion,
                Location: $"memory://published-workflows/{workflow.DefinitionKey}"));
        }
    }

    public Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        var byDefinitionKey = engine.GetAllDefinitions()
            .ToDictionary(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase);

        foreach (var (definitionKey, workflow) in _overrides)
        {
            byDefinitionKey[definitionKey] = workflow;
        }

        var summaries = byDefinitionKey.Values
            .OrderBy(workflow => workflow.DefinitionKey, StringComparer.Ordinal)
            .Select(workflow => new WorkflowSourceSummary(workflow.DefinitionKey, workflow.DisplayName))
            .ToArray();

        return Task.FromResult<IReadOnlyList<WorkflowSourceSummary>>(summaries);
    }
}
