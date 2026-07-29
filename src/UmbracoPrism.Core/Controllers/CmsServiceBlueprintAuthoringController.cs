using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Services.ServiceDesign;
using Wayfinder.Engine.Services;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Backoffice-hosted authoring surface for CMS Service Blueprint definitions — list/read/validate/save/
/// simulate, backed by the same transport-agnostic <see cref="ServiceBlueprintAuthoringService"/> the
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
public class CmsServiceBlueprintAuthoringController(ServiceBlueprintAuthoringService authoringService) : ManagementApiControllerBase
{
    /// <summary>
    /// The single well-known queue every CMS Service Blueprint definition runs on — the editor's
    /// queue-picker UI is driven entirely by this list, so returning exactly one entry is what
    /// keeps authoring locked to single-queue without any editor-side "CMS mode" flag.
    /// </summary>
    [HttpGet("cms-service-blueprints/queues")]
    public IActionResult GetQueues() =>
        Ok(new[] { new { queueName = CmsQueue.Key, displayName = CmsQueue.DisplayName } });

    [HttpGet("cms-service-blueprints")]
    public async Task<IActionResult> ListServiceBlueprints(CancellationToken ct) =>
        Ok(await authoringService.ListAsync(ct));

    [HttpGet("cms-service-blueprints/{definitionKey}")]
    public async Task<IActionResult> ReadServiceBlueprint(string definitionKey, CancellationToken ct)
    {
        var blueprint = await authoringService.ReadAsync(definitionKey, ct);
        return blueprint is null ? NotFound() : Ok(blueprint);
    }

    [HttpGet("cms-service-blueprints/{definitionKey}/version")]
    public async Task<IActionResult> GetServiceBlueprintVersion(string definitionKey, CancellationToken ct)
    {
        var blueprint = await authoringService.ReadAsync(definitionKey, ct);
        return blueprint is null ? NotFound() : Ok(new { version = blueprint.Version });
    }

    [HttpPost("cms-service-blueprints/validate")]
    public IActionResult ValidateServiceBlueprint([FromBody] ServiceBlueprint blueprint) =>
        Ok(authoringService.Validate(blueprint));

    /// <summary>
    /// The body's own <c>version</c> (already round-tripped by any client that loaded the
    /// blueprint first) IS the expected version for the optimistic-concurrency check — see
    /// <see cref="ServiceBlueprintAuthoringService.SaveAsync"/>.
    /// </summary>
    [HttpPut("cms-service-blueprints/{definitionKey}")]
    public async Task<IActionResult> SaveServiceBlueprint(
        string definitionKey, [FromBody] ServiceBlueprint blueprint, CancellationToken ct)
    {
        if (!string.Equals(blueprint.DefinitionKey, definitionKey, StringComparison.Ordinal))
        {
            return BadRequest(new ServiceBlueprintValidationOutcome(
                false,
                [new ServiceBlueprintDiagnostic(
                    "ROUTE_KEY_MISMATCH",
                    "definitionKey",
                    $"Route key '{definitionKey}' does not match body definitionKey '{blueprint.DefinitionKey}'.")]));
        }

        var outcome = await authoringService.SaveAsync(blueprint, blueprint.Version, ct);
        return outcome.Status switch
        {
            ServiceBlueprintSaveStatus.Saved => Ok(outcome),
            ServiceBlueprintSaveStatus.Conflict => Conflict(outcome),
            _ => BadRequest(outcome)
        };
    }

    [HttpPost("cms-service-blueprints/simulate")]
    public IActionResult SimulateServiceBlueprint([FromBody] CmsServiceBlueprintSimulationRequest request) =>
        Ok(authoringService.Simulate(request.Blueprint, request.Steps));

    [HttpDelete("cms-service-blueprints/{definitionKey}")]
    public async Task<IActionResult> DeleteServiceBlueprint(string definitionKey, CancellationToken ct)
    {
        var deleted = await authoringService.DeleteAsync(definitionKey, ct);
        return deleted ? Ok() : NotFound();
    }
}

/// <summary>Request body for <see cref="CmsServiceBlueprintAuthoringController.SimulateServiceBlueprint"/>.</summary>
public sealed record CmsServiceBlueprintSimulationRequest(
    ServiceBlueprint Blueprint,
    IReadOnlyList<ProcessManagerSimulationStep> Steps);
