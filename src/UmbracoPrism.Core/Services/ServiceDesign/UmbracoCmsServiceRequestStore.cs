using System.Text.Json;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Models;

namespace UmbracoPrism.Core.Services.ServiceDesign;

/// <summary>
/// <see cref="IServiceRequestStore"/> backed by the prismCmsServiceRequest table — durable
/// across an app-pool recycle. An anonymous visitor's row carries a sliding <c>ExpiresUtc</c>
/// so it still dies with their session; an authenticated member's row
/// (<see cref="ServiceRequest.IsAuthenticated"/>) is stamped with <see cref="NeverExpires"/>
/// instead, so signing in and coming back next week finds it exactly where it was left — see
/// "My Requests" (<c>ServiceRequestHubController</c>).
/// </summary>
public sealed class UmbracoCmsServiceRequestStore(
    IUmbracoDatabaseFactory databaseFactory,
    TimeSpan? slidingExpiration = null) : IServiceRequestStore
{
    private readonly TimeSpan _slidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(30);

    /// <summary>
    /// Sentinel <c>ExpiresUtc</c> for an authenticated instance — deliberately not nullable-column
    /// "no expiry" to avoid a schema migration; every expiry comparison in this store
    /// (<c>WHERE ExpiresUtc &lt; @now</c> / <c>&gt;= @now</c>) already treats a value this far in
    /// the future as "never," with no special-casing needed anywhere else.
    /// </summary>
    private static readonly DateTime NeverExpires = DateTime.MaxValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool TryGet(string instanceId, out ServiceRequest instance)
    {
        using var db = databaseFactory.CreateDatabase();
        var row = db.FirstOrDefault<PrismCmsServiceRequestSchema>(
            "SELECT * FROM prismCmsServiceRequest WHERE InstanceId = @0", instanceId);

        if (row is null || row.ExpiresUtc < DateTime.UtcNow)
        {
            instance = null!;
            return false;
        }

        var state = JsonSerializer.Deserialize<ServiceRequest>(row.StateJson, JsonOptions);
        if (state is null)
        {
            instance = null!;
            return false;
        }

        // Refresh the sliding window on read — an active anonymous visitor keeps their instance
        // alive. Skipped for an authenticated instance: it's already at NeverExpires, and
        // re-issuing the same value on every read would just be a wasted write.
        if (!state.IsAuthenticated)
        {
            db.Execute(
                "UPDATE prismCmsServiceRequest SET ExpiresUtc = @0 WHERE InstanceId = @1",
                DateTime.UtcNow.Add(_slidingExpiration), instanceId);
        }

        instance = state;
        return true;
    }

    public void Save(ServiceRequest instance)
    {
        using var db = databaseFactory.CreateDatabase();
        var expiresUtc = instance.IsAuthenticated ? NeverExpires : DateTime.UtcNow.Add(_slidingExpiration);
        var json = JsonSerializer.Serialize(instance, JsonOptions);

        var rowsAffected = db.Execute(
            "UPDATE prismCmsServiceRequest SET BlueprintKey = @0, TenantId = @1, UserId = @2, " +
            "StateJson = @3, ExpiresUtc = @4, UpdatedUtc = @5 WHERE InstanceId = @6",
            instance.BlueprintKey, instance.TenantId, instance.UserId, json, expiresUtc, DateTime.UtcNow,
            instance.InstanceId);

        if (rowsAffected == 0)
        {
            db.Insert(new PrismCmsServiceRequestSchema
            {
                InstanceId = instance.InstanceId,
                BlueprintKey = instance.BlueprintKey,
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
            "DELETE FROM prismCmsServiceRequest WHERE InstanceId = @0", instanceId);
        return rowsAffected > 0;
    }

    public void Clear()
    {
        using var db = databaseFactory.CreateDatabase();
        db.Execute("DELETE FROM prismCmsServiceRequest");
    }

    public IEnumerable<ServiceRequest> GetAll()
    {
        // No server-side filtering in IServiceRequestStore's contract, so callers (e.g.
        // ProcessManagerEngine.FindLatestInstance) filter in memory over this full fetch —
        // acceptable for a single CMS Service Blueprint's expected visitor volume; expired rows
        // are excluded so a stale, not-yet-swept row never resurfaces as "latest".
        using var db = databaseFactory.CreateDatabase();
        var rows = db.Fetch<PrismCmsServiceRequestSchema>(
            "SELECT * FROM prismCmsServiceRequest WHERE ExpiresUtc >= @0", DateTime.UtcNow);

        return rows
            .Select(row => JsonSerializer.Deserialize<ServiceRequest>(row.StateJson, JsonOptions))
            .Where(state => state is not null)
            .Select(state => state!)
            .ToArray();
    }
}
