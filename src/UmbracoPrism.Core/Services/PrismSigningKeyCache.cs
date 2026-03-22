using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// In-memory cache of Entra signing keys keyed by tenant identifier.
/// </summary>
/// <param name="httpClientFactory">Factory used to create HTTP clients for OIDC metadata retrieval.</param>
public sealed class PrismSigningKeyCache(IHttpClientFactory httpClientFactory) : IPrismSigningKeyCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, (IReadOnlyCollection<SecurityKey> Keys, DateTimeOffset FetchedAt)> _store = new();

    /// <summary>
    /// Fetches and caches signing keys for the provided tenant when the cache is missing or expired.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token for metadata retrieval.</param>
    /// <returns>A task that completes when keys are cached.</returns>
    public async Task WarmAsync(string entraTenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entraTenantId)) return;

        if (_store.TryGetValue(entraTenantId, out var existing) && DateTimeOffset.UtcNow - existing.FetchedAt < Ttl)
            return;

        var metadataAddress = $"https://{entraTenantId}.ciamlogin.com/{entraTenantId}/v2.0/.well-known/openid-configuration";
        var http = httpClientFactory.CreateClient("prism-oidc-metadata");
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(http) { RequireHttps = true });

        var config = await manager.GetConfigurationAsync(cancellationToken);
        _store[entraTenantId] = (config.SigningKeys.ToList().AsReadOnly(), DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Returns cached signing keys for a tenant.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier.</param>
    /// <returns>Cached signing keys, or an empty sequence if no cache entry exists.</returns>
    public IEnumerable<SecurityKey> GetSigningKeys(string entraTenantId)
    {
        if (_store.TryGetValue(entraTenantId, out var cached))
            return cached.Keys;
        return [];
    }
}
