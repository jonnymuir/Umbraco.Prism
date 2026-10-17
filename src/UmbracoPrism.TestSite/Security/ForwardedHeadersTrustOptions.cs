using Microsoft.AspNetCore.HttpOverrides;

namespace UmbracoPrism.TestSite.Security;

/// <summary>
/// Configures which upstream proxies the reference site trusts to supply
/// <c>X-Forwarded-For</c>/<c>X-Forwarded-Proto</c>; configure under <c>TestSite:ForwardedHeaders</c>.
/// Proxy trust is the host's decision (only it knows what sits in front of it), so it lives here
/// rather than in the Prism package. It matters to Prism all the same: the biometric-exchange
/// rate limiter reads RemoteIpAddress, and OIDC redirect URIs follow the request scheme.
///
/// SEC (2026-09-05 audit): the previous default unconditionally cleared
/// <c>ForwardedHeadersOptions.KnownProxies</c>/<c>KnownIPNetworks</c> for every host installing
/// the Prism package, making <c>HttpContext.Connection.RemoteIpAddress</c> entirely client-controlled
/// (<c>X-Forwarded-For</c> is trivially spoofable), which defeated the anonymous
/// biometric-exchange IP rate limiter (<c>BiometricController.GetClientIp()</c>) — an attacker
/// rotating the header got an unbounded number of rate-limit partitions. A deployment now opts in
/// to trusting specific proxy addresses/networks explicitly; ASP.NET Core's own loopback-only
/// default applies until it does.
/// </summary>
public class ForwardedHeadersTrustOptions
{
    public const string SectionName = "TestSite:ForwardedHeaders";

    /// <summary>
    /// IP addresses of proxies trusted to supply forwarded headers (e.g. an internal load
    /// balancer's address). Empty by default — ASP.NET Core's built-in loopback-only trust
    /// applies until a deployment names its real proxy addresses here.
    /// </summary>
    public IReadOnlyList<string> KnownProxies { get; set; } = [];

    /// <summary>
    /// CIDR network ranges trusted to supply forwarded headers (e.g. <c>"10.0.0.0/8"</c> for an
    /// internal subnet all of whose addresses may proxy for this site). Empty by default: see
    /// <see cref="KnownProxies"/>.
    /// </summary>
    public IReadOnlyList<string> KnownNetworks { get; set; } = [];

    /// <summary>
    /// Applies this configuration to a real <see cref="ForwardedHeadersOptions"/>, shared by
    /// <see cref="ForwardedHeadersRegistration"/> and the tests, so a test
    /// exercises the exact logic that runs at startup rather than a re-derivation of it. Invalid
    /// entries (an address or CIDR range that doesn't parse) are skipped rather than throwing:
    /// a malformed deployment config should fail safe (loopback-only trust preserved for that
    /// entry), not take the whole site down at startup.
    /// </summary>
    public void ApplyTo(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        foreach (var proxy in KnownProxies)
        {
            if (System.Net.IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (var network in KnownNetworks)
        {
            if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
            {
                options.KnownIPNetworks.Add(ipNetwork);
            }
        }
    }
}
