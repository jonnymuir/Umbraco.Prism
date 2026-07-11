using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.WorkflowRuntime.Api;

/// <summary>
/// Maps the Prism workflow authoring toolkit's HTTP surface — list/read/validate/save/simulate —
/// onto <see cref="WorkflowAuthoringService"/>. The returned <see cref="RouteGroupBuilder"/> lets
/// the host chain its own policy, e.g. <c>.RequireAuthorization()</c>; this extension applies none.
/// The host must have already registered <c>WorkflowAuthoringService</c> (see
/// <c>AddPrismWorkflowAuthoring()</c>) and its own <c>IWorkflowSourceStore</c>.
/// </summary>
public static class PrismWorkflowAuthoringApiExtensions
{
    public static RouteGroupBuilder MapPrismWorkflowAuthoringApi(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/prism/workflow-authoring")
    {
        var group = endpoints.MapGroup(prefix);

        group.MapGet("/workflows", async (WorkflowAuthoringService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        group.MapGet("/workflows/{definitionKey}", async (
            string definitionKey,
            WorkflowAuthoringService service,
            CancellationToken ct) =>
        {
            var workflow = await service.ReadAsync(definitionKey, ct);
            return workflow is null ? Results.NotFound() : Results.Ok(workflow);
        });

        group.MapPost("/workflows/validate", (WorkflowDefinitionFile workflow, WorkflowAuthoringService service) =>
            Results.Ok(service.Validate(workflow)));

        // The body's own `version` (already round-tripped by any client that loaded the workflow
        // first) IS the expected version for the optimistic-concurrency check — no separate field
        // needed. The store ignores it as a *value to write*; it only compares against it, then
        // authoritatively sets the new persisted version itself.
        group.MapPut("/workflows/{definitionKey}", async (
            string definitionKey,
            WorkflowDefinitionFile workflow,
            WorkflowAuthoringService service,
            CancellationToken ct) =>
        {
            if (!string.Equals(workflow.DefinitionKey, definitionKey, StringComparison.Ordinal))
            {
                return Results.BadRequest(new WorkflowValidationOutcome(
                    false,
                    [$"Route key '{definitionKey}' does not match body definitionKey '{workflow.DefinitionKey}'."]));
            }

            var outcome = await service.SaveAsync(workflow, workflow.Version, ct);
            return outcome.Status switch
            {
                WorkflowSaveStatus.Saved => Results.Ok(outcome),
                WorkflowSaveStatus.Conflict => Results.Conflict(outcome),
                _ => Results.BadRequest(outcome)
            };
        });

        group.MapPost("/workflows/simulate", (SimulateWorkflowRequest request, WorkflowAuthoringService service) =>
            Results.Ok(service.Simulate(request.Workflow, request.Steps)));

        // Cheap enough to poll: a client that has a workflow open (e.g. the visual editor) can
        // check every ~15s whether it's still current without re-fetching the full definition.
        // A high-scale host may want a dedicated version-only store lookup instead of loading
        // the whole definition just to read one field; not worth it for the reference store.
        group.MapGet("/workflows/{definitionKey}/version", async (
            string definitionKey,
            WorkflowAuthoringService service,
            CancellationToken ct) =>
        {
            var workflow = await service.ReadAsync(definitionKey, ct);
            return workflow is null ? Results.NotFound() : Results.Ok(new { version = workflow.Version });
        });

        return group;
    }
}
