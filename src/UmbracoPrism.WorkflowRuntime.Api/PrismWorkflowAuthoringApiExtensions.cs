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

            var outcome = await service.SaveAsync(workflow, ct);
            return outcome.IsValid ? Results.Ok(outcome) : Results.BadRequest(outcome);
        });

        group.MapPost("/workflows/simulate", (SimulateWorkflowRequest request, WorkflowAuthoringService service) =>
            Results.Ok(service.Simulate(request.Workflow, request.Steps)));

        return group;
    }
}
