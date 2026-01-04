using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;


/// <summary>
/// Service implementation for managing tenants.
/// </summary>
public class TenantService : ITenantService
{
    /// <summary>
    /// Gets a tenant by its domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <returns></returns>
    public async Task<PrismTenant?> GetByDomainAsync(string domain)
    {
        // Hardcoded - will wire up to database later.
        // Simulate an async database call
        await Task.Yield(); 

        if (domain.Contains("localhost"))
        {
            return new PrismTenant 
            { 
                Id = 1, 
                Name = "Localhost Client", 
                ThemeColor = "#e74c3c" 
            };
        }
        
        return null;
    }
}