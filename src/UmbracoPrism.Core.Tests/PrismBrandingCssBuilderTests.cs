using FluentAssertions;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Unit tests for the branding-CSS-building logic extracted from the old
/// <c>PrismBrandingMiddleware</c> (SEC-PT2-004 CSP follow-up — see
/// <c>PrismBrandingAssetsController</c>, which is the only real caller now). Pure-function
/// tests: no HTTP context, no tenant resolution, just the precedence/escaping rules.
/// </summary>
public class PrismBrandingCssBuilderTests
{
    [Fact]
    public void ReturnsEmptyString_WhenNoOverridesAreConfigured()
    {
        var css = PrismBrandingCssBuilder.BuildCssOverrides(null, null, null, null);

        css.Should().BeEmpty("a tenant with no branding configured should get a valid, empty CSS document — not a special case for callers");
    }

    [Fact]
    public void BuildsRootBlock_FromDesktopOverridesOnly()
    {
        var css = PrismBrandingCssBuilder.BuildCssOverrides(
            new Dictionary<string, string> { ["--prism-primary"] = "#0055ff" },
            null, null, null);

        css.Should().Be(":root{--prism-primary:#0055ff;}");
    }

    [Fact]
    public void AppendsMobileOverrides_AfterDesktopOverrides_SoMobileWins()
    {
        var css = PrismBrandingCssBuilder.BuildCssOverrides(
            new Dictionary<string, string> { ["--prism-primary"] = "#0055ff" },
            new Dictionary<string, string> { ["--prism-primary"] = "#003399" },
            null, null);

        var styleStart = css.IndexOf(":root{", StringComparison.Ordinal);
        var desktopIndex = css.IndexOf("--prism-primary:#0055ff;", StringComparison.Ordinal);
        var mobileIndex = css.IndexOf("--prism-primary:#003399;", StringComparison.Ordinal);

        styleStart.Should().Be(0);
        desktopIndex.Should().BeGreaterThan(-1);
        mobileIndex.Should().BeGreaterThan(desktopIndex, "later declarations of the same property win the CSS cascade");
    }

    [Fact]
    public void PrefersPrecomputedDeclarations_OverRawDictionaries_WhenBothPresent()
    {
        var css = PrismBrandingCssBuilder.BuildCssOverrides(
            new Dictionary<string, string> { ["--prism-primary"] = "#old" },
            new Dictionary<string, string> { ["--prism-primary"] = "#old-mobile" },
            "--prism-primary:#new;",
            "--prism-primary:#new-mobile;");

        css.Should().Be(":root{--prism-primary:#new;--prism-primary:#new-mobile;}");
    }

    [Fact]
    public void SkipsUnsafePropertyNamesAndValues()
    {
        var css = PrismBrandingCssBuilder.BuildCssOverrides(
            new Dictionary<string, string>
            {
                ["--prism-primary"] = "#0055ff",
                ["not-a-custom-property"] = "red",
                ["--prism-evil"] = "red}</style><script>alert(1)</script>"
            },
            null, null, null);

        css.Should().Be(":root{--prism-primary:#0055ff;}",
            "PrismBrandingCssSafety must reject anything that isn't a well-formed custom property / plain value, regardless of delivery mechanism");
    }

    [Fact]
    public void SkipsEmptyOrWhitespaceOnlyEntries()
    {
        var css = PrismBrandingCssBuilder.BuildCssOverrides(
            new Dictionary<string, string>
            {
                ["--prism-primary"] = "#0055ff",
                [" "] = "#000000",
                ["--prism-empty"] = "   "
            },
            null, null, null);

        css.Should().Be(":root{--prism-primary:#0055ff;}");
    }
}
