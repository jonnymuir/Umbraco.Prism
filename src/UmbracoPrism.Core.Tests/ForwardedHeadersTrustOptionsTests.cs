using AwesomeAssertions;
using Microsoft.AspNetCore.HttpOverrides;
using UmbracoPrism.TestSite.Security;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// SECURITY REGRESSION: Prism's composer used to unconditionally clear
/// <see cref="ForwardedHeadersOptions.KnownProxies"/>/<see cref="ForwardedHeadersOptions.KnownIPNetworks"/>
/// for every host installing this package, making <c>HttpContext.Connection.RemoteIpAddress</c>
/// entirely attacker-controlled via a spoofed <c>X-Forwarded-For</c> header, which defeated the
/// anonymous biometric-exchange IP rate limiter. <see cref="ForwardedHeadersTrustOptions.ApplyTo"/>
/// is the exact logic the reference site's ForwardedHeadersRegistration wires into DI; these tests exercise it
/// directly rather than a re-derivation of it.
/// </summary>
public sealed class ForwardedHeadersTrustOptionsTests
{
    [Fact]
    public void ApplyTo_PreservesTheFrameworksLoopbackOnlyTrust_WhenNoProxiesAreConfigured()
    {
        var options = new ForwardedHeadersOptions();
        var defaultKnownProxyCount = options.KnownProxies.Count;
        var defaultKnownNetworkCount = options.KnownIPNetworks.Count;

        new ForwardedHeadersTrustOptions().ApplyTo(options);

        options.KnownProxies.Should().HaveCount(defaultKnownProxyCount,
            "an unconfigured deployment must keep ASP.NET Core's own loopback-only trust, not an empty (trust-everyone) allowlist");
        options.KnownIPNetworks.Should().HaveCount(defaultKnownNetworkCount,
            "an unconfigured deployment must keep ASP.NET Core's own loopback-only trust, not an empty (trust-everyone) allowlist");
        options.KnownIPNetworks.Should().NotBeEmpty(
            "the previous defect cleared this to empty, which trusts every caller's X-Forwarded-For unconditionally");
    }

    [Fact]
    public void ApplyTo_AddsTheConfiguredProxyAddress()
    {
        var options = new ForwardedHeadersOptions();

        new ForwardedHeadersTrustOptions { KnownProxies = ["10.0.0.5"] }.ApplyTo(options);

        options.KnownProxies.Should().Contain(System.Net.IPAddress.Parse("10.0.0.5"));
    }

    [Fact]
    public void ApplyTo_AddsTheConfiguredNetworkRange()
    {
        var options = new ForwardedHeadersOptions();

        new ForwardedHeadersTrustOptions { KnownNetworks = ["10.0.0.0/8"] }.ApplyTo(options);

        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    }

    [Fact]
    public void ApplyTo_SkipsAnUnparsableProxyOrNetwork_RatherThanThrowing()
    {
        var options = new ForwardedHeadersOptions();

        var act = () => new ForwardedHeadersTrustOptions
        {
            KnownProxies = ["not-an-ip-address"],
            KnownNetworks = ["not-a-cidr-range"],
        }.ApplyTo(options);

        act.Should().NotThrow("a malformed deployment config must fail safe, not take the whole site down at startup");
    }
}
