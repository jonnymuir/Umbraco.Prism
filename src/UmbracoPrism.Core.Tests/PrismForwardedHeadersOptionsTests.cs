using FluentAssertions;
using Microsoft.AspNetCore.HttpOverrides;
using UmbracoPrism.Core.Configuration;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// SECURITY REGRESSION: <see cref="PrismComposer"/> used to unconditionally clear
/// <see cref="ForwardedHeadersOptions.KnownProxies"/>/<see cref="ForwardedHeadersOptions.KnownIPNetworks"/>
/// for every host installing this package, making <c>HttpContext.Connection.RemoteIpAddress</c>
/// entirely attacker-controlled via a spoofed <c>X-Forwarded-For</c> header — which defeated the
/// anonymous biometric-exchange IP rate limiter. <see cref="PrismForwardedHeadersOptions.ApplyTo"/>
/// is the exact logic <see cref="PrismComposer"/> now wires into DI; these tests exercise it
/// directly rather than a re-derivation of it.
/// </summary>
public sealed class PrismForwardedHeadersOptionsTests
{
    [Fact]
    public void ApplyTo_PreservesTheFrameworksLoopbackOnlyTrust_WhenNoProxiesAreConfigured()
    {
        var options = new ForwardedHeadersOptions();
        var defaultKnownProxyCount = options.KnownProxies.Count;
        var defaultKnownNetworkCount = options.KnownIPNetworks.Count;

        new PrismForwardedHeadersOptions().ApplyTo(options);

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

        new PrismForwardedHeadersOptions { KnownProxies = ["10.0.0.5"] }.ApplyTo(options);

        options.KnownProxies.Should().Contain(System.Net.IPAddress.Parse("10.0.0.5"));
    }

    [Fact]
    public void ApplyTo_AddsTheConfiguredNetworkRange()
    {
        var options = new ForwardedHeadersOptions();

        new PrismForwardedHeadersOptions { KnownNetworks = ["10.0.0.0/8"] }.ApplyTo(options);

        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    }

    [Fact]
    public void ApplyTo_SkipsAnUnparsableProxyOrNetwork_RatherThanThrowing()
    {
        var options = new ForwardedHeadersOptions();

        var act = () => new PrismForwardedHeadersOptions
        {
            KnownProxies = ["not-an-ip-address"],
            KnownNetworks = ["not-a-cidr-range"],
        }.ApplyTo(options);

        act.Should().NotThrow("a malformed deployment config must fail safe, not take the whole site down at startup");
    }
}
