using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Exceptions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for workflow instance operations.
/// </summary>
[Route("umbraco/prism/workflow")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowController(
    IWorkflowInstanceService workflowInstanceService,
    IWorkflowDefinitionRepository definitionRepository,
    IPrismContext prismContext,
    ILogger<WorkflowController> logger) : Controller
{
    /// <summary>
    /// Creates a new workflow instance.
    /// </summary>
    [HttpPost("instances")]
    public async Task<IActionResult> CreateInstanceAsync([FromBody] CreateInstanceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            logger.LogWarning("Workflow create: no tenant context");
            return BadRequest(new { error = "No tenant context available." });
        }

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
        {
            logger.LogWarning("Workflow create: user OID not found");
            return Unauthorized(new { error = "User identity could not be determined." });
        }

        try
        {
            var envelope = await workflowInstanceService.CreateAsync(
                tenant.Id.ToString(), userOid, request.DefinitionKey, request.CorrelationId);

            return envelope.ResponseState switch
            {
                "ask_now" or "complete" => Ok(envelope),
                "wait" => StatusCode(202, envelope),
                "error" => StatusCode(422, envelope),
                _ => StatusCode(500, envelope)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create workflow instance for definition {DefinitionKey}", request.DefinitionKey);
            return StatusCode(500, new { error = "An error occurred while creating the workflow instance." });
        }
    }

    /// <summary>
    /// Gets the current state of a workflow instance.
    /// </summary>
    [HttpGet("instances/{instanceId}")]
    public async Task<IActionResult> GetInstanceAsync(string instanceId)
    {
        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            return BadRequest(new { error = "No tenant context available." });
        }

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
        {
            return Unauthorized(new { error = "User identity could not be determined." });
        }

        try
        {
            var envelope = await workflowInstanceService.GetCurrentStateAsync(
                tenant.Id.ToString(), userOid, instanceId);

            return envelope.ResponseState switch
            {
                "ask_now" or "complete" => Ok(envelope),
                "wait" => StatusCode(202, envelope),
                "error" => StatusCode(422, envelope),
                _ => StatusCode(500, envelope)
            };
        }
        catch (WorkflowInstanceNotFoundException)
        {
            return NotFound(new { error = $"Workflow instance {instanceId} not found." });
        }
        catch (UnauthorizedWorkflowAccessException)
        {
            return StatusCode(403, new { error = "Access denied to this workflow instance." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get workflow instance {InstanceId}", instanceId);
            return StatusCode(500, new { error = "An error occurred while retrieving the workflow instance." });
        }
    }

    /// <summary>
    /// Advances a workflow instance to the next state.
    /// </summary>
    [HttpPost("instances/{instanceId}/advance")]
    public async Task<IActionResult> AdvanceInstanceAsync(string instanceId, [FromBody] AdvanceInstanceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            return BadRequest(new { error = "No tenant context available." });
        }

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
        {
            return Unauthorized(new { error = "User identity could not be determined." });
        }

        try
        {
            var envelope = await workflowInstanceService.AdvanceAsync(
                tenant.Id.ToString(), userOid, instanceId, request.Action,
                request.ExpectedStateVersion, request.FieldValues);

            return envelope.ResponseState switch
            {
                "ask_now" or "complete" => Ok(envelope),
                "wait" => StatusCode(202, envelope),
                "error" => StatusCode(422, envelope),
                _ => StatusCode(500, envelope)
            };
        }
        catch (OptimisticConcurrencyException ex)
        {
            logger.LogWarning("Optimistic concurrency failure for instance {InstanceId}: expected {Expected}, actual {Actual}",
                instanceId, ex.ExpectedVersion, ex.ActualVersion);
            return Conflict(new
            {
                error = "The workflow instance has been modified by another request.",
                expectedVersion = ex.ExpectedVersion,
                actualVersion = ex.ActualVersion
            });
        }
        catch (WorkflowInstanceNotFoundException)
        {
            return NotFound(new { error = $"Workflow instance {instanceId} not found." });
        }
        catch (UnauthorizedWorkflowAccessException)
        {
            return StatusCode(403, new { error = "Access denied to this workflow instance." });
        }
        catch (InvalidWorkflowTransitionException ex)
        {
            return StatusCode(422, new
            {
                error = $"Invalid transition from state '{ex.FromState}' with action '{ex.Action}'.",
                problems = new[]
                {
                    new WorkflowProblem
                    {
                        FieldKey = string.Empty,
                        Message = ex.Message,
                        Code = "INVALID_TRANSITION"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to advance workflow instance {InstanceId}", instanceId);
            return StatusCode(500, new { error = "An error occurred while advancing the workflow instance." });
        }
    }

    /// <summary>
    /// Cancels a workflow instance.
    /// </summary>
    [HttpDelete("instances/{instanceId}")]
    public async Task<IActionResult> CancelInstanceAsync(string instanceId)
    {
        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            return BadRequest(new { error = "No tenant context available." });
        }

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
        {
            return Unauthorized(new { error = "User identity could not be determined." });
        }

        try
        {
            var envelope = await workflowInstanceService.CancelAsync(
                tenant.Id.ToString(), userOid, instanceId);

            return Ok(envelope);
        }
        catch (WorkflowInstanceNotFoundException)
        {
            return NotFound(new { error = $"Workflow instance {instanceId} not found." });
        }
        catch (UnauthorizedWorkflowAccessException)
        {
            return StatusCode(403, new { error = "Access denied to this workflow instance." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel workflow instance {InstanceId}", instanceId);
            return StatusCode(500, new { error = "An error occurred while cancelling the workflow instance." });
        }
    }

    /// <summary>
    /// Gets all published workflow definitions for the current tenant.
    /// </summary>
    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitionsAsync()
    {
        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            return BadRequest(new { error = "No tenant context available." });
        }

        try
        {
            var definitions = await definitionRepository.GetAllAsync(tenant.Id.ToString());
            return Ok(definitions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get workflow definitions");
            return StatusCode(500, new { error = "An error occurred while retrieving workflow definitions." });
        }
    }
}

/// <summary>
/// Request model for creating a workflow instance.
/// </summary>
public record CreateInstanceRequest
{
    /// <summary>
    /// Gets the workflow definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the optional correlation identifier.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Request model for advancing a workflow instance.
/// </summary>
public record AdvanceInstanceRequest
{
    /// <summary>
    /// Gets the action to perform.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets the expected state version for optimistic concurrency control.
    /// </summary>
    public required int ExpectedStateVersion { get; init; }

    /// <summary>
    /// Gets the optional field values submitted with the action.
    /// </summary>
    public Dictionary<string, object?>? FieldValues { get; init; }
}
