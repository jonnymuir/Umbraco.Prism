using System.Collections.Concurrent;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Discovery for generic OIDC authorities. One long-lived <see cref="ConfigurationManager{T}"/> per
/// authority does the caching and refresh throttling, so this class holds no fetch logic of its own.
/// </summary>
public sealed class PrismOidcDiscovery(IHttpClientFactory httpClientFactory) : IPrismOidcDiscovery
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<PrismOidcEndpoints> GetAsync(string authority, CancellationToken cancellationToken = default)
    {
        var normalized = (authority ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var authorityUri))
        {
            throw new SecurityTokenException("The OIDC authority is not an absolute URL.");
        }

        var manager = _managers.GetOrAdd(normalized, _ => CreateManager(normalized, authorityUri));
        var configuration = await manager.GetConfigurationAsync(cancellationToken);

        // The mix-up defence: the issuer the provider declares must be the authority we configured.
        // Skipped only for the Development backchannel, where discovery is fetched from an internal
        // address that can report a different issuer.
        if (PrismBackchannel.GetBase() is null
            && !string.Equals(configuration.Issuer?.TrimEnd('/'), normalized, StringComparison.Ordinal))
        {
            throw new SecurityTokenInvalidIssuerException(
                "The discovery document's issuer does not match the configured authority.");
        }

        if (string.IsNullOrWhiteSpace(configuration.AuthorizationEndpoint)
            || string.IsNullOrWhiteSpace(configuration.TokenEndpoint))
        {
            throw new SecurityTokenException("The discovery document lacks an authorization or token endpoint.");
        }

        return new PrismOidcEndpoints(
            configuration.Issuer ?? normalized,
            configuration.AuthorizationEndpoint,
            configuration.TokenEndpoint,
            string.IsNullOrWhiteSpace(configuration.EndSessionEndpoint) ? null : configuration.EndSessionEndpoint);
    }

    private ConfigurationManager<OpenIdConnectConfiguration> CreateManager(string authority, Uri authorityUri)
    {
        var http = httpClientFactory.CreateClient("prism-oidc-metadata");
        var metadataAddress = $"{authority}/.well-known/openid-configuration";
        var requireHttps = authorityUri.Scheme != Uri.UriSchemeHttp;
        IDocumentRetriever retriever = new HttpDocumentRetriever(http) { RequireHttps = requireHttps };

        var backchannel = PrismBackchannel.GetBase();
        if (backchannel is not null
            && Uri.TryCreate(backchannel, UriKind.Absolute, out var backchannelUri)
            && authorityUri.Scheme == Uri.UriSchemeHttps)
        {
            retriever = new BackchannelRewritingDocumentRetriever(authorityUri, backchannelUri, retriever);
        }

        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            retriever);
    }
}
