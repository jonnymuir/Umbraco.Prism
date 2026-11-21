using System.Security.Authentication;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Logging;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Auth;

/// <summary>The generic-OIDC steps of the sign-in flow that depend on the provider's discovery document and signing keys.</summary>
internal static partial class PrismOidcFlowHelpers
{
    /// <summary>The named HTTP client for the code exchange: a bounded timeout, and a handler the factory manages.</summary>
    internal const string TokenHttpClientName = "prism-oidc-token";

    /// <summary>The OAuth <c>error</c> and <c>error_description</c> from a token endpoint response, cut short.</summary>
    internal static string DescribeTokenError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty body)";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            string? Read(string name) =>
                root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            var described = $"{Read("error")} {Read("error_description")}".Trim();
            return described.Length == 0 ? "(no error field)" : described.Length > 300 ? described[..300] : described;
        }
        catch (JsonException)
        {
            return "(unparseable body)";
        }
    }

    /// <summary>
    /// The provider's endpoints from its validated discovery document, or null (logged) when the
    /// document cannot be read or does not belong to <paramref name="authority"/>.
    /// </summary>
    internal static async Task<PrismOidcEndpoints?> TryGetEndpointsAsync(HttpContext http, string authority, ILogger logger, string phase)
    {
        try
        {
            return await http.RequestServices.GetRequiredService<IPrismOidcDiscovery>().GetAsync(authority, http.RequestAborted);
        }
        catch (Exception ex) when (ex is SecurityTokenException or HttpRequestException or InvalidOperationException or OperationCanceledException)
        {
            LogDiscoveryFailed(logger, ex, phase, LogScrub.Line(authority) ?? string.Empty);
            return null;
        }
    }

    /// <summary>
    /// RFC 9207: when the provider says who answered, it must be the authority this tenant signed in
    /// to. Defends against a response from a different provider.
    /// </summary>
    internal static void EnsureResponseIssuerMatches(OpenIdConnectMessage message, string authority)
    {
        if (!string.IsNullOrEmpty(message.Iss)
            && !string.Equals(message.Iss.TrimEnd('/'), authority.TrimEnd('/'), StringComparison.Ordinal))
        {
            throw new AuthenticationException("The authorization response was issued by a different provider than the tenant's authority.");
        }
    }

    /// <summary>
    /// The signing keys for an ID token. The shared signing-key cache does the discovery, the
    /// Development backchannel rewrite and the refresh throttling.
    /// </summary>
    internal static async Task<IEnumerable<SecurityKey>> GetIdTokenSigningKeysAsync(IPrismSigningKeyCache cache, string authority, string? keyId)
    {
        var key = authority.TrimEnd('/');
        var snapshot = cache.GetSnapshot(key, keyId);
        if (snapshot.IsExpired || !snapshot.ContainsRequestedKey)
        {
            await cache.WarmAsync(key, $"{key}/.well-known/openid-configuration", forceRefresh: true, requiredKeyId: keyId);
            snapshot = cache.GetSnapshot(key, keyId);
        }

        return snapshot.ContainsRequestedKey && !snapshot.IsExpired ? snapshot.Keys : [];
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "OIDC discovery failed during {Phase} for authority {Authority}")]
    private static partial void LogDiscoveryFailed(ILogger logger, Exception exception, string phase, string authority);
}
