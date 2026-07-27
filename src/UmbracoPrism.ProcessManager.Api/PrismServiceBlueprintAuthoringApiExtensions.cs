using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Services.Calculations;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.ProcessManager.Api;

/// <summary>
/// Maps the Prism service blueprint authoring toolkit's HTTP surface — list/read/validate/save/simulate —
/// onto <see cref="ServiceBlueprintAuthoringService"/>. The returned <see cref="RouteGroupBuilder"/> lets
/// the host chain its own policy, e.g. <c>.RequireAuthorization()</c>; this extension applies none.
/// The host must have already registered <c>ServiceBlueprintAuthoringService</c> (see
/// <c>AddPrismServiceBlueprintAuthoring()</c>) and its own <c>IServiceBlueprintSourceStore</c>.
/// </summary>
public static class PrismServiceBlueprintAuthoringApiExtensions
{
    public static RouteGroupBuilder MapPrismServiceBlueprintAuthoringApi(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/prism/service-blueprint-authoring")
    {
        var group = endpoints.MapGroup(prefix);

        group.MapGet("/blueprints", async (ServiceBlueprintAuthoringService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        group.MapGet("/blueprints/{definitionKey}", async (
            string definitionKey,
            ServiceBlueprintAuthoringService service,
            CancellationToken ct) =>
        {
            var blueprint = await service.ReadAsync(definitionKey, ct);
            return blueprint is null ? Results.NotFound() : Results.Ok(blueprint);
        });

        group.MapPost("/blueprints/validate", (ServiceBlueprint blueprint, ServiceBlueprintAuthoringService service) =>
            Results.Ok(service.Validate(blueprint)));

        // The body's own `version` (already round-tripped by any client that loaded the blueprint
        // first) IS the expected version for the optimistic-concurrency check — no separate field
        // needed. The store ignores it as a *value to write*; it only compares against it, then
        // authoritatively sets the new persisted version itself.
        group.MapPut("/blueprints/{definitionKey}", async (
            string definitionKey,
            ServiceBlueprint blueprint,
            ServiceBlueprintAuthoringService service,
            CancellationToken ct) =>
        {
            if (!string.Equals(blueprint.DefinitionKey, definitionKey, StringComparison.Ordinal))
            {
                return Results.BadRequest(new ServiceBlueprintValidationOutcome(
                    false,
                    [new ServiceBlueprintDiagnostic(
                        "ROUTE_KEY_MISMATCH",
                        "definitionKey",
                        $"Route key '{definitionKey}' does not match body definitionKey '{blueprint.DefinitionKey}'.")]));
            }

            var outcome = await service.SaveAsync(blueprint, blueprint.Version, ct);
            return outcome.Status switch
            {
                ServiceBlueprintSaveStatus.Saved => Results.Ok(outcome),
                ServiceBlueprintSaveStatus.Conflict => Results.Conflict(outcome),
                _ => Results.BadRequest(outcome)
            };
        });

        group.MapPost("/blueprints/simulate", (SimulateServiceBlueprintRequest request, ServiceBlueprintAuthoringService service) =>
        {
            var mockServiceInputs = request.MockServiceInputs is { } element
                ? (IReadOnlyDictionary<string, object?>)CalculationScopeJson.ToScopeValue(element)!
                : null;
            return Results.Ok(service.Simulate(request.Blueprint, request.Steps, mockServiceInputs));
        });

        // Cheap enough to poll: a client that has a blueprint open (e.g. the visual editor) can
        // check every ~15s whether it's still current without re-fetching the full definition.
        // A high-scale host may want a dedicated version-only store lookup instead of loading
        // the whole definition just to read one field; not worth it for the reference store.
        group.MapGet("/blueprints/{definitionKey}/version", async (
            string definitionKey,
            ServiceBlueprintAuthoringService service,
            CancellationToken ct) =>
        {
            var blueprint = await service.ReadAsync(definitionKey, ct);
            return blueprint is null ? Results.NotFound() : Results.Ok(new { version = blueprint.Version });
        });

        return group;
    }
}
