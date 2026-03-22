using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;

namespace UmbracoPrism.Core.Services;

public sealed class PrismSigningKeyCache(IHttpClientFactory httpClientFactory) : IPrismSigningKeyCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, (IReadOnlyCollection<SecurityKey> Keys, DateTimeOffset FetchedAt)> _store = new();

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

    public IEnumerable<SecurityKey> GetSigningKeys(string entraTenantId)
    {
        if (_store.TryGetValue(entraTenantId, out var cached))
            return cached.Keys;
        return [];
    }
}
