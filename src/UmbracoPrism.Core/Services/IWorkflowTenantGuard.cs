using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for validating tenant access to workflow instances.
/// </summary>
public interface IWorkflowTenantGuard
{
    /// <summary>
    /// Validates that a workflow instance belongs to the specified tenant and returns it.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The workflow instance if access is granted.</returns>
    /// <exception cref="Exceptions.UnauthorizedWorkflowAccessException">Thrown when the instance does not belong to the tenant.</exception>
    /// <exception cref="Exceptions.WorkflowInstanceNotFoundException">Thrown when the instance is not found.</exception>
    Task<WorkflowInstance> RequireInstanceAsync(string tenantId, string userId, string instanceId, CancellationToken cancellationToken = default);
}
