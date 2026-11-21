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
            LastAuthorizationFailureReason = PrismRefreshOutcome.MissingAccessTokenReason(refreshToken);
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
        LastAuthorizationFailureReason = PrismRefreshOutcome.ExpiredReason(forceRefresh, shouldRefreshForRuntimeRestart);
        return refreshToken == null ? null : await RefreshTokenAsync(context, authResult, refreshToken);
    }

    private readonly PrismRefreshRequestResolver _refreshRequests = new(vault, oidcDiscovery);

    /// <summary>
    /// Posts the refresh-token grant for the current tenant. Returns null, with the reason recorded, when
    /// the request could not be built (no tenant, wrong tenant, missing client configuration).
    /// </summary>
    private async Task<TokenRefreshResult?> RequestRefreshAsync(HttpContext context, ClaimsPrincipal? principal, string refreshToken)
    {
        if (CurrentTenant == null)
        {
            Log.Error("Cannot refresh token: CurrentTenant is null.");
            LastAuthorizationFailureReason = "missing-current-tenant";
            return null;
        }

        if (principal == null || !tenantBindingValidator.IsBound(principal, CurrentTenant))
        {
            Log.Warning("Rejecting token refresh because principal tenant claim does not match resolved tenant context");
            LastAuthorizationFailureReason = "tenant-mismatch";
            return null;
        }

        var resolved = await _refreshRequests.ResolveAsync(CurrentTenant, context.RequestAborted);
        if (resolved.Request is not { } request)
        {
            LastAuthorizationFailureReason = resolved.FailureReason;
            return null;
        }

        return await tokenRefreshService.RefreshAsync(
            request.TokenEndpoint, request.ToForm(refreshToken), context.RequestAborted, request.ForwardingHeaders);
    }

    private async Task<AuthenticationHeaderValue?> RefreshTokenAsync(HttpContext context, AuthenticateResult authResult, string refreshToken)
    {
        var result = await RequestRefreshAsync(context, authResult.Principal, refreshToken);
        if (result is null)
        {
            return null;
        }

        if (!result.Success || result.AccessToken == null)
        {
            LastAuthorizationFailureReason = result.FailureReason ?? "refresh-failed";
            return null;
        }

        return await ApplyRefreshedTokensAsync(context, authResult, result);
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
        PrismRefreshOutcome.Store(props, result);

        // Clear any one-off redirect URI to prevent stale navigation state from
        // persisting across token refresh cycles (matches login flow hygiene)
        props.RedirectUri = null;

        await context.SignInAsync("PrismMemberCookie", authResult.Principal, props);

        LastAuthorizationFailureReason = null;
        return new AuthenticationHeaderValue("Bearer", result.AccessToken);
    }

    /// <inheritdoc />
    public async Task<PrismSessionCheck> RevalidateSessionAsync(HttpContext context, AuthenticationProperties properties, ClaimsPrincipal principal)
    {
        var refreshToken = properties.GetTokenValue("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            return PrismSessionCheck.Skipped;
        }

        var result = await RequestRefreshAsync(context, principal, refreshToken);
        return result is null ? PrismSessionCheck.Unreachable : PrismRefreshOutcome.ToSessionCheck(properties, result);
    }

    private static bool ShouldRefreshForRuntimeRestart(AuthenticationProperties? properties)
    {
        var issuedUtc = properties?.IssuedUtc;
        return issuedUtc.HasValue && issuedUtc.Value < ProcessStartedUtc;
    }
}
