using Microsoft.IdentityModel.Tokens;
using Serilog;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>Where, and as which client, a tenant's refresh-token grant is posted.</summary>
internal sealed record PrismRefreshRequest(
    string TokenEndpoint,
    string ClientId,
    string ClientSecret,
    string Scope,
    IReadOnlyDictionary<string, string>? ForwardingHeaders)
{
    /// <summary>The form body of the refresh-token grant for <paramref name="refreshToken"/>.</summary>
    public Dictionary<string, string> ToForm(string refreshToken)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "client_secret", ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        if (!string.IsNullOrWhiteSpace(Scope))
        {
            form["scope"] = Scope;
        }

        return form;
    }
}

/// <summary>A refresh request, or the reason one could not be built.</summary>
internal sealed record PrismRefreshRequestResult(PrismRefreshRequest? Request, string? FailureReason)
{
    public static PrismRefreshRequestResult Ok(PrismRefreshRequest request) => new(request, null);

    public static PrismRefreshRequestResult Fail(string reason, string logMessage)
    {
        Log.Error("Cannot refresh token: {Reason}", logMessage);
        return new(null, reason);
    }
}

/// <summary>
/// Works out the refresh-token grant for a tenant: the token endpoint (discovered for a generic OIDC
/// provider, fixed for Entra), the client and its secret from the vault, and the scope.
/// </summary>
internal sealed class PrismRefreshRequestResolver(ISecretVaultService vault, IPrismOidcDiscovery oidcDiscovery)
{
    public async Task<PrismRefreshRequestResult> ResolveAsync(PrismTenant tenant, CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(tenant.OidcAuthority)
            ? await ResolveEntraAsync(tenant)
            : await ResolveGenericAsync(tenant, cancellationToken);

        return result.Request is not null && string.IsNullOrWhiteSpace(result.Request.ClientSecret)
            ? PrismRefreshRequestResult.Fail("missing-client-secret", "client secret could not be resolved from configured provider")
            : result;
    }

    private async Task<PrismRefreshRequestResult> ResolveGenericAsync(PrismTenant tenant, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant.OidcClientId))
        {
            return PrismRefreshRequestResult.Fail("missing-oidc-client-config", "CurrentTenant is missing generic OIDC client configuration");
        }

        var tokenEndpoint = await DiscoverTokenEndpointAsync(tenant.OidcAuthority!, cancellationToken);
        if (tokenEndpoint is null)
        {
            return PrismRefreshRequestResult.Fail("oidc-discovery-failed", "OIDC discovery failed for the tenant's authority");
        }

        // Development backchannel (Codespaces): rewrite the token endpoint to the internal host, and send
        // X-Forwarded-* so Keycloak derives the public issuer the refresh token was issued under (without
        // them it answers invalid_grant). Transport rewrite only: validation stays on the public authority.
        IReadOnlyDictionary<string, string>? forwardingHeaders = null;
        if (!string.IsNullOrEmpty(PrismBackchannel.GetBase()))
        {
            tokenEndpoint = PrismBackchannel.Rewrite(tokenEndpoint);
            forwardingHeaders = PublicIssuerHeaders(tenant.OidcAuthority!);
        }

        var secret = await vault.ResolveSecretAsync(tenant.OidcClientSecretProvider, tenant.OidcClientSecretReference);
        return PrismRefreshRequestResult.Ok(new PrismRefreshRequest(
            tokenEndpoint, tenant.OidcClientId, secret, PrismOidcConfiguration.GetRefreshScope(tenant) ?? string.Empty, forwardingHeaders));
    }

    private async Task<PrismRefreshRequestResult> ResolveEntraAsync(PrismTenant tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.EntraTenantId) || string.IsNullOrWhiteSpace(tenant.EntraClientId))
        {
            return PrismRefreshRequestResult.Fail("missing-entra-client-config", "CurrentTenant is missing Entra tenant/client configuration");
        }

        if (string.IsNullOrWhiteSpace(tenant.SecretKeyName))
        {
            return PrismRefreshRequestResult.Fail("missing-secret-reference", "CurrentTenant secret reference is missing");
        }

        return PrismRefreshRequestResult.Ok(new PrismRefreshRequest(
            $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/token",
            tenant.EntraClientId,
            await vault.GetSecretAsync(tenant.SecretKeyName),
            $"openid profile offline_access {tenant.EntraClientId}/.default",
            null));
    }

    private async Task<string?> DiscoverTokenEndpointAsync(string authority, CancellationToken cancellationToken)
    {
        try
        {
            return (await oidcDiscovery.GetAsync(authority, cancellationToken)).TokenEndpoint;
        }
        catch (Exception ex) when (ex is SecurityTokenException or HttpRequestException or InvalidOperationException)
        {
            Log.Error(ex, "OIDC discovery failed for the tenant's authority");
            return null;
        }
    }

    // X-Forwarded-* values from the public authority, so Keycloak computes its canonical issuer as
    // https://<public-host>/realms/prism-dev.
    private static Dictionary<string, string> PublicIssuerHeaders(string authority)
    {
        var authorityUri = new Uri(authority.TrimEnd('/'));
        return new Dictionary<string, string>
        {
            ["X-Forwarded-Proto"] = authorityUri.Scheme,
            ["X-Forwarded-Host"] = authorityUri.Host
        };
    }
}
