using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// The one definition of a tenant identity Prism will send server-side requests to. The controller,
/// uSync and the seeder call it when a tenant is written, and <see cref="TenantService"/> calls it
/// again on the resolved values, so a row that arrived by any route is still refused at runtime.
/// </summary>
public static partial class PrismTenantValidator
{
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$")]
    private static partial Regex EntraTenantIdPattern();

    /// <summary>Returns an error message for an unacceptable identity, or null when it is acceptable.</summary>
    /// <param name="oidcAuthority">The generic OIDC authority, resolved of any tokens.</param>
    /// <param name="entraTenantId">The Entra tenant id or CIAM subdomain, resolved of any tokens.</param>
    public static string? Validate(string? oidcAuthority, string? entraTenantId) =>
        Validate(
            oidcAuthority,
            entraTenantId,
            PrismDemoTenants.IsAllowed
                || string.Equals(Environment.GetEnvironmentVariable(InternalAuthoritiesVariable)?.Trim(), "true", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Set to <c>true</c> to accept an identity provider on a private or loopback address, for a
    /// provider that really is on an internal network.
    /// </summary>
    public const string InternalAuthoritiesVariable = "Prism__AllowInternalAuthorities";

    internal static string? Validate(string? oidcAuthority, string? entraTenantId, bool allowDemo)
    {
        if (!string.IsNullOrWhiteSpace(oidcAuthority))
        {
            var authorityError = ValidateAuthority(oidcAuthority.Trim(), allowDemo);
            if (authorityError is not null)
            {
                return authorityError;
            }
        }

        if (!string.IsNullOrWhiteSpace(entraTenantId) && !EntraTenantIdPattern().IsMatch(entraTenantId.Trim()))
        {
            return "The Entra tenant id must be a single DNS label or GUID, with no dots, slashes or other characters.";
        }

        return null;
    }

    private static string? ValidateAuthority(string authority, bool allowDemo)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri))
        {
            return "The OIDC authority must be an absolute URL.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return "The OIDC authority must not contain credentials, a query string or a fragment.";
        }

        var internalHost = IsInternalHost(uri.Host);
        if (internalHost && !allowDemo)
        {
            return "The OIDC authority must be a public host, not a loopback, private or link-local address.";
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return null;
        }

        // Plain http is only for the local demo identity provider.
        return uri.Scheme == Uri.UriSchemeHttp && allowDemo && internalHost
            ? null
            : "The OIDC authority must use https.";
    }

    private static bool IsInternalHost(string host)
    {
        var trimmed = host.Trim('[', ']');
        if (IPAddress.TryParse(trimmed, out var address))
        {
            return IsInternalAddress(address);
        }

        return trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || !trimmed.Contains('.');
    }

    private static bool IsInternalAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork
            ? IsInternalIPv4(bytes)
            : address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || (bytes[0] & 0xFE) == 0xFC;
    }

    private static bool IsInternalIPv4(byte[] bytes) =>
        bytes[0] switch
        {
            0 or 10 => true,
            172 => bytes[1] is >= 16 and <= 31,
            192 => bytes[1] == 168,
            169 => bytes[1] == 254,
            100 => bytes[1] is >= 64 and <= 127,
            _ => false
        };
}
