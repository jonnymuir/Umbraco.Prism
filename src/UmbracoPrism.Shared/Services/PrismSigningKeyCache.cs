using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// In-memory cache of Entra signing keys keyed by tenant identifier.
/// </summary>
public sealed class PrismSigningKeyCache : IPrismSigningKeyCache
{
    internal static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(45);
    internal static readonly TimeSpan HardExpiry = TimeSpan.FromHours(1);
    internal static readonly TimeSpan ForcedRefreshCooldown = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (IReadOnlyCollection<SecurityKey> Keys, DateTimeOffset FetchedAt)> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _warmLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly Func<HttpClient, string, bool, IConfigurationManager<OpenIdConnectConfiguration>> _configurationManagerFactory;

    /// <summary>
    /// Creates a signing-key cache with the default metadata retriever and system clock.
    /// </summary>
    /// <param name="httpClientFactory">Factory used to create OIDC metadata HTTP clients.</param>
    public PrismSigningKeyCache(IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, TimeProvider.System, CreateConfigurationManager)
    {
    }

    internal PrismSigningKeyCache(
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        Func<HttpClient, string, bool, IConfigurationManager<OpenIdConnectConfiguration>> configurationManagerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _configurationManagerFactory = configurationManagerFactory;
    }

    /// <summary>
    /// Fetches and caches signing keys for the provided tenant when the cache is missing or expired.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier.</param>
    /// <param name="forceRefresh">When <see langword="true"/>, bypasses TTL checks and refreshes immediately.</param>
    /// <param name="cancellationToken">Cancellation token for metadata retrieval.</param>
    /// <returns>A task that completes when keys are cached.</returns>
    public async Task WarmAsync(string entraTenantId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entraTenantId)) return;

        var normalizedTenantId = entraTenantId.Trim();
        var requestStartedAt = _timeProvider.GetUtcNow();

        if (!forceRefresh && !GetSnapshot(normalizedTenantId).ShouldRefresh)
            return;

        var warmLock = _warmLocks.GetOrAdd(normalizedTenantId, _ => new SemaphoreSlim(1, 1));
        await warmLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetSnapshot(normalizedTenantId);
            if (!forceRefresh && !snapshot.ShouldRefresh)
                return;

            if (forceRefresh
                && _store.TryGetValue(normalizedTenantId, out var forcedRefreshExisting)
                && requestStartedAt - forcedRefreshExisting.FetchedAt < ForcedRefreshCooldown)
            {
                return;
            }

            // Deduplicate overlapping forced refresh requests for the same tenant:
            // if another waiter already refreshed after this call started, reuse it.
            if (forceRefresh
                && _store.TryGetValue(normalizedTenantId, out var existing)
                && existing.FetchedAt >= requestStartedAt)
                return;

            var metadataAddress = $"https://{normalizedTenantId}.ciamlogin.com/{normalizedTenantId}/v2.0/.well-known/openid-configuration";
            var http = _httpClientFactory.CreateClient("prism-oidc-metadata");
            var manager = _configurationManagerFactory(http, metadataAddress, true);

            var config = await manager.GetConfigurationAsync(cancellationToken);
            var signingKeys = config.SigningKeys.ToList().AsReadOnly();
            if (signingKeys.Count == 0)
            {
                throw new SecurityTokenSignatureKeyNotFoundException("OIDC metadata did not contain any signing keys.");
            }

            _store[normalizedTenantId] = (signingKeys, _timeProvider.GetUtcNow());
        }
        finally
        {
            warmLock.Release();
        }
    }

    /// <summary>
    /// Fetches and caches signing keys using the provided metadata address when the cache is missing or expired.
    /// Use this overload for generic OIDC providers (e.g. Keycloak) where the metadata URL cannot be
    /// derived from a tenant identifier alone.
    /// </summary>
    /// <param name="tenantKey">The cache key for this tenant (e.g. the OidcAuthority URL).</param>
    /// <param name="metadataAddress">The OpenID Connect metadata URL to fetch signing keys from.</param>
    /// <param name="forceRefresh">When <see langword="true"/>, bypasses TTL checks and refreshes immediately.</param>
    /// <param name="cancellationToken">Cancellation token for metadata retrieval.</param>
    /// <returns>A task that completes when keys are cached.</returns>
    public async Task WarmAsync(string tenantKey, string metadataAddress, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantKey)) return;

        var normalizedKey = tenantKey.TrimEnd('/');
        var requestStartedAt = _timeProvider.GetUtcNow();

        if (!forceRefresh && !GetSnapshot(normalizedKey).ShouldRefresh)
            return;

        var warmLock = _warmLocks.GetOrAdd(normalizedKey, _ => new SemaphoreSlim(1, 1));
        await warmLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetSnapshot(normalizedKey);
            if (!forceRefresh && !snapshot.ShouldRefresh)
                return;

            if (forceRefresh
                && _store.TryGetValue(normalizedKey, out var forcedRefreshExisting)
                && requestStartedAt - forcedRefreshExisting.FetchedAt < ForcedRefreshCooldown)
            {
                return;
            }

            if (forceRefresh
                && _store.TryGetValue(normalizedKey, out var existing)
                && existing.FetchedAt >= requestStartedAt)
                return;

            var requireHttps = !metadataAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            var http = _httpClientFactory.CreateClient("prism-oidc-metadata");
            var manager = _configurationManagerFactory(http, metadataAddress, requireHttps);

            var config = await manager.GetConfigurationAsync(cancellationToken);
            var signingKeys = config.SigningKeys.ToList().AsReadOnly();
            if (signingKeys.Count == 0)
            {
                throw new SecurityTokenSignatureKeyNotFoundException("OIDC metadata did not contain any signing keys.");
            }

            _store[normalizedKey] = (signingKeys, _timeProvider.GetUtcNow());
        }
        finally
        {
            warmLock.Release();
        }
    }

    /// <summary>
    /// Returns cached signing-key state for a tenant.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier.</param>
    /// <param name="keyId">Optional signing-key identifier required by the current token.</param>
    /// <returns>A snapshot describing the current cache state.</returns>
    public PrismSigningKeyCacheSnapshot GetSnapshot(string entraTenantId, string? keyId = null)
    {
        if (string.IsNullOrWhiteSpace(entraTenantId))
        {
            return new PrismSigningKeyCacheSnapshot([], true, true, false);
        }

        var normalizedTenantId = entraTenantId.Trim();
        if (!_store.TryGetValue(normalizedTenantId, out var existing))
        {
            return new PrismSigningKeyCacheSnapshot([], true, true, false);
        }

        var age = _timeProvider.GetUtcNow() - existing.FetchedAt;
        var containsRequestedKey = string.IsNullOrWhiteSpace(keyId)
            || existing.Keys.Any(key => string.Equals(key.KeyId, keyId, StringComparison.OrdinalIgnoreCase));

        return new PrismSigningKeyCacheSnapshot(
            existing.Keys,
            age >= RefreshAfter || !containsRequestedKey,
            age >= HardExpiry,
            containsRequestedKey);
    }

    /// <summary>
    /// Returns cached signing keys for a tenant.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier.</param>
    /// <returns>Cached signing keys, or an empty sequence if no cache entry exists.</returns>
    public IEnumerable<SecurityKey> GetSigningKeys(string entraTenantId)
    {
        return GetSnapshot(entraTenantId).Keys;
    }

    private static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(HttpClient httpClient, string metadataAddress, bool requireHttps)
    {
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(httpClient) { RequireHttps = requireHttps });
    }
}
