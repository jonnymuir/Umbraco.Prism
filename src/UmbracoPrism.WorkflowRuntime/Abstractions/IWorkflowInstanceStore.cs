using UmbracoPrism.WorkflowRuntime.Models;

namespace UmbracoPrism.WorkflowRuntime.Abstractions;

/// <summary>
/// The toolkit's extension point for workflow *instance* storage — mirrors
/// <see cref="IWorkflowSourceStore"/>'s role for definitions. <see cref="Services.WorkflowRuntimeEngine"/>
/// ships a default in-memory implementation, but a host that needs instances to survive a
/// process restart, or to expire on a schedule unrelated to the process lifetime (e.g. tied to a
/// browser session), implements this against its own persistence instead.
/// </summary>
public interface IWorkflowInstanceStore
{
    /// <summary>Looks up a single instance by id.</summary>
    bool TryGet(string instanceId, out WorkflowInstanceState instance);

    /// <summary>Upserts an instance.</summary>
    void Save(WorkflowInstanceState instance);

    /// <summary>Removes an instance, if present. Returns whether one was removed.</summary>
    bool Remove(string instanceId);

    /// <summary>Removes every stored instance.</summary>
    void Clear();

    /// <summary>Returns every stored instance. Order is not guaranteed.</summary>
    IEnumerable<WorkflowInstanceState> GetAll();
}
