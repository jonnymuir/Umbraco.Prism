using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.MockBackOffice.Filters;

namespace UmbracoPrism.MockBackOffice.Controllers;

/// <summary>
/// Workflow emulator controller — simulates backoffice reviewer actions for demo purposes.
/// Development-only. Never exposed in production.
/// </summary>
[ApiController]
[Route("emulator/workflow")]
[EmulatorOnly]
[Authorize(AuthenticationSchemes = "Bearer")]
public class WorkflowEmulatorController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, EmulatedTask> _tasks = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkflowEmulatorController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public WorkflowEmulatorController(
        IConfiguration configuration,
        ILogger<WorkflowEmulatorController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Lists active workflow instances tracked by the emulator.
    /// </summary>
    [HttpGet("instances")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ListInstances()
    {
        _logger.LogInformation("Emulator: Listing {Count} tracked workflow instances", _tasks.Count);
        
        var instances = _tasks.Values.Select(t => new
        {
            t.InstanceId,
            t.CurrentState,
            t.TenantId,
            t.CreatedAt,
            Status = "Active"
        });

        return Ok(instances);
    }

    /// <summary>
    /// Advances a workflow instance as a reviewer by calling the Core workflow API.
    /// Simulates a backoffice reviewer taking action (approve or request-changes).
    /// </summary>
    [HttpPost("instances/{instanceId}/advance-as-reviewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdvanceAsReviewer(
        string instanceId,
        [FromBody] AdvanceAsReviewerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return BadRequest(new { Error = "Instance ID is required" });
        }

        if (request?.Action == null)
        {
            return BadRequest(new { Error = "Action is required" });
        }

        _logger.LogInformation(
            "Emulator: Reviewer advancing instance {InstanceId} with action {Action}",
            instanceId,
            request.Action);

        // Track the instance in our emulator state
        _tasks.TryAdd(instanceId, new EmulatedTask(
            InstanceId: instanceId,
            CurrentState: "under-review",
            TenantId: "emulator-tenant",
            CreatedAt: DateTimeOffset.UtcNow));

        // Call the Core workflow API to actually advance the workflow
        var coreApiBaseUrl = _configuration["WorkflowEmulator:CoreApiBaseUrl"] ?? "https://localhost:5001";
        var client = _httpClientFactory.CreateClient();
        
        try
        {
            var advanceUrl = $"{coreApiBaseUrl}/umbraco/prism/workflow/instances/{instanceId}/advance";
            
            var advancePayload = new
            {
                action = request.Action,
                expectedStateVersion = 2, // After initial submission, version is 2
                actorRole = "reviewer"
            };

            _logger.LogDebug("Emulator: Calling Core API at {Url}", advanceUrl);

            var response = await client.PostAsJsonAsync(advanceUrl, advancePayload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Emulator: Successfully advanced instance {InstanceId}", instanceId);
                
                // Update our tracked state
                if (_tasks.TryGetValue(instanceId, out var task))
                {
                    var newState = request.Action == "approve" ? "complete" : "collecting-info";
                    _tasks.TryUpdate(instanceId, task with { CurrentState = newState }, task);
                }

                return Ok(new
                {
                    Message = $"Reviewer action '{request.Action}' applied successfully",
                    InstanceId = instanceId,
                    Action = request.Action,
                    CoreApiResponse = result
                });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Emulator: Core API returned {StatusCode}: {Error}",
                    response.StatusCode,
                    error);
                
                return StatusCode((int)response.StatusCode, new
                {
                    Error = "Core API call failed",
                    StatusCode = response.StatusCode,
                    Details = error
                });
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Emulator: HTTP error calling Core API for instance {InstanceId}", instanceId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Error = "Core workflow API unavailable",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Proxies to the Core workflow API to get available workflow definitions.
    /// </summary>
    [HttpGet("definitions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDefinitions(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Emulator: Proxying workflow definitions request to Core API");

        var coreApiBaseUrl = _configuration["WorkflowEmulator:CoreApiBaseUrl"] ?? "https://localhost:5001";
        var client = _httpClientFactory.CreateClient();

        try
        {
            var definitionsUrl = $"{coreApiBaseUrl}/umbraco/prism/workflow/definitions";
            var response = await client.GetAsync(definitionsUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                return Content(result, "application/json");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Emulator: Core API returned {StatusCode}: {Error}", response.StatusCode, error);
                return StatusCode((int)response.StatusCode, error);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Emulator: HTTP error calling Core API definitions endpoint");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Error = "Core workflow API unavailable",
                Details = ex.Message
            });
        }
    }
}

/// <summary>
/// Represents a workflow task tracked by the emulator.
/// </summary>
public record EmulatedTask(
    string InstanceId,
    string CurrentState,
    string TenantId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Request payload for advancing a workflow as a reviewer.
/// </summary>
public record AdvanceAsReviewerRequest
{
    /// <summary>
    /// The action to perform: "approve" or "request-changes"
    /// </summary>
    public string Action { get; init; } = "approve";
}
