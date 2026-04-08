using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Exceptions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for validating tenant access to workflow instances.
/// </summary>
public class WorkflowTenantGuard(
    IUmbracoDatabaseFactory databaseFactory,
    ILogger<WorkflowTenantGuard> logger) : IWorkflowTenantGuard
{
    /// <inheritdoc/>
    public Task<WorkflowInstance> RequireInstanceAsync(string tenantId, string userId, string instanceId, CancellationToken cancellationToken = default)
    {
        using var db = databaseFactory.CreateDatabase();

        var schema = db.SingleOrDefault<PrismWorkflowInstanceSchema>(
            "WHERE InstanceId = @0", instanceId);

        if (schema == null)
        {
            logger.LogWarning("Workflow instance {InstanceId} not found", instanceId);
            throw new WorkflowInstanceNotFoundException(instanceId);
        }

        if (schema.TenantId != tenantId)
        {
            logger.LogWarning(
                "Unauthorized access attempt to workflow instance {InstanceId} by tenant {TenantId}",
                instanceId, tenantId);
            throw new UnauthorizedWorkflowAccessException(instanceId);
        }

        var instance = new WorkflowInstance
        {
            InstanceId = schema.InstanceId,
            TenantId = schema.TenantId,
            DefinitionId = 0,
            DefinitionKey = schema.WorkflowKey,
            CurrentState = schema.CurrentStateKey,
            StateVersion = schema.StateVersion,
            Status = schema.Status,
            CorrelationId = null,
            InitiatedByUserId = schema.UserId,
            StateJson = schema.MetadataJson,
            CreatedAt = schema.CreatedAt,
            UpdatedAt = schema.UpdatedAt
        };

        return Task.FromResult(instance);
    }
}
