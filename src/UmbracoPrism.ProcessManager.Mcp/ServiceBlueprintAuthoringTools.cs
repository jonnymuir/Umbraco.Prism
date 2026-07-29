using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Services.Calculations;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.ProcessManager.Mcp;

/// <summary>
/// MCP tool wrappers over <see cref="ServiceBlueprintAuthoringService"/>. Calls it directly,
/// in-process — this runs inside the same app as the live engine, so a save is visible
/// immediately, no restart.
/// </summary>
[McpServerToolType]
public static class ServiceBlueprintAuthoringTools
{
    // ServiceBlueprint.Stages[].Components is a [JsonPolymorphic] PrismComponent
    // hierarchy. The MCP SDK's own tool-argument binding doesn't set
    // AllowOutOfOrderMetadataProperties (github.com/modelcontextprotocol/csharp-sdk#795 —
    // no supported hook to configure it), so it fails whenever a component's "type"
    // discriminator isn't the first JSON property, which a model-constructed payload can't
    // be relied on to guarantee. Tools that take a blueprint as input accept it as a JSON
    // string and deserialize it themselves with the same options FilesystemServiceBlueprintSourceStore
    // uses. Tools that only return a ServiceBlueprint (read_service_blueprint) are unaffected —
    // serialization already writes the discriminator first — so those stay typed.
    private static readonly JsonSerializerOptions ServiceBlueprintJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    [McpServerTool(Name = "list_service_blueprints")]
    [Description("List every service blueprint available in the connected host's store, with key and display name.")]
    public static Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListServiceBlueprints(
        ServiceBlueprintAuthoringService service, CancellationToken ct) =>
        service.ListAsync(ct);

    [McpServerTool(Name = "read_service_blueprint")]
    [Description("Read a service blueprint's full JSON by its definitionKey.")]
    public static Task<ServiceBlueprint?> ReadServiceBlueprint(
        ServiceBlueprintAuthoringService service,
        [Description("The blueprint's definitionKey, as returned by list_service_blueprints.")] string definitionKey,
        CancellationToken ct) =>
        service.ReadAsync(definitionKey, ct);

    private const string CalculationsShapeReminder =
        "A `calculations` block is a sibling of `stages`/`gateways`/`queues`, not an action or a " +
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
        "`target` must actually match an existing gateway/stage key — an empty target is fine mid-edit but " +
        "must be wired up before you consider the blueprint finished. " +
        "Read service-blueprint-docs://calculation-language before writing or editing a calculations block; it " +
        "has the full grammar and a worked example.";

    [McpServerTool(Name = "list_queue_capabilities")]
    [Description(
        "List every queue this host has explicitly declared render capabilities for, " +
        "and which PrismComponent \"type\" discriminators (e.g. \"text\", \"summary-list\", " +
        "\"panel\") are supported for each. A queue key NOT present in this result is " +
        "unrestricted from this toolkit's point of view — not a declared concern of this host " +
        "(e.g. served by a different downstream app). Check this before drafting a stage for a " +
        "queue you haven't authored for before, rather than finding out from validate_service_blueprint's " +
        "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT diagnostic after the fact.")]
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ListQueueCapabilities(
        ServiceBlueprintAuthoringService service) =>
        service.GetQueueCapabilities();

    [McpServerTool(Name = "validate_service_blueprint")]
    [Description(
        "Validate a service blueprint JSON — checks that every stage route targets a gateway " +
        "(never another stage directly), that any calculations block evaluates cleanly, and that " +
        "every stat-group/chart/summary-list component's bound field or series actually exists. " +
        "Does not save. Returns { isValid, diagnostics }, each diagnostic { code, path, message, severity } " +
        "— severity \"Warning\" (e.g. an unverifiable service-sourced field) does not block isValid. " +
        "When the host declares queue render capabilities, also checks that every component in a " +
        "stage is actually supported by that stage's queue (QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT " +
        "— call list_queue_capabilities first to check what a queue supports). " +
        CalculationsShapeReminder + " " +
        "See also service-blueprint-docs://authoring-guide for the full contract shape.")]
    public static ServiceBlueprintValidationOutcome ValidateServiceBlueprint(
        ServiceBlueprintAuthoringService service,
        [Description("The full ServiceBlueprint JSON to validate.")] string blueprintJson)
    {
        if (!TryDeserialize(blueprintJson, out var blueprint, out var diagnostic))
        {
            return new ServiceBlueprintValidationOutcome(false, [diagnostic!]);
        }
        return service.Validate(blueprint);
    }

    [McpServerTool(Name = "save_service_blueprint")]
    [Description(
        "Validate and save a service blueprint JSON to the connected host's live store — the " +
        "change is visible to the running app immediately, no restart. Invalid definitions " +
        "are rejected and NOT saved — check { status, errors } in the response (status: " +
        "\"Saved\", \"Invalid\", or \"Conflict\"). A Conflict means the blueprint's `version` " +
        "field is stale — someone else (a human in the editor, or another agent) saved a newer " +
        "version; re-read_service_blueprint to get the current version and reapply your change on top of " +
        "it before saving again. For a brand-new definitionKey that has never been saved, set " +
        "`version` to 0, not 1 — a non-existent blueprint's current version is 0, and copying " +
        "`\"version\": 1` from an existing seed you read as a style reference (that's its " +
        "*current* saved version, not a starting value) will Conflict on your very first save. " +
        "This is the only way to persist a blueprint change the running " +
        "app will honor; editing seed/source files directly (e.g. workflow-seeds/*.json) has no " +
        "effect on the live app. " + CalculationsShapeReminder + " " +
        "See also service-blueprint-docs://authoring-guide for the full contract shape.")]
    public static Task<ServiceBlueprintSaveOutcome> SaveServiceBlueprint(
        ServiceBlueprintAuthoringService service,
        [Description("The full ServiceBlueprint JSON to save, including the `version` you read it at.")] string blueprintJson,
        CancellationToken ct)
    {
        if (!TryDeserialize(blueprintJson, out var blueprint, out var diagnostic))
        {
            return Task.FromResult(ServiceBlueprintSaveOutcome.Invalid([diagnostic!]));
        }
        return service.SaveAsync(blueprint, blueprint.Version, ct);
    }

    [McpServerTool(Name = "simulate_service_blueprint")]
    [Description(
        "Dry-run a sequence of actions through a service blueprint with no persistence — " +
        "returns { trace, calculations }: trace is the stage trace (one entry per step: " +
        "response stage, available actions, problems) exactly as the real runtime would " +
        "report to a client; calculations is the raw calculated field/series values per step " +
        "(parallel to trace, null for steps with no calculations block), so you can check the " +
        "maths directly instead of parsing rendered UI text. A definition with a " +
        "source: \"service\" calculation field (e.g. money-modeller's \"member\") needs " +
        "mockServiceInputsJson supplying it, or those fields — and anything calculated from " +
        "them — are simply unresolved, the same as against a host with no data for them. Use " +
        "this to check a definition actually behaves as intended before saving it. The trace " +
        "follows a single cursor: if a Split's business-side branch routes to both a Join and " +
        "its own separate terminal stage, only one branch is followed and the other's actions " +
        "go unverified — route a reviewer/business action only into the Join, matching " +
        "payment-demo/information-request's convention, rather than giving it a parallel terminal.")]
    public static ServiceBlueprintSimulationResult SimulateServiceBlueprint(
        ServiceBlueprintAuthoringService service,
        [Description("The full ServiceBlueprint JSON to simulate.")] string blueprintJson,
        [Description(
            "JSON array of steps to advance through in order, e.g. " +
            "[{\"action\":\"submit\",\"fieldValues\":{\"name\":\"Ada\"}}].")]
        string stepsJson,
        [Description(
            "Optional JSON object of mock values for source: \"service\" calculation fields, e.g. " +
            "{\"member\":{\"age\":47,\"active\":true}}. Omit if the definition has none.")]
        string? mockServiceInputsJson = null)
    {
        if (!TryDeserialize(blueprintJson, out var blueprint, out var diagnostic))
        {
            throw new InvalidOperationException(diagnostic!.Message);
        }
        var steps = JsonSerializer.Deserialize<List<ProcessManagerSimulationStep>>(stepsJson, ServiceBlueprintJsonOptions)
            ?? [];
        var mockServiceInputs = string.IsNullOrWhiteSpace(mockServiceInputsJson)
            ? null
            : CalculationScopeJson.ToScopeValues(mockServiceInputsJson);
        return service.Simulate(blueprint, steps, mockServiceInputs);
    }

    // System.Text.Json throws on any malformed blueprintJson (wrong types, truncated JSON,
    // etc.) — without this guard, that exception bubbles out of the MCP tool call unhandled
    // instead of the structured { isValid/status, diagnostics } shape these tools document,
    // which the MCP SDK then surfaces as an opaque tool-call error rather than something an
    // agent can act on.
    private static bool TryDeserialize(
        string blueprintJson, out ServiceBlueprint blueprint, out ServiceBlueprintDiagnostic? diagnostic)
    {
        try
        {
            blueprint = JsonSerializer.Deserialize<ServiceBlueprint>(blueprintJson, ServiceBlueprintJsonOptions)
                ?? throw new JsonException("blueprintJson did not deserialize to a ServiceBlueprint.");
            diagnostic = null;
            return true;
        }
        catch (JsonException ex)
        {
            blueprint = null!;
            diagnostic = new ServiceBlueprintDiagnostic(
                "INVALID_JSON", ex.Path ?? "$", $"blueprintJson is not a valid ServiceBlueprint: {ex.Message}");
            return false;
        }
    }
}
