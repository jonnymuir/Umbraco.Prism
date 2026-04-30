using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Lightweight API endpoint for polling workflow state changes.
/// Used by the waiting step type to detect when external processing completes,
/// without requiring a full page reload on every check.
/// </summary>
[ApiController]
[Route("api/prism/workflow")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowPollController : ControllerBase
{
    private readonly IBusinessAppWorkflowClient _workflowClient;

    /// <summary>
    /// Initialises a new instance of <see cref="WorkflowPollController"/>.
    /// </summary>
    public WorkflowPollController(IBusinessAppWorkflowClient workflowClient)
    {
        _workflowClient = workflowClient;
    }

    /// <summary>
    /// Polls for workflow state changes without a full page render.
    /// Returns whether the state version has changed since the client last checked.
    /// </summary>
    /// <param name="workflowKey">The workflow definition key.</param>
    /// <param name="instanceId">The workflow instance ID to check.</param>
    /// <param name="knownStateVersion">The state version the client currently knows about.</param>
    /// <returns>
    /// A JSON object with <c>changed</c> (bool), <c>newStateVersion</c> (int), and <c>stepType</c> (string).
    /// </returns>
    [HttpGet("poll")]
    public async Task<IActionResult> Poll(
        [FromQuery] string workflowKey,
        [FromQuery] string instanceId,
        [FromQuery] int knownStateVersion)
    {
        if (string.IsNullOrWhiteSpace(workflowKey) || string.IsNullOrWhiteSpace(instanceId))
            return BadRequest(new { error = "workflowKey and instanceId are required" });

        var envelope = await _workflowClient.GetCurrentAsync(workflowKey, instanceId, action: null);

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
