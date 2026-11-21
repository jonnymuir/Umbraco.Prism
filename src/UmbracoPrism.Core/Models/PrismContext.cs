using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Security.Claims;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Models;

/// <summary>
/// Context implementation for managing the current tenant and downstream authorization headers.
/// </summary>
/// <param name="httpContextAccessor">Provides access to the current HTTP context and authentication state.</param>
/// <param name="vault">Resolves tenant client secrets from secure storage.</param>
/// <param name="tokenRefreshService">Performs resilient token refresh calls when session tokens expire.</param>
/// <param name="tenantBindingValidator">Determines whether a principal's own claims match the current tenant.</param>
/// <param name="oidcDiscovery">Supplies a generic OIDC provider's endpoints from its discovery document.</param>
public class PrismContext(
    IHttpContextAccessor httpContextAccessor,
    ISecretVaultService vault,
    IPrismTokenRefreshService tokenRefreshService,
    IPrismTenantBindingValidator tenantBindingValidator,
    IPrismOidcDiscovery oidcDiscovery) : IPrismContext
{
    private static readonly DateTimeOffset ProcessStartedUtc = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the current tenant.
    /// </summary>
    public PrismTenant? CurrentTenant { get; set; }

    /// <inheritdoc />
    public string? LastAuthorizationFailureReason { get; private set; }

    /// <summary>
    /// Gets a valid bearer authorization header for downstream tenant-aware API calls.
    /// </summary>
    /// <returns>A bearer header when available; otherwise <see langword="null"/>.</returns>
    public async Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false)
    {
        var context = httpContextAccessor.HttpContext;

        if(context == null)
        {
            LastAuthorizationFailureReason = "missing-http-context";
            return null;
        }

        // Grab tokens from the encrypted cookie
        var authResult = await context.AuthenticateAsync("PrismMemberCookie");
        if (!authResult.Succeeded || authResult.Principal == null)
        {
            LastAuthorizationFailureReason = "missing-cookie-principal";
            return null;
        }

        if (CurrentTenant is null || !tenantBindingValidator.IsBound(authResult.Principal, CurrentTenant))
        {
            Log.Warning("Rejecting token usage because principal tenant claim does not match resolved tenant context");
            LastAuthorizationFailureReason = "tenant-mismatch";
            return null;
        }

        var tokens = authResult.Properties?.GetTokens();

        var accessToken = tokens?.FirstOrDefault(t => t.Name == "access_token")?.Value;
        var refreshToken = tokens?.FirstOrDefault(t => t.Name == "refresh_token")?.Value;
        var expiresAtStr = tokens?.FirstOrDefault(t => t.Name == "expires_at")?.Value;
        var shouldRefreshForRuntimeRestart = ShouldRefreshForRuntimeRestart(authResult.Properties);

        if (string.IsNullOrEmpty(accessToken))
        {
            LastAuthorizationFailureReason = refreshToken == null
                ? "missing-access-token"
                : "refresh-required";
            return refreshToken == null ? null : await RefreshTokenAsync(context, authResult, refreshToken);
        }

        // Check if expired (with a 1-minute buffer)
        if (!forceRefresh &&
            !shouldRefreshForRuntimeRestart &&
            DateTimeOffset.TryParse(expiresAtStr, out var expiresAt) &&
            expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            LastAuthorizationFailureReason = null;
            return new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // EXPIRED: Manual Refresh
        LastAuthorizationFailureReason = forceRefresh
            ? "forced-refresh-required"
            : shouldRefreshForRuntimeRestart
                ? "runtime-restart-refresh-required"
                : "token-expired";
        return refreshToken == null ? null : await RefreshTokenAsync(context, authResult, refreshToken);
    }

    private async Task<string?> DiscoverTokenEndpointAsync(HttpContext context)
    {
        try
        {
            return (await oidcDiscovery.GetAsync(CurrentTenant!.OidcAuthority!, context.RequestAborted)).TokenEndpoint;
        }
        catch (Exception ex) when (ex is Microsoft.IdentityModel.Tokens.SecurityTokenException or HttpRequestException or InvalidOperationException)
        {
            Log.Error(ex, "Cannot refresh token: OIDC discovery failed for the tenant's authority");
            LastAuthorizationFailureReason = "oidc-discovery-failed";
            return null;
        }
    }

    private sealed record RefreshRequest(
        string TokenEndpoint,
        string ClientId,
        string ClientSecret,
        string Scope,
        IReadOnlyDictionary<string, string>? ForwardingHeaders);

    private async Task<AuthenticationHeaderValue?> RefreshTokenAsync(HttpContext context, AuthenticateResult authResult, string refreshToken)
    {
        if (CurrentTenant == null)
        {
            Log.Error("Cannot refresh token: CurrentTenant is null.");
            LastAuthorizationFailureReason = "missing-current-tenant";
            return null;
        }

        if (authResult.Principal == null || !tenantBindingValidator.IsBound(authResult.Principal, CurrentTenant))
        {
            Log.Warning("Rejecting token refresh because principal tenant claim does not match resolved tenant context");
            LastAuthorizationFailureReason = "tenant-mismatch";
            return null;
        }

        var request = await ResolveRefreshRequestAsync(context);
        if (request is null)
        {
            return null;
        }

        var result = await tokenRefreshService.RefreshAsync(
            request.TokenEndpoint, BuildRefreshForm(request, refreshToken), context.RequestAborted, request.ForwardingHeaders);

        if (!result.Success || result.AccessToken == null)
        {
            LastAuthorizationFailureReason = result.FailureReason ?? "refresh-failed";
            return null;
        }

        return await ApplyRefreshedTokensAsync(context, authResult, result);
    }

    /// <summary>
    /// Works out where and as whom the refresh-token grant is posted for the current tenant. Returns null,
    /// with the reason recorded, when the tenant's client configuration or secret is missing.
    /// </summary>
    private async Task<RefreshRequest?> ResolveRefreshRequestAsync(HttpContext context)
    {
        var request = string.IsNullOrWhiteSpace(CurrentTenant!.OidcAuthority)
            ? await ResolveEntraRefreshRequestAsync()
            : await ResolveGenericRefreshRequestAsync(context);

        if (request is not null && string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            Log.Error("Cannot refresh token: client secret could not be resolved from configured provider");
            LastAuthorizationFailureReason = "missing-client-secret";
            return null;
        }

        return request;
    }

    private async Task<RefreshRequest?> ResolveGenericRefreshRequestAsync(HttpContext context)
    {
        var tenant = CurrentTenant!;
        if (string.IsNullOrWhiteSpace(tenant.OidcClientId))
        {
            Log.Error("Cannot refresh token: CurrentTenant is missing generic OIDC client configuration");
            LastAuthorizationFailureReason = "missing-oidc-client-config";
            return null;
        }

        var tokenEndpoint = await DiscoverTokenEndpointAsync(context);
        if (tokenEndpoint is null)
        {
            return null;
        }

        // Development backchannel (Codespaces): rewrite the token endpoint to the internal host, and send
        // X-Forwarded-* so Keycloak derives the public issuer the refresh token was issued under (without
        // them it answers invalid_grant). Transport rewrite only: validation stays on the public authority.
        IReadOnlyDictionary<string, string>? forwardingHeaders = null;
        if (!string.IsNullOrEmpty(UmbracoPrism.Core.Auth.PrismBackchannel.GetBase()))
        {
            tokenEndpoint = UmbracoPrism.Core.Auth.PrismBackchannel.Rewrite(tokenEndpoint);
            forwardingHeaders = PublicIssuerHeaders(tenant.OidcAuthority!);
        }

        var secret = await vault.ResolveSecretAsync(tenant.OidcClientSecretProvider, tenant.OidcClientSecretReference);
        return new RefreshRequest(
            tokenEndpoint, tenant.OidcClientId, secret, PrismOidcConfiguration.GetRefreshScope(tenant) ?? string.Empty, forwardingHeaders);
    }

    private async Task<RefreshRequest?> ResolveEntraRefreshRequestAsync()
    {
        var tenant = CurrentTenant!;
        if (string.IsNullOrWhiteSpace(tenant.EntraTenantId) || string.IsNullOrWhiteSpace(tenant.EntraClientId))
        {
            Log.Error("Cannot refresh token: CurrentTenant is missing Entra tenant/client configuration");
            LastAuthorizationFailureReason = "missing-entra-client-config";
            return null;
        }

        if (string.IsNullOrWhiteSpace(tenant.SecretKeyName))
        {
            Log.Error("Cannot refresh token: CurrentTenant secret reference is missing");
            LastAuthorizationFailureReason = "missing-secret-reference";
            return null;
        }

        return new RefreshRequest(
            $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/token",
            tenant.EntraClientId,
            await vault.GetSecretAsync(tenant.SecretKeyName),
            $"openid profile offline_access {tenant.EntraClientId}/.default",
            null);
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

    private static Dictionary<string, string> BuildRefreshForm(RefreshRequest request, string refreshToken)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", request.ClientId },
            { "client_secret", request.ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        if (!string.IsNullOrWhiteSpace(request.Scope))
        {
            form["scope"] = request.Scope;
        }

        return form;
    }

    private async Task<AuthenticationHeaderValue?> ApplyRefreshedTokensAsync(
        HttpContext context, AuthenticateResult authResult, TokenRefreshResult result)
    {
        var props = authResult.Properties;
        if (props == null)
        {
            Log.Error("Cannot refresh token: AuthenticationProperties is null.");
            LastAuthorizationFailureReason = "missing-auth-properties";
            return null;
        }

        if (authResult.Principal == null)
        {
            Log.Error("Cannot refresh token: Principal is null.");
            LastAuthorizationFailureReason = "missing-cookie-principal";
            return null;
        }

        props.IssuedUtc = DateTimeOffset.UtcNow;
        props.UpdateTokenValue("access_token", result.AccessToken!);
        if (result.RefreshToken != null)
            props.UpdateTokenValue("refresh_token", result.RefreshToken);
        props.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn ?? 3600).ToString("o"));

        // Clear any one-off redirect URI to prevent stale navigation state from
        // persisting across token refresh cycles (matches login flow hygiene)
        props.RedirectUri = null;

        await context.SignInAsync("PrismMemberCookie", authResult.Principal, props);

        LastAuthorizationFailureReason = null;
        return new AuthenticationHeaderValue("Bearer", result.AccessToken);
    }

    private static bool ShouldRefreshForRuntimeRestart(AuthenticationProperties? properties)
    {
        var issuedUtc = properties?.IssuedUtc;
        return issuedUtc.HasValue && issuedUtc.Value < ProcessStartedUtc;
    }
}
