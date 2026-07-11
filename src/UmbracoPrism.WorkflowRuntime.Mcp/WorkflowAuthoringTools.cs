using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.WorkflowRuntime.Mcp;

/// <summary>
/// MCP tool wrappers over <see cref="WorkflowAuthoringService"/>. Calls it directly,
/// in-process — this runs inside the same app as the live engine, so a save is visible
/// immediately, no restart.
/// </summary>
[McpServerToolType]
public static class WorkflowAuthoringTools
{
    // WorkflowDefinitionFile.States[].Components is a [JsonPolymorphic] PrismComponent
    // hierarchy. The MCP SDK's own tool-argument binding doesn't set
    // AllowOutOfOrderMetadataProperties (github.com/modelcontextprotocol/csharp-sdk#795 —
    // no supported hook to configure it), so it fails whenever a component's "type"
    // discriminator isn't the first JSON property, which a model-constructed payload can't
    // be relied on to guarantee. Tools that take a workflow as input accept it as a JSON
    // string and deserialize it themselves with the same options FilesystemWorkflowSourceStore
    // uses. Tools that only return a WorkflowDefinitionFile (read_workflow) are unaffected —
    // serialization already writes the discriminator first — so those stay typed.
    private static readonly JsonSerializerOptions WorkflowJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    [McpServerTool(Name = "list_workflows")]
    [Description("List every workflow definition available in the connected host's store, with key and display name.")]
    public static Task<IReadOnlyList<WorkflowSourceSummary>> ListWorkflows(
        WorkflowAuthoringService service, CancellationToken ct) =>
        service.ListAsync(ct);

    [McpServerTool(Name = "read_workflow")]
    [Description("Read a workflow definition's full JSON by its definitionKey.")]
    public static Task<WorkflowDefinitionFile?> ReadWorkflow(
        WorkflowAuthoringService service,
        [Description("The workflow's definitionKey, as returned by list_workflows.")] string definitionKey,
        CancellationToken ct) =>
        service.ReadAsync(definitionKey, ct);

    [McpServerTool(Name = "validate_workflow")]
    [Description(
        "Validate a workflow definition JSON — checks that every state route targets a gateway " +
        "(never another state directly) and that any calculations block evaluates cleanly. " +
        "Does not save. Returns { isValid, errors }.")]
    public static WorkflowValidationOutcome ValidateWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to validate.")] string workflowJson) =>
        service.Validate(Deserialize(workflowJson));

    [McpServerTool(Name = "save_workflow")]
    [Description(
        "Validate and save a workflow definition JSON to the connected host's live store — the " +
        "change is visible to the running app immediately, no restart. Invalid definitions " +
        "are rejected and NOT saved — check { status, errors } in the response (status: " +
        "\"Saved\", \"Invalid\", or \"Conflict\"). A Conflict means the workflow's `version` " +
        "field is stale — someone else (a human in the editor, or another agent) saved a newer " +
        "version; re-read_workflow to get the current version and reapply your change on top of " +
        "it before saving again. This is the only way to persist a workflow change the running " +
        "app will honor; editing seed/source files directly (e.g. workflow-seeds/*.json) has no " +
        "effect on the live app.")]
    public static Task<WorkflowSaveOutcome> SaveWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to save, including the `version` you read it at.")] string workflowJson,
        CancellationToken ct)
    {
        var workflow = Deserialize(workflowJson);
        return service.SaveAsync(workflow, workflow.Version, ct);
    }

    [McpServerTool(Name = "simulate_workflow")]
    [Description(
        "Dry-run a sequence of actions through a workflow definition with no persistence — " +
        "returns the resulting state trace (one entry per step: response state, available " +
        "actions, problems) exactly as the real runtime would report to a client. Use this to " +
        "check a definition actually behaves as intended before saving it.")]
    public static IReadOnlyList<WorkflowResponseEnvelope> SimulateWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to simulate.")] string workflowJson,
        [Description(
            "JSON array of steps to advance through in order, e.g. " +
            "[{\"action\":\"submit\",\"fieldValues\":{\"name\":\"Ada\"}}].")]
        string stepsJson)
    {
        var steps = JsonSerializer.Deserialize<List<WorkflowRuntimeSimulationStep>>(stepsJson, WorkflowJsonOptions)
            ?? [];
        return service.Simulate(Deserialize(workflowJson), steps);
    }

    private static WorkflowDefinitionFile Deserialize(string workflowJson) =>
        JsonSerializer.Deserialize<WorkflowDefinitionFile>(workflowJson, WorkflowJsonOptions)
            ?? throw new InvalidOperationException("workflowJson did not deserialize to a WorkflowDefinitionFile.");
}
