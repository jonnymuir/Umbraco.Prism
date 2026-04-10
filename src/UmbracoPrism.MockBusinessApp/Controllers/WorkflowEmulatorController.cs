using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.MockBusinessApp.Filters;
using UmbracoPrism.MockBusinessApp.Services;

namespace UmbracoPrism.MockBusinessApp.Controllers;

/// <summary>
/// Workflow emulator controller — simulates reviewer actions for demo purposes.
/// Calls the <see cref="BusinessAppWorkflowEngine"/> directly since the Business App
/// is now the authoritative source of workflow state.
/// Development-only. Never exposed in production.
/// </summary>
[ApiController]
[Route("emulator/workflow")]
[EmulatorOnly]
[Authorize(AuthenticationSchemes = "Bearer")]
public class WorkflowEmulatorController(
    BusinessAppWorkflowEngine engine,
    ILogger<WorkflowEmulatorController> logger) : ControllerBase
{
    /// <summary>
    /// Lists all active workflow instances held in the Business App engine.
    /// </summary>
    [HttpGet("instances")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ListInstances()
    {
        var instances = engine.GetAllInstances().Select(i => new
        {
            i.InstanceId,
            i.WorkflowKey,
            i.CurrentState,
            i.TenantId,
            i.UserId,
            i.StateVersion,
            i.CreatedAt,
            i.UpdatedAt
        });

        logger.LogInformation("Emulator: Listing workflow instances");
        return Ok(instances);
    }

    /// <summary>
    /// Advances a workflow instance as a reviewer (approve or request-changes).
    /// </summary>
    [HttpPost("instances/{instanceId}/advance-as-reviewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult AdvanceAsReviewer(string instanceId, [FromBody] AdvanceAsReviewerRequest request)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return BadRequest(new { error = "Instance ID is required." });

        if (string.IsNullOrWhiteSpace(request?.Action))
            return BadRequest(new { error = "Action is required." });

        logger.LogInformation("Emulator: Reviewer advancing instance {InstanceId} with action {Action}", instanceId, request.Action);

        var envelope = engine.AdvanceAsReviewer(instanceId, request.Action);

        return envelope.ResponseState == "error"
            ? UnprocessableEntity(envelope)
            : Ok(new
            {
                Message = $"Reviewer action '{request.Action}' applied successfully.",
                InstanceId = instanceId,
                Action = request.Action,
                NewState = envelope.Render?.StateDisplayName,
                ResponseState = envelope.ResponseState
            });
    }

    /// <summary>
    /// Returns the registered workflow definitions from the Business App engine.
    /// </summary>
    [HttpGet("definitions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetDefinitions()
    {
        logger.LogInformation("Emulator: Listing workflow definitions");
        var instances = engine.GetAllInstances();
        return Ok(new { Message = "Workflow definitions are managed by the Business App engine.", InstanceCount = instances.Count() });
    }
}

/// <summary>
/// Request payload for advancing a workflow as a reviewer.
/// </summary>
public record AdvanceAsReviewerRequest
{
    /// <summary>The reviewer action to perform: "approve" or "request-changes".</summary>
    public string Action { get; init; } = "approve";
}
