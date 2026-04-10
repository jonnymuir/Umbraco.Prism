using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.MockBusinessApp.Services;

namespace UmbracoPrism.MockBusinessApp.Controllers;

/// <summary>
/// Primary workflow API for the Mock Business App.
/// Called by the Umbraco TestSite (server-to-server) to ask:
///   "For this member, what is the next step in workflow {key}?"
///
/// The caller forwards the member's Entra Bearer token; identity is derived from
/// JWT claims here — tenant and user are never trusted from the request body.
/// </summary>
[ApiController]
[Route("api/workflow")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class WorkflowApiController(
    BusinessAppWorkflowEngine engine,
    IConfiguration config,
    ILogger<WorkflowApiController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the current workflow state for the authenticated member, creating a new instance if none exists.
    /// </summary>
    /// <param name="workflowKey">The workflow definition key.</param>
    /// <returns>
    /// 200 OK with a WorkflowResponseEnvelope on success.
    /// 422 Unprocessable Entity if the workflow is not found or another validation error occurs.
    /// </returns>
    [HttpPost("{workflowKey}/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult GetCurrent(string workflowKey)
    {
        var (tenantId, userId) = ResolveIdentity();
        if (tenantId == null || userId == null)
            return Unauthorized();

        logger.LogInformation("Workflow current: key={Key} tenant={Tenant} user={User}", workflowKey, tenantId, userId);

        var envelope = engine.GetCurrent(workflowKey, tenantId, userId);
        return envelope.ResponseState == "error" ? UnprocessableEntity(envelope) : Ok(envelope);
    }

    /// <summary>
    /// Submits field data and advances the workflow instance to the next state.
    /// Returns the new state for the client to render.
    /// </summary>
    /// <param name="workflowKey">The workflow definition key.</param>
    /// <param name="request">Request body containing instance ID, action, state version, and field values.</param>
    /// <returns>
    /// 200 OK with a WorkflowResponseEnvelope on success.
    /// 422 Unprocessable Entity if the transition is invalid, state version mismatches, or access is denied.
    /// </returns>
    [HttpPost("{workflowKey}/advance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Advance(string workflowKey, [FromBody] WorkflowAdvanceApiRequest request)
    {
        var (tenantId, userId) = ResolveIdentity();
        if (tenantId == null || userId == null)
            return Unauthorized();

        logger.LogInformation(
            "Workflow advance: key={Key} instance={Instance} action={Action}",
            workflowKey, request.InstanceId, request.Action);

        var envelope = engine.Advance(
            request.InstanceId, tenantId, userId,
            request.Action, request.StateVersion, request.FieldValues);

        return envelope.ResponseState == "error" ? UnprocessableEntity(envelope) : Ok(envelope);
    }

    /// <summary>
    /// Resolves tenant and user identifiers from the authenticated Bearer token.
    /// Uses <c>tid</c> (Entra tenant GUID) mapped to the Prism tenant code,
    /// and <c>oid</c> (stable user object ID) as the user identifier.
    /// </summary>
    private (string? tenantId, string? userId) ResolveIdentity()
    {
        var tenant = User.GetPrismTenant(PrismResolvers.FromConfig(config));
        var userId = User.FindFirst("oid")?.Value ?? User.FindFirst("sub")?.Value;

        if (tenant == null)
        {
            logger.LogWarning("Workflow identity resolution failed: tenant not recognised (tid={Tid})", User.GetTenantId());
            return (null, null);
        }

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Workflow identity resolution failed: no oid/sub claim present");
            return (null, null);
        }

        return (tenant.Code, userId);
    }
}

/// <summary>Request body for <c>POST /api/workflow/{key}/advance</c>.</summary>
/// <param name="InstanceId">The workflow instance ID to advance.</param>
/// <param name="Action">The action to perform (e.g. "submit", "save-draft").</param>
/// <param name="StateVersion">The expected current state version (for optimistic concurrency control).</param>
/// <param name="FieldValues">Field values collected from the user's form submission.</param>
public record WorkflowAdvanceApiRequest(
    string InstanceId,
    string Action,
    int StateVersion,
    Dictionary<string, object?>? FieldValues);

