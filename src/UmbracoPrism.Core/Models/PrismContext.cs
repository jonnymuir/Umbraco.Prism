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

        string tokenEndpoint;
        string clientId;
        string clientSecret;
        string scope;
        IReadOnlyDictionary<string, string>? backchannelForwardingHeaders = null;

        if (!string.IsNullOrWhiteSpace(CurrentTenant.OidcAuthority))
        {
            if (string.IsNullOrWhiteSpace(CurrentTenant.OidcClientId))
            {
                Log.Error("Cannot refresh token: CurrentTenant is missing generic OIDC client configuration");
                LastAuthorizationFailureReason = "missing-oidc-client-config";
                return null;
            }

            var discoveredEndpoint = await DiscoverTokenEndpointAsync(context);
            if (discoveredEndpoint is null)
            {
                return null;
            }

            tokenEndpoint = discoveredEndpoint;

            // Development backchannel (Codespaces): rewrite the token endpoint to the internal host, and send
            // X-Forwarded-* so Keycloak derives the public issuer the refresh token was issued under (without
            // them it answers invalid_grant). Transport rewrite only: validation stays on the public authority.
            var backchannelBase = UmbracoPrism.Core.Auth.PrismBackchannel.GetBase();
            if (!string.IsNullOrEmpty(backchannelBase))
            {
                tokenEndpoint = UmbracoPrism.Core.Auth.PrismBackchannel.Rewrite(tokenEndpoint);

                // Derive X-Forwarded-* values from the public OidcAuthority so Keycloak
                // computes its canonical issuer as https://<public-host>/realms/prism-dev.
                var authorityUri = new Uri(CurrentTenant.OidcAuthority!.TrimEnd('/'));
                backchannelForwardingHeaders = new Dictionary<string, string>
                {
                    ["X-Forwarded-Proto"] = authorityUri.Scheme,
                    ["X-Forwarded-Host"]  = authorityUri.Host
                };
            }

            clientId = CurrentTenant.OidcClientId;
            clientSecret = await vault.ResolveSecretAsync(CurrentTenant.OidcClientSecretProvider, CurrentTenant.OidcClientSecretReference);
            scope = PrismOidcConfiguration.GetRefreshScope(CurrentTenant) ?? string.Empty;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(CurrentTenant.EntraTenantId) || string.IsNullOrWhiteSpace(CurrentTenant.EntraClientId))
            {
                Log.Error("Cannot refresh token: CurrentTenant is missing Entra tenant/client configuration");
                LastAuthorizationFailureReason = "missing-entra-client-config";
                return null;
            }

            if (string.IsNullOrWhiteSpace(CurrentTenant.SecretKeyName))
            {
                Log.Error("Cannot refresh token: CurrentTenant secret reference is missing");
                LastAuthorizationFailureReason = "missing-secret-reference";
                return null;
            }

            tokenEndpoint = $"https://{CurrentTenant.EntraTenantId}.ciamlogin.com/{CurrentTenant.EntraTenantId}/oauth2/v2.0/token";
            clientId = CurrentTenant.EntraClientId;
            clientSecret = await vault.GetSecretAsync(CurrentTenant.SecretKeyName);
            scope = $"openid profile offline_access {CurrentTenant.EntraClientId}/.default";
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            Log.Error("Cannot refresh token: client secret could not be resolved from configured provider");
            LastAuthorizationFailureReason = "missing-client-secret";
            return null;
        }

        var formParameters = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        if (!string.IsNullOrWhiteSpace(scope))
        {
            formParameters["scope"] = scope;
        }

        var result = await tokenRefreshService.RefreshAsync(tokenEndpoint, formParameters, context.RequestAborted, backchannelForwardingHeaders);

        if (!result.Success || result.AccessToken == null)
        {
            LastAuthorizationFailureReason = result.FailureReason ?? "refresh-failed";
            return null;
        }

        var newExpires = DateTimeOffset.UtcNow
            .AddSeconds(result.ExpiresIn ?? 3600)
            .ToString("o");

        var props = authResult.Properties;
        if (props == null)
        {
            Log.Error("Cannot refresh token: AuthenticationProperties is null.");
            LastAuthorizationFailureReason = "missing-auth-properties";
            return null;
        }

        props.IssuedUtc = DateTimeOffset.UtcNow;
        props.UpdateTokenValue("access_token", result.AccessToken);
        if (result.RefreshToken != null)
            props.UpdateTokenValue("refresh_token", result.RefreshToken);
        props.UpdateTokenValue("expires_at", newExpires);
        
        // Clear any one-off redirect URI to prevent stale navigation state from
        // persisting across token refresh cycles (matches login flow hygiene)
        props.RedirectUri = null;

        if (authResult.Principal == null)
        {
            Log.Error("Cannot refresh token: Principal is null.");
            LastAuthorizationFailureReason = "missing-cookie-principal";
            return null;
        }

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
