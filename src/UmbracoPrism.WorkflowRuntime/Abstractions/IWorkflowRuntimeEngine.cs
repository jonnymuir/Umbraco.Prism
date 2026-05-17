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

    WorkflowResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues);

    WorkflowInstanceListEnvelope GetInstances(string tenantId, string userId);

    IEnumerable<WorkflowInstanceState> GetAllInstances();

    IEnumerable<WorkflowDefinitionFile> GetAllDefinitions();

    WorkflowDefinitionFile? GetDefinition(string key);

    bool UpdateDefinition(string key, WorkflowDefinitionFile updated);

    bool Reset(string instanceId);

    void ResetAll();
}
