using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.MockBusinessApp.Services;

namespace UmbracoPrism.MockBusinessApp.Controllers;

/// <summary>
/// Primary workflow API for the Mock Business App.
/// Called by the Umbraco TestSite (server-to-server) to ask:
///   "For this user/tenant, what is the next step in workflow {key}?"
/// 
/// API key authentication via <c>X-Prism-Api-Key</c> header (optional if not configured).
/// </summary>
[ApiController]
[Route("api/workflow")]
[AllowAnonymous]
public class WorkflowApiController(
    BusinessAppWorkflowEngine engine,
    IConfiguration configuration,
    ILogger<WorkflowApiController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the current workflow state for a given user and tenant, creating a new instance if none exists.
    /// </summary>
    /// <param name="workflowKey">The workflow definition key.</param>
    /// <param name="request">Request body containing TenantId and UserId.</param>
    /// <returns>
    /// 200 OK with a WorkflowResponseEnvelope on success.
    /// 401 Unauthorized if the API key is invalid or missing (when required).
    /// 422 Unprocessable Entity if the workflow is not found or another validation error occurs.
    /// </returns>
    [HttpPost("{workflowKey}/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult GetCurrent(string workflowKey, [FromBody] WorkflowCurrentRequest request)
    {
        if (!IsApiKeyValid())
        {
            logger.LogWarning("Workflow API: invalid or missing API key from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid or missing API key." });
        }

        logger.LogInformation("Workflow current: key={Key} tenant={Tenant} user={User}", workflowKey, request.TenantId, request.UserId);

        var envelope = engine.GetCurrent(workflowKey, request.TenantId, request.UserId);
        return envelope.ResponseState == "error" ? UnprocessableEntity(envelope) : Ok(envelope);
    }

    /// <summary>
    /// Submits field data and advances the workflow instance to the next state.
    /// Returns the new state for the client to render.
    /// </summary>
    /// <param name="workflowKey">The workflow definition key.</param>
    /// <param name="request">Request body containing instance ID, tenant ID, user ID, action, state version, and field values.</param>
    /// <returns>
    /// 200 OK with a WorkflowResponseEnvelope on success.
    /// 401 Unauthorized if the API key is invalid or missing (when required).
    /// 422 Unprocessable Entity if the transition is invalid, state version mismatches, or access is denied.
    /// </returns>
    [HttpPost("{workflowKey}/advance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Advance(string workflowKey, [FromBody] WorkflowAdvanceApiRequest request)
    {
        if (!IsApiKeyValid())
        {
            logger.LogWarning("Workflow API: invalid or missing API key from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid or missing API key." });
        }

        logger.LogInformation(
            "Workflow advance: key={Key} instance={Instance} action={Action}",
            workflowKey, request.InstanceId, request.Action);

        var envelope = engine.Advance(
            request.InstanceId, request.TenantId, request.UserId,
            request.Action, request.StateVersion, request.FieldValues);

        return envelope.ResponseState == "error" ? UnprocessableEntity(envelope) : Ok(envelope);
    }

    /// <summary>Validates the API key from the <c>X-Prism-Api-Key</c> header.</summary>
    /// <returns>True if the provided API key matches the configured key via constant-time comparison; otherwise false.</returns>
    /// <remarks>
    /// The API key is required (no default allow). Uses constant-time comparison to prevent timing attacks.
    /// </remarks>
    private bool IsApiKeyValid()
    {
        var configuredKey = configuration["PrismBusinessApp:WorkflowApiKey"];
        
        // SECURITY: Require API key to be configured - no default allow
        if (string.IsNullOrEmpty(configuredKey))
        {
            logger.LogError("SECURITY: PrismBusinessApp:WorkflowApiKey is not configured. Rejecting all requests.");
            return false;
        }
        
        if (!Request.Headers.TryGetValue("X-Prism-Api-Key", out var providedKey))
        {
            return false;
        }
        
        // SECURITY: Use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredKey),
            Encoding.UTF8.GetBytes(providedKey.ToString()));
    }
}

/// <summary>Request body for <c>POST /api/workflow/{key}/current</c>.</summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="UserId">The user identifier.</param>
public record WorkflowCurrentRequest(string TenantId, string UserId);

/// <summary>Request body for <c>POST /api/workflow/{key}/advance</c>.</summary>
/// <param name="InstanceId">The workflow instance ID to advance.</param>
/// <param name="TenantId">The tenant identifier (for access control).</param>
/// <param name="UserId">The user identifier (for access control).</param>
/// <param name="Action">The action to perform (e.g. "submit", "save-draft").</param>
/// <param name="StateVersion">The expected current state version (for optimistic concurrency control).</param>
/// <param name="FieldValues">Field values collected from the user's form submission.</param>
public record WorkflowAdvanceApiRequest(
    string InstanceId,
    string TenantId,
    string UserId,
    string Action,
    int StateVersion,
    Dictionary<string, object?>? FieldValues);
