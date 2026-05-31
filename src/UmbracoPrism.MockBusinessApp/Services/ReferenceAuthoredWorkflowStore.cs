using System.Collections.Concurrent;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Server-side singleton in-memory store of authored workflows for the
/// MockBusinessApp's <c>/mockapp/workflows/*</c> endpoints. Slice B retired
/// the platform-side <c>IAuthoredWorkflowStore</c>; each host now owns its
/// own persistence and exposes whatever transport its TS
/// <c>WorkflowSource</c> consumes.
/// </summary>
public sealed class ReferenceAuthoredWorkflowStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public ReferenceAuthoredWorkflowStore()
    {
        foreach (var kv in ReferenceWorkflowRepository.GetReferenceWorkflows())
        {
            _entries[kv.Key] = new Entry(kv.Key, kv.Value);
        }
    }

    public IReadOnlyList<WorkflowSummary> List() =>
        _entries.Values
            .OrderBy(e => e.WorkflowKey, StringComparer.Ordinal)
            .Select(e => new WorkflowSummary(
                e.WorkflowKey,
                e.Workflow.DefinitionKey,
                e.Workflow.DisplayName))
            .ToArray();

    public AuthoredWorkflow? Load(string workflowKey) =>
        _entries.TryGetValue(workflowKey, out var entry) ? entry.Workflow : null;

    public void Save(string workflowKey, AuthoredWorkflow workflow) =>
        _entries[workflowKey] = new Entry(workflowKey, workflow);

    public IReadOnlyDictionary<string, AuthoredWorkflow> Snapshot() =>
        _entries.ToDictionary(kv => kv.Key, kv => kv.Value.Workflow, StringComparer.OrdinalIgnoreCase);

    private sealed record Entry(string WorkflowKey, AuthoredWorkflow Workflow);

    public sealed record WorkflowSummary(string WorkflowKey, string DefinitionKey, string DisplayName);
}
