using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.WorkflowRuntime.Mcp;

/// <summary>
/// MCP tool wrappers over <see cref="WorkflowAuthoringService"/>. Pure protocol adapter —
/// no business logic lives here. Workflow/step payloads are passed as JSON strings
/// (the documented-safe SDK pattern); simple identifiers stay typed/primitive.
/// </summary>
[McpServerToolType]
public static class WorkflowAuthoringTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [McpServerTool(Name = "list_workflows")]
    [Description("List every workflow definition available in the connected host's store, with key and display name.")]
    public static async Task<string> ListWorkflows(WorkflowAuthoringService service)
    {
        var summaries = await service.ListAsync();
        return JsonSerializer.Serialize(summaries, JsonOptions);
    }

    [McpServerTool(Name = "read_workflow")]
    [Description("Read a workflow definition's full JSON by its definitionKey.")]
    public static async Task<string> ReadWorkflow(
        WorkflowAuthoringService service,
        [Description("The workflow's definitionKey, as returned by list_workflows.")] string definitionKey)
    {
        var workflow = await service.ReadAsync(definitionKey);
        return workflow is null ? "null" : JsonSerializer.Serialize(workflow, JsonOptions);
    }

    [McpServerTool(Name = "validate_workflow")]
    [Description(
        "Validate a workflow definition JSON — checks that every state route targets a gateway " +
        "(never another state directly) and that any calculations block evaluates cleanly. " +
        "Does not save. Returns { isValid, errors }.")]
    public static string ValidateWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to validate.")] string workflowJson)
    {
        var workflow = Deserialize(workflowJson);
        var outcome = service.Validate(workflow);
        return JsonSerializer.Serialize(outcome, JsonOptions);
    }

    [McpServerTool(Name = "save_workflow")]
    [Description(
        "Validate and save a workflow definition JSON to the host's store. Invalid definitions " +
        "are rejected and NOT saved — check { isValid, errors } in the response.")]
    public static async Task<string> SaveWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to save.")] string workflowJson)
    {
        var workflow = Deserialize(workflowJson);
        var outcome = await service.SaveAsync(workflow);
        return JsonSerializer.Serialize(outcome, JsonOptions);
    }

    [McpServerTool(Name = "simulate_workflow")]
    [Description(
        "Dry-run a sequence of actions through a workflow definition with no persistence — " +
        "returns the resulting state trace (one entry per step: response state, available " +
        "actions, problems) exactly as the real runtime would report to a client. Use this to " +
        "check a definition actually behaves as intended before saving it.")]
    public static string SimulateWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to simulate.")] string workflowJson,
        [Description(
            "JSON array of steps to advance through in order, e.g. " +
            "[{\"action\":\"submit\",\"fieldValues\":{\"name\":\"Ada\"}}].")]
        string stepsJson)
    {
        var workflow = Deserialize(workflowJson);
        var steps = JsonSerializer.Deserialize<List<WorkflowRuntimeSimulationStep>>(stepsJson, JsonOptions)
            ?? [];

        var trace = service.Simulate(workflow, steps);
        return JsonSerializer.Serialize(trace, JsonOptions);
    }

    private static WorkflowDefinitionFile Deserialize(string workflowJson) =>
        JsonSerializer.Deserialize<WorkflowDefinitionFile>(workflowJson, JsonOptions)
            ?? throw new InvalidOperationException("workflowJson did not deserialize to a WorkflowDefinitionFile.");
}
