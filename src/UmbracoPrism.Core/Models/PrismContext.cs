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
public class PrismContext(
    IHttpContextAccessor httpContextAccessor,
    ISecretVaultService vault,
    IPrismTokenRefreshService tokenRefreshService) : IPrismContext
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

        if (!IsPrincipalBoundToCurrentTenant(authResult.Principal))
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

    private async Task<AuthenticationHeaderValue?> RefreshTokenAsync(HttpContext context, AuthenticateResult authResult, string refreshToken)
    {
        if (CurrentTenant == null)
        {
            Log.Error("Cannot refresh token: CurrentTenant is null.");
            LastAuthorizationFailureReason = "missing-current-tenant";
            return null;
        }

        if (authResult.Principal == null || !IsPrincipalBoundToCurrentTenant(authResult.Principal))
        {
            Log.Warning("Rejecting token refresh because principal tenant claim does not match resolved tenant context");
            LastAuthorizationFailureReason = "tenant-mismatch";
            return null;
        }

        string tokenEndpoint;
        string clientId;
        string clientSecret;
        string scope;

        if (!string.IsNullOrWhiteSpace(CurrentTenant.OidcAuthority))
        {
            if (string.IsNullOrWhiteSpace(CurrentTenant.OidcClientId))
            {
                Log.Error("Cannot refresh token: CurrentTenant is missing generic OIDC client configuration");
                LastAuthorizationFailureReason = "missing-oidc-client-config";
                return null;
            }

            tokenEndpoint = $"{CurrentTenant.OidcAuthority.TrimEnd('/')}/protocol/openid-connect/token";

            // In Codespaces, the GitHub forwarded-port proxy blocks server-side HTTP calls to
            // the public Keycloak URL (*.app.github.dev). When KEYCLOAK_BACKCHANNEL_URL is set
            // AND the environment is Development, rewrite the token endpoint host to the internal
            // backchannel URL before POSTing the refresh-token grant.
            // Transport rewrite ONLY: issuer/audience validation on the returned tokens remains
            // strict against the public OidcAuthority. Outside Development or when the env var
            // is absent, this block is never entered — zero behaviour change for production.
            var backchannelBase = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
            var isDevelopment = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);
            if (isDevelopment && !string.IsNullOrEmpty(backchannelBase))
            {
                var oidcPath = new Uri(CurrentTenant.OidcAuthority!.TrimEnd('/')).AbsolutePath.TrimEnd('/');
                tokenEndpoint = $"{backchannelBase.TrimEnd('/')}{oidcPath}/protocol/openid-connect/token";
                Console.WriteLine($"[PRISM] RefreshTokenAsync: rewriting token endpoint to backchannel → {tokenEndpoint}");
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

        var result = await tokenRefreshService.RefreshAsync(tokenEndpoint, formParameters, context.RequestAborted);

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

    private bool IsPrincipalBoundToCurrentTenant(ClaimsPrincipal principal)
    {
        if (CurrentTenant == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CurrentTenant.OidcAuthority))
        {
            return IsGenericOidcPrincipalBoundToCurrentTenant(principal);
        }

        var tenantId = CurrentTenant.EntraTenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        var principalTenantId = principal.FindFirstValue("tid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");

        return !string.IsNullOrWhiteSpace(principalTenantId)
            && string.Equals(principalTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsGenericOidcPrincipalBoundToCurrentTenant(ClaimsPrincipal principal)
    {
        if (CurrentTenant == null || string.IsNullOrWhiteSpace(CurrentTenant.OidcAuthority))
        {
            return false;
        }

        var principalIssuer = principal.FindFirstValue("iss");
        if (!UrisMatch(principalIssuer, CurrentTenant.OidcAuthority))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentTenant.OidcClientId))
        {
            return true;
        }

        var audienceMatches = principal.FindAll("aud")
            .Select(claim => claim.Value)
            .Any(audience => string.Equals(audience, CurrentTenant.OidcClientId, StringComparison.OrdinalIgnoreCase));
        var authorizedPartyMatches = string.Equals(
            principal.FindFirstValue("azp"),
            CurrentTenant.OidcClientId,
            StringComparison.OrdinalIgnoreCase);

        return audienceMatches || authorizedPartyMatches;
    }

    private static bool UrisMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            left.TrimEnd('/'),
            right.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRefreshForRuntimeRestart(AuthenticationProperties? properties)
    {
        var issuedUtc = properties?.IssuedUtc;
        return issuedUtc.HasValue && issuedUtc.Value < ProcessStartedUtc;
    }
}
