using UmbracoPrism.ProcessManager.Models;

namespace UmbracoPrism.ProcessManager.Abstractions;

/// <summary>
/// The toolkit's extension point for blueprint *instance* storage — mirrors
/// <see cref="IServiceBlueprintSourceStore"/>'s role for definitions. <see cref="Services.ProcessManagerEngine"/>
/// ships a default in-memory implementation, but a host that needs instances to survive a
/// process restart, or to expire on a schedule unrelated to the process lifetime (e.g. tied to a
/// browser session), implements this against its own persistence instead.
/// </summary>
public interface IServiceRequestStore
{
    /// <summary>Looks up a single instance by id.</summary>
    bool TryGet(string instanceId, out ServiceRequest instance);

    /// <summary>Upserts an instance.</summary>
    void Save(ServiceRequest instance);

    /// <summary>Removes an instance, if present. Returns whether one was removed.</summary>
    bool Remove(string instanceId);

    /// <summary>Removes every stored instance.</summary>
    void Clear();

    /// <summary>Returns every stored instance. Order is not guaranteed.</summary>
    IEnumerable<ServiceRequest> GetAll();
}
