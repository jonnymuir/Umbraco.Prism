using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// What a host's own Content-Security-Policy must allow for Prism's sign-in to work. Prism sets no
/// security headers itself: the policy is the host's, and a host that runs one merges these
/// sources into it (the reference site's SecurityHeadersMiddleware does).
/// </summary>
public static class PrismContentSecurityPolicy
{
    /// <summary>
    /// The <c>form-action</c> and <c>frame-src</c> sources the sign-in and sign-out redirects to
    /// <paramref name="tenant"/>'s identity provider need, keyed by directive. Empty when the
    /// tenant has no sign-in configured, since Prism then never redirects anywhere.
    /// </summary>
    public static IReadOnlyDictionary<string, string> SignInSources(PrismTenant? tenant)
    {
        if (tenant?.HasSignInConfigured() != true)
        {
            return new Dictionary<string, string>();
        }

        var sources = string.Join(' ', IdentityProviderHosts(tenant).Select(host => $"https://{host}"));
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["form-action"] = sources,
            ["frame-src"] = sources,
        };
    }

    // Wildcard forms included, unlike PrismIdentityProviderHosts: Entra can redirect between
    // its own login hosts during a single sign-in, and a CSP source may name a wildcard.
    private static IReadOnlyList<string> IdentityProviderHosts(PrismTenant tenant)
    {
        var hosts = new List<string>();

        var oidcAuthority = tenant.OidcAuthority?.Trim();
        if (!string.IsNullOrWhiteSpace(oidcAuthority) &&
            Uri.TryCreate(oidcAuthority, UriKind.Absolute, out var authorityUri))
        {
            AddHost(hosts, authorityUri.Authority);
        }

        var entraTenantId = tenant.EntraTenantId?.Trim();
        if (!string.IsNullOrWhiteSpace(entraTenantId))
        {
            AddHost(hosts, "login.microsoftonline.com");
            AddHost(hosts, "*.ciamlogin.com");
            AddHost(hosts, "*.b2clogin.com");
            AddHost(hosts, $"{entraTenantId}.ciamlogin.com");
            AddHost(hosts, $"{entraTenantId}.b2clogin.com");
        }

        return hosts;
    }

    private static void AddHost(List<string> hosts, string host)
    {
        if (!hosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            hosts.Add(host);
        }
    }
}
