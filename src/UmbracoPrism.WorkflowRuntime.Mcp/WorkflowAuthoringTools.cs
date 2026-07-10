using System.ComponentModel;
using ModelContextProtocol.Server;

namespace UmbracoPrism.WorkflowRuntime.Mcp;

/// <summary>
/// MCP tool wrappers over <see cref="WorkflowAuthoringApiClient"/>. Pure protocol adapter —
/// every call proxies to the target app's own HTTP API; no business logic lives here.
/// </summary>
[McpServerToolType]
public static class WorkflowAuthoringTools
{
    [McpServerTool(Name = "list_workflows")]
    [Description("List every workflow definition available in the connected host's store, with key and display name.")]
    public static Task<string> ListWorkflows(WorkflowAuthoringApiClient client, CancellationToken ct) =>
        client.ListWorkflowsAsync(ct);

    [McpServerTool(Name = "read_workflow")]
    [Description("Read a workflow definition's full JSON by its definitionKey.")]
    public static async Task<string> ReadWorkflow(
        WorkflowAuthoringApiClient client,
        [Description("The workflow's definitionKey, as returned by list_workflows.")] string definitionKey,
        CancellationToken ct)
    {
        var result = await client.ReadWorkflowAsync(definitionKey, ct);
        return result ?? "null";
    }

    [McpServerTool(Name = "validate_workflow")]
    [Description(
        "Validate a workflow definition JSON — checks that every state route targets a gateway " +
        "(never another state directly) and that any calculations block evaluates cleanly. " +
        "Does not save. Returns { isValid, errors }.")]
    public static Task<string> ValidateWorkflow(
        WorkflowAuthoringApiClient client,
        [Description("The full WorkflowDefinitionFile JSON to validate.")] string workflowJson,
        CancellationToken ct) =>
        client.ValidateWorkflowAsync(workflowJson, ct);

    [McpServerTool(Name = "save_workflow")]
    [Description(
        "Validate and save a workflow definition JSON to the connected host's live store — " +
        "the change is visible to the running app immediately, no restart. Invalid definitions " +
        "are rejected and NOT saved — check { isValid, errors } in the response.")]
    public static Task<string> SaveWorkflow(
        WorkflowAuthoringApiClient client,
        [Description("The full WorkflowDefinitionFile JSON to save.")] string workflowJson,
        CancellationToken ct) =>
        client.SaveWorkflowAsync(workflowJson, ct);

    [McpServerTool(Name = "simulate_workflow")]
    [Description(
        "Dry-run a sequence of actions through a workflow definition with no persistence — " +
        "returns the resulting state trace (one entry per step: response state, available " +
        "actions, problems) exactly as the real runtime would report to a client. Use this to " +
        "check a definition actually behaves as intended before saving it.")]
    public static Task<string> SimulateWorkflow(
        WorkflowAuthoringApiClient client,
        [Description("The full WorkflowDefinitionFile JSON to simulate.")] string workflowJson,
        [Description(
            "JSON array of steps to advance through in order, e.g. " +
            "[{\"action\":\"submit\",\"fieldValues\":{\"name\":\"Ada\"}}].")]
        string stepsJson,
        CancellationToken ct) =>
        client.SimulateWorkflowAsync(workflowJson, stepsJson, ct);
}
