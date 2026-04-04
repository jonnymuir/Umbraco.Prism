using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Resolves Prism tenants by host name and caches tenant metadata for request-time lookups.
/// </summary>
public class TenantService : ITenantService
{
    private const string TenantCacheKeyPrefix = "Prism_Tenant_";

    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly ILogger<TenantService> _logger;

    private long _cacheHits;
    private long _cacheMisses;
    private long _invalidations;
    private long _databaseLoads;

    /// <summary>
    /// Initializes a new tenant service with Umbraco database and runtime cache dependencies.
    /// </summary>
    /// <param name="databaseFactory">Factory used to open Umbraco database connections.</param>
    /// <param name="appCaches">Application cache container used for runtime tenant caching.</param>
    /// <param name="logger">Logger used for cache invalidation diagnostics.</param>
    public TenantService(IUmbracoDatabaseFactory databaseFactory, AppCaches appCaches, ILogger<TenantService> logger)
    {
        _databaseFactory = databaseFactory;
        _runtimeCache = appCaches.RuntimeCache;
        _logger = logger;
    }

    /// <summary>
    /// Looks up the tenant mapped to a request host and caches the resolved tenant for subsequent requests.
    /// </summary>
    /// <param name="domain">The request host name to resolve.</param>
    /// <returns>The resolved tenant, or <see langword="null"/> when no tenant matches.</returns>
    public Task<PrismTenant?> GetByDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return Task.FromResult<PrismTenant?>(null);

        var normalizedDomain = domain.Trim().ToLowerInvariant();
        var cacheKey = BuildTenantCacheKey(normalizedDomain);
        var populatedFromDatabase = false;

        // Callback executes only on cache miss, which allows hit/miss counting.
        var tenant = _runtimeCache.GetCacheItem<PrismTenant?>(cacheKey, () =>
        {
            populatedFromDatabase = true;
            Interlocked.Increment(ref _cacheMisses);
            Interlocked.Increment(ref _databaseLoads);

            using var db = _databaseFactory.CreateDatabase();

            var tenantSchema = db.FirstOrDefault<PrismTenantSchema>(
                "SELECT * FROM PrismTenants WHERE Hostname = @0",
                [normalizedDomain]);

            if (tenantSchema == null) return null;

            var brandingOverrides = ParseBrandingOverrides(tenantSchema.BrandingOverrides);
            var mobileBrandingOverrides = ParseBrandingOverrides(tenantSchema.MobileBrandingOverrides);

            return new PrismTenant
            {
                Id = tenantSchema.Id,
                Name = tenantSchema.Name,
                Hostname = tenantSchema.Hostname,
                EntraTenantId = tenantSchema.EntraTenantId,
                EntraClientId = tenantSchema.EntraClientId,
                SecretKeyName = tenantSchema.SecretKeyName,
                BrandingOverrides = brandingOverrides,
                MobileBrandingOverrides = mobileBrandingOverrides,
                BrandingCssDeclarations = BuildCssDeclarations(brandingOverrides),
                MobileBrandingCssDeclarations = BuildCssDeclarations(mobileBrandingOverrides),
                AllowBiometricLogin = tenantSchema.AllowBiometricLogin
            };
        }, TimeSpan.FromMinutes(30));

        if (!populatedFromDatabase)
        {
            Interlocked.Increment(ref _cacheHits);
        }

        return Task.FromResult(tenant);
    }

    /// <inheritdoc />
    public void InvalidateDomain(string domain, string reason = "unspecified")
    {
        if (string.IsNullOrWhiteSpace(domain)) return;

        var normalizedDomain = domain.Trim().ToLowerInvariant();
        var cacheKey = BuildTenantCacheKey(normalizedDomain);

        _runtimeCache.ClearByKey(cacheKey);
        Interlocked.Increment(ref _invalidations);

        _logger.LogInformation(
            "Prism tenant cache invalidated for domain '{Domain}'. Reason: {Reason}",
            normalizedDomain,
            reason);
    }

    /// <inheritdoc />
    public void InvalidateDomains(IEnumerable<string> domains, string reason = "unspecified")
    {
        foreach (var domain in domains
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            InvalidateDomain(domain, reason);
        }
    }

    /// <inheritdoc />
    public TenantCacheMetricsSnapshot GetCacheMetrics() =>
        new(
            Hits: Interlocked.Read(ref _cacheHits),
            Misses: Interlocked.Read(ref _cacheMisses),
            Invalidations: Interlocked.Read(ref _invalidations),
            DatabaseLoads: Interlocked.Read(ref _databaseLoads));

    private static string BuildTenantCacheKey(string domain) =>
        $"{TenantCacheKeyPrefix}{domain}";

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

    private static string BuildCssDeclarations(IReadOnlyDictionary<string, string> overrides)
    {
        if (overrides.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var (name, value) in overrides)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            builder.Append(name.Trim());
            builder.Append(':');
            builder.Append(value.Trim());
            builder.Append(';');
        }

        return builder.ToString();
    }
}
