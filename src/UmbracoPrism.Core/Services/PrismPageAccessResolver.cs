using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Extensions;
using UmbracoPrism.Core.Logging;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Caches the entire prismPageAccessPolicies table as one Dictionary&lt;Guid, PrismPageAccessPolicy&gt;
/// under a single runtime-cache key — mirrors TenantService.GetByDomainAsync's own caching shape
/// (see its own remarks) rather than one cache entry per page: resolving one page is then an O(1)
/// dictionary lookup regardless of how many policies exist in total, so this doesn't get slower as
/// the page catalog grows.
/// </summary>
public class PrismPageAccessResolver : IPrismPageAccessResolver
{
    private const string CacheKey = "Prism_PageAccessPolicies_All";

    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly ILogger<PrismPageAccessResolver> _logger;

    public PrismPageAccessResolver(
        IUmbracoDatabaseFactory databaseFactory,
        AppCaches appCaches,
        ILogger<PrismPageAccessResolver> logger)
    {
        _databaseFactory = databaseFactory;
        _runtimeCache = appCaches.RuntimeCache;
        _logger = logger;
    }

    public PrismPageAccessResult Resolve(Guid contentKey, PrismTenant? tenant, bool isAuthenticated)
    {
        var policies = GetAllPolicies();
        if (policies is null)
        {
            return PrismPageAccessResult.Unavailable;
        }

        if (!policies.TryGetValue(contentKey, out var policy))
        {
            return PrismPageAccessResult.Public;
        }

        if (policy.AllowedTenantNames.Count > 0 &&
            (tenant is null || !policy.AllowedTenantNames.Contains(tenant.Name, StringComparer.OrdinalIgnoreCase)))
        {
            return PrismPageAccessResult.Unavailable;
        }

        if (policy.RequiresSignIn && !isAuthenticated)
        {
            return PrismPageAccessResult.RequiresSignIn;
        }

        return PrismPageAccessResult.Public;
    }

    public void Invalidate(string reason = "unspecified")
    {
        _runtimeCache.ClearByKey(CacheKey);
        _logger.LogInformation(
            "Prism page-access policy cache invalidated. Reason: {Reason}",
            LogScrub.Line(reason));
    }

    private Dictionary<Guid, PrismPageAccessPolicy>? GetAllPolicies()
    {
        try
        {
            // The whole GetCacheItem call, not just the factory, is inside this try — same
            // reasoning as TenantService.GetByDomainAsync: a factory exception must never be
            // cached as "no policies configured", so the next request retries rather than a
            // transient failure sticking.
            return _runtimeCache.GetCacheItem(CacheKey, LoadAllFromDatabase, TimeSpan.FromMinutes(30))
                ?? [];
        }
        catch (Exception ex)
        {
            // Fail closed: when the policy table cannot be read we cannot know which pages are
            // protected, so every page is unavailable for this request. Not cached, so the next
            // request retries (a cold-boot race against the table's migration clears itself).
            _logger.LogError(ex,
                "Page-access policy lookup failed due to an unexpected database error; denying the page for this request.");
            return null;
        }
    }

    private Dictionary<Guid, PrismPageAccessPolicy> LoadAllFromDatabase()
    {
        using var db = _databaseFactory.CreateDatabase();
        var rows = db.Fetch<PrismPageAccessPolicySchema>();

        return rows.ToDictionary(
            row => row.ContentKey,
            row => new PrismPageAccessPolicy
            {
                Id = row.Id,
                ContentKey = row.ContentKey,
                ContentRoute = row.ContentRoute,
                RequiresSignIn = row.RequiresSignIn,
                AllowedTenantNames = ParseAllowList(row.TenantAllowListJson)
            });
    }

    private static string[] ParseAllowList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
