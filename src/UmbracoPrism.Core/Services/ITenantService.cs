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
}