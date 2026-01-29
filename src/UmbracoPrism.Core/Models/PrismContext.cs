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
public class PrismContext(IHttpContextAccessor httpContextAccessor, ISecretVaultService vault, IMsalTokenCacheProvider tokenCacheProvider, ILogger<PrismContext> logger) : IPrismContext
{
    /// <summary>
    /// Gets or sets the current tenant.
    /// </summary>
    public PrismTenant? CurrentTenant { get; set; }

    public async Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync()
    {
        var context = httpContextAccessor.HttpContext;
        // 1. Grab tokens from the encrypted cookie
        var authResult = await context.AuthenticateAsync("PrismMemberCookie");
        var tokens = authResult.Properties?.GetTokens();

        var accessToken = tokens?.FirstOrDefault(t => t.Name == "access_token")?.Value;
        var refreshToken = tokens?.FirstOrDefault(t => t.Name == "refresh_token")?.Value;
        var expiresAtStr = tokens?.FirstOrDefault(t => t.Name == "expires_at")?.Value;

        if (string.IsNullOrEmpty(accessToken)) return null;

        // 2. Check if expired (with a 1-minute buffer)
        if (DateTimeOffset.TryParse(expiresAtStr, out var expiresAt) && expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // 3. EXPIRED: Manual Refresh
        return await RefreshTokenAsync(context, authResult, refreshToken);
    }

    private async Task<AuthenticationHeaderValue?> RefreshTokenAsync(HttpContext context, AuthenticateResult authResult, string refreshToken)
    {
        var tenant = CurrentTenant;
        var authority = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/token";

        using var client = new HttpClient();
        var response = await client.PostAsync(authority, new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "client_id", tenant.EntraClientId },
        { "client_secret", await vault.GetSecretAsync(tenant.SecretKeyName) },
        { "grant_type", "refresh_token" },
        { "refresh_token", refreshToken },
        { "scope", $"openid profile offline_access {tenant.EntraClientId}/.default" }
    }));

        if (!response.IsSuccessStatusCode) return null;

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var newAccess = json.RootElement.GetProperty("access_token").GetString();
        var newRefresh = json.RootElement.GetProperty("refresh_token").GetString();
        var newExpires = DateTimeOffset.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32()).ToString("o");

        // 4. Update the Cookie (Re-issue the identity with new tokens)
        var props = authResult.Properties;
        props.UpdateTokenValue("access_token", newAccess);
        props.UpdateTokenValue("refresh_token", newRefresh);
        props.UpdateTokenValue("expires_at", newExpires);

        // This updates the encrypted cookie in the browser for the NEXT request
        await context.SignInAsync("PrismMemberCookie", authResult.Principal, props);

        return new AuthenticationHeaderValue("Bearer", newAccess);
    }
}