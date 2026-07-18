using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Calculations;
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

    private const string CalculationsShapeReminder =
        "A `calculations` block is a sibling of `states`/`gateways`/`queues`, not an action or a " +
        "component property: { \"tables\": {}, \"fields\": { \"<fieldName>\": { \"expr\": \"<expression>\" } }, " +
        "\"series\": {} } — `fields` is a JSON OBJECT keyed by field name, each value an object with an " +
        "`expr` string (NOT a list of {name, expression} pairs). Only `stat-group`, `chart`, and " +
        "`summary-list` components render a calculated value on screen (binding by field/series name) — " +
        "`text`/`number`/etc. are INPUT components and never display one, however they're labelled, and a " +
        "`stat-group` with an empty `items` list renders nothing. Every input component's own `fieldKey` " +
        "is already automatically in the calculation scope — never redeclare it as a calculations.fields " +
        "entry with `\"source\": \"service\"`; that marker is only for a value an external system supplies " +
        "(e.g. a lookup), and doing this to your own captured input makes it permanently unresolvable. A " +
        "gateway needs a real, unique `key` (a keyless one can never be a route target), and every route's " +
        "`target` must actually match an existing gateway/state key — an empty target is fine mid-edit but " +
        "must be wired up before you consider the workflow finished. " +
        "Read workflow-docs://calculation-language before writing or editing a calculations block; it " +
        "has the full grammar and a worked example.";

    [McpServerTool(Name = "list_queue_capabilities")]
    [Description(
        "List every workflow queue this host has explicitly declared render capabilities for, " +
        "and which PrismComponent \"type\" discriminators (e.g. \"text\", \"summary-list\", " +
        "\"panel\") are supported for each. A queue key NOT present in this result is " +
        "unrestricted from this toolkit's point of view — not a declared concern of this host " +
        "(e.g. served by a different downstream app). Check this before drafting a state for a " +
        "queue you haven't authored for before, rather than finding out from validate_workflow's " +
        "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT diagnostic after the fact.")]
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ListQueueCapabilities(
        WorkflowAuthoringService service) =>
        service.GetQueueCapabilities();

    [McpServerTool(Name = "validate_workflow")]
    [Description(
        "Validate a workflow definition JSON — checks that every state route targets a gateway " +
        "(never another state directly), that any calculations block evaluates cleanly, and that " +
        "every stat-group/chart/summary-list component's bound field or series actually exists. " +
        "Does not save. Returns { isValid, diagnostics }, each diagnostic { code, path, message, severity } " +
        "— severity \"Warning\" (e.g. an unverifiable service-sourced field) does not block isValid. " +
        "When the host declares queue render capabilities, also checks that every component in a " +
        "state is actually supported by that state's queue (QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT " +
        "— call list_queue_capabilities first to check what a queue supports). " +
        CalculationsShapeReminder + " " +
        "See also workflow-docs://authoring-guide for the full contract shape.")]
    public static WorkflowValidationOutcome ValidateWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to validate.")] string workflowJson)
    {
        if (!TryDeserialize(workflowJson, out var workflow, out var diagnostic))
        {
            return new WorkflowValidationOutcome(false, [diagnostic!]);
        }
        return service.Validate(workflow);
    }

    [McpServerTool(Name = "save_workflow")]
    [Description(
        "Validate and save a workflow definition JSON to the connected host's live store — the " +
        "change is visible to the running app immediately, no restart. Invalid definitions " +
        "are rejected and NOT saved — check { status, errors } in the response (status: " +
        "\"Saved\", \"Invalid\", or \"Conflict\"). A Conflict means the workflow's `version` " +
        "field is stale — someone else (a human in the editor, or another agent) saved a newer " +
        "version; re-read_workflow to get the current version and reapply your change on top of " +
        "it before saving again. For a brand-new definitionKey that has never been saved, set " +
        "`version` to 0, not 1 — a non-existent workflow's current version is 0, and copying " +
        "`\"version\": 1` from an existing seed you read as a style reference (that's its " +
        "*current* saved version, not a starting value) will Conflict on your very first save. " +
        "This is the only way to persist a workflow change the running " +
        "app will honor; editing seed/source files directly (e.g. workflow-seeds/*.json) has no " +
        "effect on the live app. " + CalculationsShapeReminder + " " +
        "See also workflow-docs://authoring-guide for the full contract shape.")]
    public static Task<WorkflowSaveOutcome> SaveWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to save, including the `version` you read it at.")] string workflowJson,
        CancellationToken ct)
    {
        if (!TryDeserialize(workflowJson, out var workflow, out var diagnostic))
        {
            return Task.FromResult(WorkflowSaveOutcome.Invalid([diagnostic!]));
        }
        return service.SaveAsync(workflow, workflow.Version, ct);
    }

    [McpServerTool(Name = "simulate_workflow")]
    [Description(
        "Dry-run a sequence of actions through a workflow definition with no persistence — " +
        "returns { trace, calculations }: trace is the state trace (one entry per step: " +
        "response state, available actions, problems) exactly as the real runtime would " +
        "report to a client; calculations is the raw calculated field/series values per step " +
        "(parallel to trace, null for steps with no calculations block), so you can check the " +
        "maths directly instead of parsing rendered UI text. A definition with a " +
        "source: \"service\" calculation field (e.g. money-modeller's \"member\") needs " +
        "mockServiceInputsJson supplying it, or those fields — and anything calculated from " +
        "them — are simply unresolved, the same as against a host with no data for them. Use " +
        "this to check a definition actually behaves as intended before saving it. The trace " +
        "follows a single cursor: if a Split's business-side branch routes to both a Join and " +
        "its own separate terminal state, only one branch is followed and the other's actions " +
        "go unverified — route a reviewer/business action only into the Join, matching " +
        "payment-demo/information-request's convention, rather than giving it a parallel terminal.")]
    public static WorkflowSimulationResult SimulateWorkflow(
        WorkflowAuthoringService service,
        [Description("The full WorkflowDefinitionFile JSON to simulate.")] string workflowJson,
        [Description(
            "JSON array of steps to advance through in order, e.g. " +
            "[{\"action\":\"submit\",\"fieldValues\":{\"name\":\"Ada\"}}].")]
        string stepsJson,
        [Description(
            "Optional JSON object of mock values for source: \"service\" calculation fields, e.g. " +
            "{\"member\":{\"age\":47,\"active\":true}}. Omit if the definition has none.")]
        string? mockServiceInputsJson = null)
    {
        if (!TryDeserialize(workflowJson, out var workflow, out var diagnostic))
        {
            throw new InvalidOperationException(diagnostic!.Message);
        }
        var steps = JsonSerializer.Deserialize<List<WorkflowRuntimeSimulationStep>>(stepsJson, WorkflowJsonOptions)
            ?? [];
        var mockServiceInputs = string.IsNullOrWhiteSpace(mockServiceInputsJson)
            ? null
            : CalculationScopeJson.ToScopeValues(mockServiceInputsJson);
        return service.Simulate(workflow, steps, mockServiceInputs);
    }

    // System.Text.Json throws on any malformed workflowJson (wrong types, truncated JSON,
    // etc.) — without this guard, that exception bubbles out of the MCP tool call unhandled
    // instead of the structured { isValid/status, diagnostics } shape these tools document,
    // which the MCP SDK then surfaces as an opaque tool-call error rather than something an
    // agent can act on.
    private static bool TryDeserialize(
        string workflowJson, out WorkflowDefinitionFile workflow, out WorkflowDiagnostic? diagnostic)
    {
        try
        {
            workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(workflowJson, WorkflowJsonOptions)
                ?? throw new JsonException("workflowJson did not deserialize to a WorkflowDefinitionFile.");
            diagnostic = null;
            return true;
        }
        catch (JsonException ex)
        {
            workflow = null!;
            diagnostic = new WorkflowDiagnostic(
                "INVALID_JSON", ex.Path ?? "$", $"workflowJson is not a valid WorkflowDefinitionFile: {ex.Message}");
            return false;
        }
    }
}
