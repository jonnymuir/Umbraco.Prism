using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Configuration;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// The security headers the reference host really configures (its <c>appsettings.json</c>), checked as the policy
/// they produce. A directive Prism does not emit is created, not extended: it replaces the <c>default-src</c> fallback,
/// so one that lists only a third-party origin silently stops the page reaching its own. That broke the dashboard's API
/// call in CI once, so the reference host's own configuration is held to it.
/// </summary>
public class TestSiteSecurityHeadersTests
{
    private static string BuildPolicy()
    {
        var options = new ConfigurationBuilder()
            .SetBasePath(TestSupportSystems.FindTestSiteDirectory())
            .AddJsonFile("appsettings.json")
            .Build()
            .GetSection("Prism:SecurityHeaders")
            .Get<PrismSecurityHeadersOptions>()!;

        return CspPolicyBuilder.WithAdditionalSources(options.ContentSecurityPolicy!, options.AdditionalContentSecurityPolicySources);
    }

    private static string Directive(string policy, string name) =>
        policy.Split(';', StringSplitOptions.TrimEntries).Single(directive => directive.StartsWith(name + " ", StringComparison.Ordinal));

    [Fact]
    public void TheMapsPlaceSearch_IsAllowedWithoutBlockingTheSitesOwnRequests()
    {
        var connect = Directive(BuildPolicy(), "connect-src");

        connect.Should().Contain("https://nominatim.openstreetmap.org", "the picker's place search calls it from the browser");
        connect.Should().Contain("'self'", "a connect-src without it stops the page's own API calls");
    }

    [Fact]
    public void TheMapTiles_AreAllowedAlongsideTheSitesOwnImages()
    {
        var images = Directive(BuildPolicy(), "img-src");

        images.Should().Contain("https://tile.openstreetmap.org");
        images.Should().Contain("'self'");
        images.Should().Contain("data:", "the photo preview is a data: image");
    }
}
