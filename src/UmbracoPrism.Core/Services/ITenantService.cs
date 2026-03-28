using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service interface for managing tenants.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets a tenant by its hostname/domain value.
    /// </summary>
    /// <param name="domain">The incoming request host used for tenant resolution.</param>
    /// <returns>The matching tenant, or <see langword="null"/> when no tenant is configured for the host.</returns>
    Task<PrismTenant?> GetByDomainAsync(string domain);

    /// <summary>
    /// Invalidates the cached tenant entry for a single host name.
    /// </summary>
    /// <param name="domain">The tenant host name whose cached entry should be removed.</param>
    /// <param name="reason">Optional reason used for diagnostics.</param>
    void InvalidateDomain(string domain, string reason = "unspecified");

    /// <summary>
    /// Invalidates cached tenant entries for multiple host names.
    /// </summary>
    /// <param name="domains">The host names whose cached entries should be removed.</param>
    /// <param name="reason">Optional reason used for diagnostics.</param>
    void InvalidateDomains(IEnumerable<string> domains, string reason = "unspecified");

    /// <summary>
    /// Returns runtime metrics for tenant-cache effectiveness and invalidation behavior.
    /// </summary>
    /// <returns>A snapshot of tenant-cache counters.</returns>
    TenantCacheMetricsSnapshot GetCacheMetrics();
}