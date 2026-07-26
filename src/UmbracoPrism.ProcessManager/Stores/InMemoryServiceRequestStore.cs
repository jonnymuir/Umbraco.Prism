using System.Collections.Concurrent;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Models;

namespace UmbracoPrism.ProcessManager.Stores;

/// <summary>
/// Default <see cref="IServiceRequestStore"/> — process-lifetime only, exactly matching
/// <see cref="Services.ProcessManagerEngine"/>'s original hardcoded behaviour before the
/// store became a pluggable seam. Used whenever a host doesn't supply its own.
/// </summary>
public sealed class InMemoryServiceRequestStore : IServiceRequestStore
{
    private readonly ConcurrentDictionary<string, ServiceRequest> _instancesById = new();

    public bool TryGet(string instanceId, out ServiceRequest instance) =>
        _instancesById.TryGetValue(instanceId, out instance!);

    public void Save(ServiceRequest instance) =>
        _instancesById[instance.InstanceId] = instance;

    public bool Remove(string instanceId) =>
        _instancesById.TryRemove(instanceId, out _);

    public void Clear() => _instancesById.Clear();

    public IEnumerable<ServiceRequest> GetAll() => _instancesById.Values;
}
