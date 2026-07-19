using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Services.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Backoffice-hosted authoring surface for CMS Workflow definitions — list/read/validate/save/
/// simulate, backed by the same transport-agnostic <see cref="WorkflowAuthoringService"/> the
/// AI-authoring toolkit (REST/MCP) and the visual editor's own HTTP source both use elsewhere.
/// A controller, not a mapped minimal-API group, because Core is a package (Razor Class
/// Library) with no access to the host application's <c>IEndpointRouteBuilder</c> — Umbraco
/// discovers MVC controllers via assembly scanning instead, exactly like
/// <see cref="TenantManagementController"/>, whose auth pattern this mirrors.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[Authorize(Policy = "PrismAdmins")]
[VersionedApiBackOfficeRoute("prism")]
[ApiExplorerSettings(GroupName = "Prism")]
[MapToApi("Prism")]
public class CmsWorkflowAuthoringController(WorkflowAuthoringService authoringService) : ManagementApiControllerBase
{
    /// <summary>
    /// The single well-known queue every CMS Workflow definition runs on — the editor's
    /// queue-picker UI is driven entirely by this list, so returning exactly one entry is what
    /// keeps authoring locked to single-queue without any editor-side "CMS mode" flag.
    /// </summary>
    [HttpGet("cms-workflows/queues")]
    public IActionResult GetQueues() =>
        Ok(new[] { new { queueName = CmsWorkflowQueue.Key, displayName = CmsWorkflowQueue.DisplayName } });

    [HttpGet("cms-workflows")]
    public async Task<IActionResult> ListWorkflows(CancellationToken ct) =>
        Ok(await authoringService.ListAsync(ct));

    [HttpGet("cms-workflows/{definitionKey}")]
    public async Task<IActionResult> ReadWorkflow(string definitionKey, CancellationToken ct)
    {
        var workflow = await authoringService.ReadAsync(definitionKey, ct);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpGet("cms-workflows/{definitionKey}/version")]
    public async Task<IActionResult> GetWorkflowVersion(string definitionKey, CancellationToken ct)
    {
        var workflow = await authoringService.ReadAsync(definitionKey, ct);
        return workflow is null ? NotFound() : Ok(new { version = workflow.Version });
    }

    [HttpPost("cms-workflows/validate")]
    public IActionResult ValidateWorkflow([FromBody] WorkflowDefinitionFile workflow) =>
        Ok(authoringService.Validate(workflow));

    /// <summary>
    /// The body's own <c>version</c> (already round-tripped by any client that loaded the
    /// workflow first) IS the expected version for the optimistic-concurrency check — see
    /// <see cref="WorkflowAuthoringService.SaveAsync"/>.
    /// </summary>
    [HttpPut("cms-workflows/{definitionKey}")]
    public async Task<IActionResult> SaveWorkflow(
        string definitionKey, [FromBody] WorkflowDefinitionFile workflow, CancellationToken ct)
    {
        if (!string.Equals(workflow.DefinitionKey, definitionKey, StringComparison.Ordinal))
        {
            return BadRequest(new WorkflowValidationOutcome(
                false,
                [new WorkflowDiagnostic(
                    "ROUTE_KEY_MISMATCH",
                    "definitionKey",
                    $"Route key '{definitionKey}' does not match body definitionKey '{workflow.DefinitionKey}'.")]));
        }

        var outcome = await authoringService.SaveAsync(workflow, workflow.Version, ct);
        return outcome.Status switch
        {
            WorkflowSaveStatus.Saved => Ok(outcome),
            WorkflowSaveStatus.Conflict => Conflict(outcome),
            _ => BadRequest(outcome)
        };
    }

    [HttpPost("cms-workflows/simulate")]
    public IActionResult SimulateWorkflow([FromBody] CmsWorkflowSimulationRequest request) =>
        Ok(authoringService.Simulate(request.Workflow, request.Steps));

    [HttpDelete("cms-workflows/{definitionKey}")]
    public async Task<IActionResult> DeleteWorkflow(string definitionKey, CancellationToken ct)
    {
        var deleted = await authoringService.DeleteAsync(definitionKey, ct);
        return deleted ? Ok() : NotFound();
    }
}

/// <summary>Request body for <see cref="CmsWorkflowAuthoringController.SimulateWorkflow"/>.</summary>
public sealed record CmsWorkflowSimulationRequest(
    WorkflowDefinitionFile Workflow,
    IReadOnlyList<WorkflowRuntimeSimulationStep> Steps);
