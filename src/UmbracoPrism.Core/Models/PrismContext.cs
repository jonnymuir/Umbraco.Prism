using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Serilog;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Models;

/// <summary>
/// Context implementation for managing the current tenant.
/// </summary>
public class PrismContext(
    IHttpContextAccessor httpContextAccessor,
    ISecretVaultService vault,
    IPrismTokenRefreshService tokenRefreshService) : IPrismContext
{
    /// <summary>
    /// Gets or sets the current tenant.
    /// </summary>
    public PrismTenant? CurrentTenant { get; set; }

    public async Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync()
    {
        var context = httpContextAccessor.HttpContext;

        if(context == null)
        {
            return null;
        }

        // Grab tokens from the encrypted cookie
        var authResult = await context.AuthenticateAsync("PrismMemberCookie");
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

        var tokenEndpoint = $"https://{CurrentTenant.EntraTenantId}.ciamlogin.com/{CurrentTenant.EntraTenantId}/oauth2/v2.0/token";

        var clientSecret = await vault.GetSecretAsync(CurrentTenant.SecretKeyName ?? string.Empty);

        var formParameters = new Dictionary<string, string>
        {
            { "client_id", CurrentTenant.EntraClientId ?? string.Empty },
            { "client_secret", clientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "scope", $"openid profile offline_access {CurrentTenant.EntraClientId}/.default" }
        };

        var result = await tokenRefreshService.RefreshAsync(tokenEndpoint, formParameters);

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
}