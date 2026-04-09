using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Repository for workflow definition persistence operations.
/// </summary>
public class WorkflowDefinitionRepository(
    IUmbracoDatabaseFactory databaseFactory) : IWorkflowDefinitionRepository
{
    /// <inheritdoc/>
    public Task<WorkflowDefinition?> GetByKeyAsync(string tenantId, string definitionKey)
    {
        using var db = databaseFactory.CreateDatabase();

        var schema = db.SingleOrDefault<PrismWorkflowDefinitionSchema>(
            "WHERE TenantId = @0 AND WorkflowKey = @1 AND Status = 'Published' ORDER BY Id DESC",
            tenantId, definitionKey);

        if (schema == null)
        {
            return Task.FromResult<WorkflowDefinition?>(null);
        }

        return Task.FromResult<WorkflowDefinition?>(MapFromSchema(schema));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(string tenantId)
    {
        using var db = databaseFactory.CreateDatabase();

        var schemas = db.Fetch<PrismWorkflowDefinitionSchema>(
            "WHERE TenantId = @0 AND Status = 'Published' ORDER BY WorkflowKey, Id DESC",
            tenantId);

        IReadOnlyList<WorkflowDefinition> result = schemas.Select(MapFromSchema).ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(WorkflowDefinition definition)
    {
        using var db = databaseFactory.CreateDatabase();

        var schema = new PrismWorkflowDefinitionSchema
        {
            Id = definition.Id,
            WorkflowKey = definition.DefinitionKey,
            Version = definition.Version,
            TenantId = definition.TenantId,
            Title = definition.DisplayName,
            Description = null,
            Status = "Published",
            StatesJson = JsonSerializer.Serialize(definition.States),
            TransitionsJson = JsonSerializer.Serialize(definition.Transitions),
            CreatedAt = definition.CreatedAt,
            PublishedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        if (definition.Id > 0)
        {
            db.Update(schema);
        }
        else
        {
            db.Insert(schema);
        }

        return Task.CompletedTask;
    }

    private WorkflowDefinition MapFromSchema(PrismWorkflowDefinitionSchema schema)
    {
        var states = JsonSerializer.Deserialize<List<WorkflowState>>(schema.StatesJson) ?? new();
        var transitions = JsonSerializer.Deserialize<List<WorkflowTransition>>(schema.TransitionsJson) ?? new();

        return new WorkflowDefinition
        {
            Id = schema.Id,
            TenantId = schema.TenantId,
            DefinitionKey = schema.WorkflowKey,
            DisplayName = schema.Title,
            Version = schema.Version,
            States = states,
            Transitions = transitions,
            InitialState = states.FirstOrDefault()?.StateKey ?? string.Empty,
            CreatedAt = schema.CreatedAt,
            UpdatedAt = schema.PublishedAt ?? schema.CreatedAt
        };
    }
}
