using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Resolves the concrete host(s) a tenant's OIDC/Entra provider sets its own SSO session
/// cookies on inside Prism's mobile WebView — used to target native cookie-store cleanup on
/// sign-out (see prism-biometric-signout.js's own remarks: a biometric-tagged session skips the
/// federated sign-out redirect entirely, which otherwise would have cleared this cookie as a
/// side effect, so it's left stale in the WebView's persistent cookie store until something else
/// clears it).
///
/// Deliberately concrete hosts only, no wildcard patterns — unlike
/// <c>PrismSecurityHeadersMiddleware.BuildOidcFormActionSources</c>, which needs the <c>*.</c>
/// form for its CSP directive. Kept as its own small list rather than shared with that one
/// because the two callers need different shapes of the same underlying tenant data.
/// </summary>
public static class PrismIdentityProviderHosts
{
    public static IReadOnlyList<string> Resolve(PrismTenant? tenant)
    {
        if (tenant is null) return [];

        var hosts = new List<string>();

        var oidcAuthority = tenant.OidcAuthority?.Trim();
        if (!string.IsNullOrWhiteSpace(oidcAuthority) &&
            Uri.TryCreate(oidcAuthority, UriKind.Absolute, out var authorityUri))
        {
            hosts.Add(authorityUri.Authority);
        }

        var entraTenantId = tenant.EntraTenantId?.Trim();
        if (!string.IsNullOrWhiteSpace(entraTenantId))
        {
            hosts.Add("login.microsoftonline.com");
            hosts.Add($"{entraTenantId}.ciamlogin.com");
            hosts.Add($"{entraTenantId}.b2clogin.com");
        }

        return hosts;
    }
}
