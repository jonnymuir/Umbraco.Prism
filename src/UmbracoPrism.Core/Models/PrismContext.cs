using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.TokenCacheProviders;
using Serilog;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Models;

/// <summary>
/// Context implementation for managing the current tenant.
/// </summary>
public class PrismContext(IHttpContextAccessor httpContextAccessor, ISecretVaultService vault) : IPrismContext
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
        if(CurrentTenant == null)
        {
            Log.Error("Cannot refresh token: CurrentTenant is null.");
            return null;
        }
        
        var authority = $"https://{CurrentTenant.EntraTenantId}.ciamlogin.com/{CurrentTenant.EntraTenantId}/oauth2/v2.0/token";

        using var client = new HttpClient();

        var response = await client.PostAsync(authority, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", CurrentTenant.EntraClientId ?? string.Empty },
            { "client_secret", await vault.GetSecretAsync(CurrentTenant.SecretKeyName ?? string.Empty) },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "scope", $"openid profile offline_access {CurrentTenant.EntraClientId}/.default" }
        }));

        if (!response.IsSuccessStatusCode) return null;

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var newAccess = json.RootElement.GetProperty("access_token").GetString();
        var newRefresh = json.RootElement.GetProperty("refresh_token").GetString();
        var newExpires = DateTimeOffset.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32()).ToString("o");

        // 4. Update the Cookie (Re-issue the identity with new tokens)
        var props = authResult.Properties;

        if(props == null)
        {
            Log.Error("Cannot refresh token: AuthenticationProperties is null.");
            return null;
        }

        props.UpdateTokenValue("access_token", newAccess);
        props.UpdateTokenValue("refresh_token", newRefresh);
        props.UpdateTokenValue("expires_at", newExpires);

        // This updates the encrypted cookie in the browser for the NEXT request

        if(authResult.Principal == null)
        {
            Log.Error("Cannot refresh token: Principal is null.");
            return null;
        }
        
        await context.SignInAsync("PrismMemberCookie", authResult.Principal, props);

        return new AuthenticationHeaderValue("Bearer", newAccess);
    }
}