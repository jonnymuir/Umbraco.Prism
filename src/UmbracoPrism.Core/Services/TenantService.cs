using System.Text.Json;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

public class TenantService : ITenantService
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IAppPolicyCache _runtimeCache;

    public TenantService(IUmbracoDatabaseFactory databaseFactory, AppCaches appCaches)
    {
        _databaseFactory = databaseFactory;
        _runtimeCache = appCaches.RuntimeCache;
    }

    public async Task<PrismTenant?> GetByDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return null;

        string cacheKey = $"Prism_Tenant_{domain}";

        // We explicitly tell the cache we are looking for a PrismTenant (nullable)
        return _runtimeCache.GetCacheItem<PrismTenant?>(cacheKey, () =>
        {
            using var db = _databaseFactory.CreateDatabase();

            var tenantSchema = db.FirstOrDefault<PrismTenantSchema>(
                "SELECT * FROM PrismTenants WHERE Hostname = @0",
                [domain]);

            // If no tenant is found in the DB, return null so we don't cache an empty object
            if (tenantSchema == null) return null;

            return new PrismTenant
            {
                Id = tenantSchema.Id,
                Name = tenantSchema.Name,
                Hostname = tenantSchema.Hostname,
                ThemeColor = tenantSchema.ThemeColor ?? "#3490dc",
                EntraTenantId = tenantSchema.EntraTenantId,
                EntraClientId = tenantSchema.EntraClientId,
                SecretKeyName = tenantSchema.SecretKeyName,
                BrandingOverrides = ParseBrandingOverrides(tenantSchema.BrandingOverrides),
                MobileBrandingOverrides = ParseBrandingOverrides(tenantSchema.MobileBrandingOverrides)
            };
        }, TimeSpan.FromMinutes(30));
    }

    private static Dictionary<string, string> ParseBrandingOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}