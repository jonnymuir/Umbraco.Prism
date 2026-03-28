namespace UmbracoPrism.Core.Services;

/// <summary>
/// Read-only snapshot of tenant cache counters for diagnostics and observability.
/// </summary>
/// <param name="Hits">Number of lookups served from cache.</param>
/// <param name="Misses">Number of lookups that required cache population.</param>
/// <param name="Invalidations">Number of explicit cache invalidations performed.</param>
/// <param name="DatabaseLoads">Number of tenant loads executed against the database.</param>
public sealed record TenantCacheMetricsSnapshot(
    long Hits,
    long Misses,
    long Invalidations,
    long DatabaseLoads);
