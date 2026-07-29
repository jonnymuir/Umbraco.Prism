using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayfinder.Umbraco.Services;

namespace Wayfinder.Umbraco.Controllers;

/// <summary>
/// Lightweight API endpoint for polling workflow state changes.
/// Used by the waiting step type to detect when external processing completes,
/// without requiring a full page reload on every check.
/// </summary>
[ApiController]
[Route("api/prism/workflow")]
[Authorize(Policy = WayfinderUmbracoAuthorizationPolicies.ServiceRequestPolling)]
public class ServiceRequestPollController : ControllerBase
{
    private readonly IBusinessAppProcessManagerClient _processManagerClient;

    /// <summary>
    /// Initialises a new instance of <see cref="ServiceRequestPollController"/>.
    /// </summary>
    public ServiceRequestPollController(IBusinessAppProcessManagerClient workflowClient)
    {
        _processManagerClient = workflowClient;
    }

    /// <summary>
    /// Polls for workflow state changes without a full page render.
    /// Returns whether the state version has changed since the client last checked.
    /// </summary>
    /// <param name="blueprintKey">The workflow definition key.</param>
    /// <param name="instanceId">The workflow instance ID to check.</param>
    /// <param name="knownStateVersion">The state version the client currently knows about.</param>
    /// <returns>
    /// A JSON object with <c>changed</c> (bool), <c>newStateVersion</c> (int), and <c>stepType</c> (string).
    /// </returns>
    [HttpGet("poll")]
    public async Task<IActionResult> Poll(
        [FromQuery] string blueprintKey,
        [FromQuery] string instanceId,
        [FromQuery] int knownStateVersion)
    {
        if (string.IsNullOrWhiteSpace(blueprintKey) || string.IsNullOrWhiteSpace(instanceId))
            return BadRequest(new { error = "blueprintKey and instanceId are required" });

        var envelope = await _processManagerClient.GetCurrentAsync(blueprintKey, instanceId, action: null);

        if (envelope.ResponseState == "error")
            return NotFound(new { error = "Instance not found or workflow unavailable" });

        var changed = envelope.StateVersion != knownStateVersion;

        return Ok(new
        {
            changed,
            newStateVersion = envelope.StateVersion,
            stepType = envelope.Render?.StepType ?? string.Empty
        });
    }
}
