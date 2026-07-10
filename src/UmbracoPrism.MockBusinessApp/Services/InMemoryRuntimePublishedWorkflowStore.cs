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

    public Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        if (_overrides.TryGetValue(definitionKey, out var overridden))
            return Task.FromResult<WorkflowDefinitionFile?>(overridden);

        return Task.FromResult(engine.GetDefinition(definitionKey));
    }

    public Task<string> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default)
    {
        _overrides[workflow.DefinitionKey] = workflow;
        engine.UpdateDefinition(workflow.DefinitionKey, workflow);
        return Task.FromResult($"memory://published-workflows/{workflow.DefinitionKey}");
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
