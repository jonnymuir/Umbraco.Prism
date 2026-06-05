using System.Collections.Concurrent;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// In-memory source store for the flattened workflow definition contract served by /mockapp/workflows/*.
/// </summary>
public sealed class ReferenceWorkflowSourceStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public ReferenceWorkflowSourceStore()
    {
        foreach (var kv in ReferenceWorkflowRepository.GetReferenceWorkflows())
        {
            _entries[kv.Key] = new Entry(kv.Key, kv.Value);
        }
    }

    public IReadOnlyList<WorkflowSummary> List() =>
        _entries.Values
            .OrderBy(entry => entry.WorkflowKey, StringComparer.Ordinal)
            .Select(entry => new WorkflowSummary(
                entry.WorkflowKey,
                entry.Workflow.DefinitionKey,
                entry.Workflow.DisplayName))
            .ToArray();

    public WorkflowDefinitionFile? Load(string workflowKey) =>
        _entries.TryGetValue(workflowKey, out var entry) ? entry.Workflow : null;

    public void Save(string workflowKey, WorkflowDefinitionFile workflow) =>
        _entries[workflowKey] = new Entry(workflowKey, workflow);

    public IReadOnlyDictionary<string, WorkflowDefinitionFile> Snapshot() =>
        _entries.ToDictionary(kv => kv.Key, kv => kv.Value.Workflow, StringComparer.OrdinalIgnoreCase);

    private sealed record Entry(string WorkflowKey, WorkflowDefinitionFile Workflow);

    public sealed record WorkflowSummary(string WorkflowKey, string DefinitionKey, string DisplayName);
}
