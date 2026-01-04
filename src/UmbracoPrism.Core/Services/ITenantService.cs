using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service interface for managing tenants.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets a tenant by its domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <returns></returns>
    Task<PrismTenant?> GetByDomainAsync(string domain);
}