using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for workflow instance operations and state machine orchestration.
/// </summary>
public interface IWorkflowInstanceService
{
    /// <summary>
    /// Creates a new workflow instance.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="definitionKey">The workflow definition key.</param>
    /// <param name="correlationId">Optional correlation identifier for tracking related instances.</param>
    /// <returns>A response envelope with the initial state.</returns>
    Task<WorkflowResponseEnvelope> CreateAsync(string tenantId, string userId, string definitionKey, string? correlationId = null);

    /// <summary>
    /// Gets the current state of a workflow instance.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="instanceId">The instance identifier.</param>
    /// <returns>A response envelope with the current state.</returns>
    Task<WorkflowResponseEnvelope> GetCurrentStateAsync(string tenantId, string userId, string instanceId);

    /// <summary>
    /// Advances a workflow instance to the next state.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="action">The action to perform.</param>
    /// <param name="expectedStateVersion">The expected state version for optimistic concurrency.</param>
    /// <param name="fieldValues">Optional field values submitted with the action.</param>
    /// <returns>A response envelope with the new state.</returns>
    Task<WorkflowResponseEnvelope> AdvanceAsync(string tenantId, string userId, string instanceId, string action, int expectedStateVersion, Dictionary<string, object?>? fieldValues = null);

    /// <summary>
    /// Cancels a workflow instance.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="instanceId">The instance identifier.</param>
    /// <returns>A response envelope indicating cancellation.</returns>
    Task<WorkflowResponseEnvelope> CancelAsync(string tenantId, string userId, string instanceId);

    /// <summary>
    /// Gets all active workflow instances for a user.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <returns>A read-only list of active workflow instances.</returns>
    Task<IReadOnlyList<WorkflowInstance>> GetActiveAsync(string tenantId, string userId);
}
