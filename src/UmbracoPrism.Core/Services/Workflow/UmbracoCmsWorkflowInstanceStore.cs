using System.Text.Json;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// <see cref="IWorkflowInstanceStore"/> backed by the prismCmsWorkflowInstance table — durable
/// across an app-pool recycle, but each row carries a sliding <c>ExpiresUtc</c> so an instance
/// still dies with the visitor's session rather than persisting indefinitely like a business
/// workflow's instance history would.
/// </summary>
public sealed class UmbracoCmsWorkflowInstanceStore(
    IUmbracoDatabaseFactory databaseFactory,
    TimeSpan? slidingExpiration = null) : IWorkflowInstanceStore
{
    private readonly TimeSpan _slidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool TryGet(string instanceId, out WorkflowInstanceState instance)
    {
        using var db = databaseFactory.CreateDatabase();
        var row = db.FirstOrDefault<PrismCmsWorkflowInstanceSchema>(
            "SELECT * FROM prismCmsWorkflowInstance WHERE InstanceId = @0", instanceId);

        if (row is null || row.ExpiresUtc < DateTime.UtcNow)
        {
            instance = null!;
            return false;
        }

        var state = JsonSerializer.Deserialize<WorkflowInstanceState>(row.StateJson, JsonOptions);
        if (state is null)
        {
            instance = null!;
            return false;
        }

        // Refresh the sliding window on read — an active visitor keeps their instance alive.
        db.Execute(
            "UPDATE prismCmsWorkflowInstance SET ExpiresUtc = @0 WHERE InstanceId = @1",
            DateTime.UtcNow.Add(_slidingExpiration), instanceId);

        instance = state;
        return true;
    }

    public void Save(WorkflowInstanceState instance)
    {
        using var db = databaseFactory.CreateDatabase();
        var expiresUtc = DateTime.UtcNow.Add(_slidingExpiration);
        var json = JsonSerializer.Serialize(instance, JsonOptions);

        var rowsAffected = db.Execute(
            "UPDATE prismCmsWorkflowInstance SET WorkflowKey = @0, TenantId = @1, UserId = @2, " +
            "StateJson = @3, ExpiresUtc = @4, UpdatedUtc = @5 WHERE InstanceId = @6",
            instance.WorkflowKey, instance.TenantId, instance.UserId, json, expiresUtc, DateTime.UtcNow,
            instance.InstanceId);

        if (rowsAffected == 0)
        {
            db.Insert(new PrismCmsWorkflowInstanceSchema
            {
                InstanceId = instance.InstanceId,
                WorkflowKey = instance.WorkflowKey,
                TenantId = instance.TenantId,
                UserId = instance.UserId,
                StateJson = json,
                ExpiresUtc = expiresUtc,
                UpdatedUtc = DateTime.UtcNow
            });
        }
    }

    public bool Remove(string instanceId)
    {
        using var db = databaseFactory.CreateDatabase();
        var rowsAffected = db.Execute(
            "DELETE FROM prismCmsWorkflowInstance WHERE InstanceId = @0", instanceId);
        return rowsAffected > 0;
    }

    public void Clear()
    {
        using var db = databaseFactory.CreateDatabase();
        db.Execute("DELETE FROM prismCmsWorkflowInstance");
    }

    public IEnumerable<WorkflowInstanceState> GetAll()
    {
        // No server-side filtering in IWorkflowInstanceStore's contract, so callers (e.g.
        // WorkflowRuntimeEngine.FindLatestInstance) filter in memory over this full fetch —
        // acceptable for a single CMS Workflow's expected visitor volume; expired rows are
        // excluded so a stale, not-yet-swept row never resurfaces as "latest".
        using var db = databaseFactory.CreateDatabase();
        var rows = db.Fetch<PrismCmsWorkflowInstanceSchema>(
            "SELECT * FROM prismCmsWorkflowInstance WHERE ExpiresUtc >= @0", DateTime.UtcNow);

        return rows
            .Select(row => JsonSerializer.Deserialize<WorkflowInstanceState>(row.StateJson, JsonOptions))
            .Where(state => state is not null)
            .Select(state => state!)
            .ToArray();
    }
}
