using System.Collections.Concurrent;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;

namespace UmbracoPrism.WorkflowRuntime.Stores;

/// <summary>
/// Default <see cref="IWorkflowInstanceStore"/> — process-lifetime only, exactly matching
/// <see cref="Services.WorkflowRuntimeEngine"/>'s original hardcoded behaviour before the
/// store became a pluggable seam. Used whenever a host doesn't supply its own.
/// </summary>
public sealed class InMemoryWorkflowInstanceStore : IWorkflowInstanceStore
{
    private readonly ConcurrentDictionary<string, WorkflowInstanceState> _instancesById = new();

    public bool TryGet(string instanceId, out WorkflowInstanceState instance) =>
        _instancesById.TryGetValue(instanceId, out instance!);

    public void Save(WorkflowInstanceState instance) =>
        _instancesById[instance.InstanceId] = instance;

    public bool Remove(string instanceId) =>
        _instancesById.TryRemove(instanceId, out _);

    public void Clear() => _instancesById.Clear();

    public IEnumerable<WorkflowInstanceState> GetAll() => _instancesById.Values;
}
