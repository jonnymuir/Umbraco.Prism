using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Models;

namespace UmbracoPrism.WorkflowRuntime.Abstractions;

public interface IWorkflowRuntimeEngine
{
    WorkflowResponseEnvelope GetCurrent(
        string workflowKey,
        string tenantId,
        string userId,
        string? instanceId = null,
        string? action = null);

    WorkflowResponseEnvelope GetCurrent(
        string workflowKey,
        string tenantId,
        string userId,
        WorkflowAccessProfile accessProfile,
        string? instanceId = null,
        string? action = null);

    WorkflowResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues);

    WorkflowResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        WorkflowAccessProfile accessProfile,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues);

    WorkflowInstanceListEnvelope GetInstances(string tenantId, string userId);

    WorkflowQueueWorkListEnvelope GetQueueWorkItems(WorkflowAccessProfile accessProfile);

    IEnumerable<WorkflowInstanceState> GetAllInstances();

    IEnumerable<WorkflowDefinitionFile> GetAllDefinitions();

    WorkflowDefinitionFile? GetDefinition(string key);

    /// <summary>Registers or updates a definition — an upsert. Always returns true.</summary>
    bool UpdateDefinition(string key, WorkflowDefinitionFile updated);

    /// <summary>Removes a definition. Returns false if it wasn't registered.</summary>
    bool RemoveDefinition(string key);

    bool Reset(string instanceId);

    void ResetAll();
}
