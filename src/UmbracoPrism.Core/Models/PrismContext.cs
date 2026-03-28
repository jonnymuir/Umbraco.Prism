using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Security.Claims;
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
    /// <summary>
    /// Gets or sets the current tenant.
    /// </summary>
    public PrismTenant? CurrentTenant { get; set; }

    /// <summary>
    /// Gets a valid bearer authorization header for downstream tenant-aware API calls.
    /// </summary>
    /// <returns>A bearer header when available; otherwise <see langword="null"/>.</returns>
    public async Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync()
    {
        var context = httpContextAccessor.HttpContext;

        if(context == null)
        {
            return null;
        }

        // Grab tokens from the encrypted cookie
        var authResult = await context.AuthenticateAsync("PrismMemberCookie");
        if (!authResult.Succeeded || authResult.Principal == null)
        {
            return null;
        }

        if (!IsPrincipalBoundToCurrentTenant(authResult.Principal))
        {
            Log.Warning("Rejecting token usage because principal tenant claim does not match resolved tenant context");
            return null;
        }

        var tokens = authResult.Properties?.GetTokens();

        var accessToken = tokens?.FirstOrDefault(t => t.Name == "access_token")?.Value;
        var refreshToken = tokens?.FirstOrDefault(t => t.Name == "refresh_token")?.Value;
        var expiresAtStr = tokens?.FirstOrDefault(t => t.Name == "expires_at")?.Value;

        if (string.IsNullOrEmpty(accessToken)) return null;

        // Check if expired (with a 1-minute buffer)
        if (DateTimeOffset.TryParse(expiresAtStr, out var expiresAt) && expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // EXPIRED: Manual Refresh
        return refreshToken == null ? null : await RefreshTokenAsync(context, authResult, refreshToken);
    }

    private async Task<AuthenticationHeaderValue?> RefreshTokenAsync(HttpContext context, AuthenticateResult authResult, string refreshToken)
    {
        if (CurrentTenant == null)
        {
            Log.Error("Cannot refresh token: CurrentTenant is null.");
            return null;
        }

        if (authResult.Principal == null || !IsPrincipalBoundToCurrentTenant(authResult.Principal))
        {
            Log.Warning("Rejecting token refresh because principal tenant claim does not match resolved tenant context");
            return null;
        }

        if (string.IsNullOrWhiteSpace(CurrentTenant.EntraTenantId) || string.IsNullOrWhiteSpace(CurrentTenant.EntraClientId))
        {
            Log.Error("Cannot refresh token: CurrentTenant is missing Entra tenant/client configuration");
            return null;
        }

        if (string.IsNullOrWhiteSpace(CurrentTenant.SecretKeyName))
        {
            Log.Error("Cannot refresh token: CurrentTenant secret reference is missing");
            return null;
        }

        var tokenEndpoint = $"https://{CurrentTenant.EntraTenantId}.ciamlogin.com/{CurrentTenant.EntraTenantId}/oauth2/v2.0/token";

        var clientSecret = await vault.GetSecretAsync(CurrentTenant.SecretKeyName);
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            Log.Error("Cannot refresh token: client secret could not be resolved from vault");
            return null;
        }

        var formParameters = new Dictionary<string, string>
        {
            { "client_id", CurrentTenant.EntraClientId },
            { "client_secret", clientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "scope", $"openid profile offline_access {CurrentTenant.EntraClientId}/.default" }
        };

        var result = await tokenRefreshService.RefreshAsync(tokenEndpoint, formParameters, context.RequestAborted);

        if (!result.Success || result.AccessToken == null)
            return null;

        var newExpires = DateTimeOffset.UtcNow
            .AddSeconds(result.ExpiresIn ?? 3600)
            .ToString("o");

        var props = authResult.Properties;
        if (props == null)
        {
            Log.Error("Cannot refresh token: AuthenticationProperties is null.");
            return null;
        }

        props.UpdateTokenValue("access_token", result.AccessToken);
        if (result.RefreshToken != null)
            props.UpdateTokenValue("refresh_token", result.RefreshToken);
        props.UpdateTokenValue("expires_at", newExpires);

        if (authResult.Principal == null)
        {
            Log.Error("Cannot refresh token: Principal is null.");
            return null;
        }

        await context.SignInAsync("PrismMemberCookie", authResult.Principal, props);

        return new AuthenticationHeaderValue("Bearer", result.AccessToken);
    }

    private bool IsPrincipalBoundToCurrentTenant(ClaimsPrincipal principal)
    {
        var tenantId = CurrentTenant?.EntraTenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        var principalTenantId = principal.FindFirstValue("tid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");

        return !string.IsNullOrWhiteSpace(principalTenantId)
            && string.Equals(principalTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }
}