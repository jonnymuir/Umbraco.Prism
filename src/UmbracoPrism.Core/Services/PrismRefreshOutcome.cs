using Microsoft.AspNetCore.Authentication;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>What the member's session does with the result of a refresh-token grant.</summary>
internal static class PrismRefreshOutcome
{
    /// <summary>
    /// Stores a successful grant's tokens in the session and reports it active. A failed grant maps to
    /// revoked only when the provider answered invalid_grant, which says the session or refresh token is
    /// no longer valid (the member was removed, disabled, signed out everywhere or the grant expired).
    /// Anything else, such as a timeout or an open circuit, says nothing about the member.
    /// </summary>
    public static PrismSessionCheck ToSessionCheck(AuthenticationProperties properties, TokenRefreshResult result)
    {
        if (!result.Success || result.AccessToken is null)
        {
            return string.Equals(result.FailureReason, "invalid_grant", StringComparison.Ordinal)
                ? PrismSessionCheck.Revoked
                : PrismSessionCheck.Unreachable;
        }

        Store(properties, result);
        return PrismSessionCheck.Active;
    }

    public static void Store(AuthenticationProperties properties, TokenRefreshResult result)
    {
        properties.UpdateTokenValue("access_token", result.AccessToken!);
        if (result.RefreshToken != null)
            properties.UpdateTokenValue("refresh_token", result.RefreshToken);
        properties.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn ?? 3600).ToString("o"));
    }

    public static string MissingAccessTokenReason(string? refreshToken) =>
        refreshToken == null ? "missing-access-token" : "refresh-required";

    public static string ExpiredReason(bool forceRefresh, bool runtimeRestart) =>
        forceRefresh ? "forced-refresh-required" : runtimeRestart ? "runtime-restart-refresh-required" : "token-expired";
}
