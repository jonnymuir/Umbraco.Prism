using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Exceptions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for workflow instance operations and state machine orchestration.
/// </summary>
public class WorkflowInstanceService(
    IUmbracoDatabaseFactory databaseFactory,
    IWorkflowDefinitionRepository definitionRepository,
    IWorkflowRenderService renderService,
    IWorkflowTenantGuard tenantGuard,
    ILogger<WorkflowInstanceService> logger) : IWorkflowInstanceService
{
    /// <inheritdoc/>
    public async Task<WorkflowResponseEnvelope> CreateAsync(
        string tenantId, string userId, string definitionKey, string? correlationId = null)
    {
        var definition = await definitionRepository.GetByKeyAsync(tenantId, definitionKey);
        if (definition == null)
        {
            logger.LogWarning("Workflow definition {DefinitionKey} not found for tenant {TenantId}",
                definitionKey, tenantId);

            return new WorkflowResponseEnvelope
            {
                InstanceId = string.Empty,
                ResponseState = "error",
                StateVersion = 0,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Problems = new[]
                {
                    new WorkflowProblem
                    {
                        FieldKey = string.Empty,
                        Message = $"Workflow definition '{definitionKey}' not found",
                        Code = "DEFINITION_NOT_FOUND"
                    }
                }
            };
        }

        var instanceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        using var db = databaseFactory.CreateDatabase();
        using var transaction = db.GetTransaction();

        var instanceSchema = new PrismWorkflowInstanceSchema
        {
            InstanceId = instanceId,
            WorkflowKey = definition.DefinitionKey,
            WorkflowVersion = definition.Version,
            TenantId = tenantId,
            UserId = userId,
            CurrentStateKey = definition.InitialState,
            StateVersion = 0,
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = null,
            OutcomeKey = null,
            MetadataJson = null
        };

        db.Insert(instanceSchema);

        var eventSchema = new PrismWorkflowEventSchema
        {
            EventId = Guid.NewGuid().ToString(),
            InstanceId = instanceId,
            TenantId = tenantId,
            EventType = "Created",
            ActorId = userId,
            StateFrom = null,
            StateTo = definition.InitialState,
            PayloadJson = "{}",
            TimestampUtc = now,
            CorrelationId = correlationId ?? instanceId
        };

        db.Insert(eventSchema);
        transaction.Complete();

        var instance = new WorkflowInstance
        {
            InstanceId = instanceId,
            TenantId = tenantId,
            DefinitionId = definition.Id,
            DefinitionKey = definition.DefinitionKey,
            CurrentState = definition.InitialState,
            StateVersion = 0,
            Status = "Active",
            CorrelationId = correlationId,
            InitiatedByUserId = userId,
            StateJson = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        var render = await renderService.RenderAsync(instance, definition);

        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = GetResponseState(instance, definition),
            StateVersion = 0,
            CorrelationId = correlationId ?? instanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Render = render
        };
    }

    /// <inheritdoc/>
    public async Task<WorkflowResponseEnvelope> GetCurrentStateAsync(
        string tenantId, string userId, string instanceId)
    {
        var instance = await tenantGuard.RequireInstanceAsync(tenantId, userId, instanceId);
        var definition = await definitionRepository.GetByKeyAsync(tenantId, instance.DefinitionKey);

        if (definition == null)
        {
            return new WorkflowResponseEnvelope
            {
                InstanceId = instanceId,
                ResponseState = "error",
                StateVersion = instance.StateVersion,
                CorrelationId = instance.CorrelationId ?? instanceId,
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Problems = new[]
                {
                    new WorkflowProblem
                    {
                        FieldKey = string.Empty,
                        Message = "Workflow definition not found",
                        Code = "DEFINITION_NOT_FOUND"
                    }
                }
            };
        }

        var render = await renderService.RenderAsync(instance, definition);

        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = GetResponseState(instance, definition),
            StateVersion = instance.StateVersion,
            CorrelationId = instance.CorrelationId ?? instanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Render = render
        };
    }

    /// <inheritdoc/>
    public async Task<WorkflowResponseEnvelope> AdvanceAsync(
        string tenantId, string userId, string instanceId, string action,
        int expectedStateVersion, Dictionary<string, object?>? fieldValues = null)
    {
        var instance = await tenantGuard.RequireInstanceAsync(tenantId, userId, instanceId);
        var definition = await definitionRepository.GetByKeyAsync(tenantId, instance.DefinitionKey);

        if (definition == null)
        {
            throw new InvalidOperationException($"Workflow definition '{instance.DefinitionKey}' not found");
        }

        var transition = definition.Transitions
            .FirstOrDefault(t => t.FromState == instance.CurrentState && t.Action == action);

        if (transition == null)
        {
            throw new InvalidWorkflowTransitionException(instance.CurrentState, action);
        }

        var now = DateTime.UtcNow;

        using var db = databaseFactory.CreateDatabase();
        using var transaction = db.GetTransaction();

        var rowsUpdated = db.Execute(
            @"UPDATE prismWorkflowInstances 
              SET CurrentStateKey = @0, StateVersion = StateVersion + 1, UpdatedAt = @1
              WHERE InstanceId = @2 AND TenantId = @3 AND StateVersion = @4",
            transition.ToState, now, instanceId, tenantId, expectedStateVersion);

        if (rowsUpdated == 0)
        {
            var currentSchema = db.SingleOrDefault<PrismWorkflowInstanceSchema>(
                "WHERE InstanceId = @0", instanceId);

            if (currentSchema != null)
            {
                throw new OptimisticConcurrencyException(instanceId, expectedStateVersion, currentSchema.StateVersion);
            }

            throw new WorkflowInstanceNotFoundException(instanceId);
        }

        var eventSchema = new PrismWorkflowEventSchema
        {
            EventId = Guid.NewGuid().ToString(),
            InstanceId = instanceId,
            TenantId = tenantId,
            EventType = "Advanced",
            ActorId = userId,
            StateFrom = instance.CurrentState,
            StateTo = transition.ToState,
            PayloadJson = fieldValues != null ? JsonSerializer.Serialize(fieldValues) : "{}",
            TimestampUtc = now,
            CorrelationId = instance.CorrelationId ?? instanceId
        };

        db.Insert(eventSchema);
        transaction.Complete();

        instance.CurrentState = transition.ToState;
        instance.StateVersion++;
        instance.UpdatedAt = now;

        var render = await renderService.RenderAsync(instance, definition);

        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = GetResponseState(instance, definition),
            StateVersion = instance.StateVersion,
            CorrelationId = instance.CorrelationId ?? instanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Render = render
        };
    }

    /// <inheritdoc/>
    public async Task<WorkflowResponseEnvelope> CancelAsync(string tenantId, string userId, string instanceId)
    {
        var instance = await tenantGuard.RequireInstanceAsync(tenantId, userId, instanceId);
        var now = DateTime.UtcNow;

        using var db = databaseFactory.CreateDatabase();
        using var transaction = db.GetTransaction();

        db.Execute(
            @"UPDATE prismWorkflowInstances 
              SET Status = 'Cancelled', UpdatedAt = @0, CompletedAt = @0
              WHERE InstanceId = @1 AND TenantId = @2",
            now, instanceId, tenantId);

        var eventSchema = new PrismWorkflowEventSchema
        {
            EventId = Guid.NewGuid().ToString(),
            InstanceId = instanceId,
            TenantId = tenantId,
            EventType = "Cancelled",
            ActorId = userId,
            StateFrom = instance.CurrentState,
            StateTo = null,
            PayloadJson = "{}",
            TimestampUtc = now,
            CorrelationId = instance.CorrelationId ?? instanceId
        };

        db.Insert(eventSchema);
        transaction.Complete();

        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "complete",
            StateVersion = instance.StateVersion,
            CorrelationId = instance.CorrelationId ?? instanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkflowInstance>> GetActiveAsync(string tenantId, string userId)
    {
        using var db = databaseFactory.CreateDatabase();

        var schemas = db.Fetch<PrismWorkflowInstanceSchema>(
            "WHERE TenantId = @0 AND UserId = @1 AND Status = 'Active' ORDER BY CreatedAt DESC",
            tenantId, userId);

        IReadOnlyList<WorkflowInstance> result = schemas.Select(s => new WorkflowInstance
        {
            InstanceId = s.InstanceId,
            TenantId = s.TenantId,
            DefinitionId = 0,
            DefinitionKey = s.WorkflowKey,
            CurrentState = s.CurrentStateKey,
            StateVersion = s.StateVersion,
            Status = s.Status,
            CorrelationId = null,
            InitiatedByUserId = s.UserId,
            StateJson = s.MetadataJson,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        return Task.FromResult(result);
    }

    private string GetResponseState(WorkflowInstance instance, WorkflowDefinition definition)
    {
        if (instance.Status == "Complete" || instance.Status == "Cancelled")
        {
            return "complete";
        }

        if (instance.Status == "Error")
        {
            return "error";
        }

        var currentState = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        if (currentState == null)
        {
            return "error";
        }

        return currentState.Archetype switch
        {
            "TaskQueue" => "wait",
            "Collect" or "Review" or "Decision" or "RequestChanges" => "ask_now",
            "Completion" => "complete",
            _ => "ask_now"
        };
    }
}
