using FluentAssertions;
using UmbracoPrism.Core.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.Services.Sanitization;

/// <summary>
/// Regression tests for SEC-PT2-007: Accordion <c>Content</c> field sanitization.
/// The Accordion partial now routes <c>accordionSection.Content</c> through
/// <see cref="ServiceContentSanitizer"/> before passing it to <c>@Html.Raw</c>.
/// These tests prove that a hostile payload cannot survive that boundary.
/// </summary>
public class AccordionContentSanitizationTests
{
    private readonly ServiceContentSanitizer _sut = new();

    [Fact]
    public void Sanitize_AccordionContent_ScriptTag_IsStripped()
    {
        // Simulates a producer (present or future) populating accordionSection.Content
        // with an injected <script> payload.
        const string hostile = "<p>Accordion body text</p><script>alert('xss')</script>";

        var result = _sut.Sanitize(hostile);

        result.Should().NotContain("<script",
            because: "script tags must be stripped before reaching @Html.Raw in the Accordion partial");
        result.Should().NotContain("alert(",
            because: "script payload content must not survive sanitization");
        result.Should().Contain("<p>Accordion body text</p>",
            because: "legitimate paragraph content must pass through intact");
    }

    [Fact]
    public void Sanitize_AccordionContent_OnerrorAttribute_IsStripped()
    {
        // img is not in the allowlist — the whole element (including onerror) is removed.
        const string hostile = "<img src=\"x\" onerror=\"alert(1)\">";

        var result = _sut.Sanitize(hostile);

        result.Should().NotContain("onerror",
            because: "onerror event handler must be stripped; img is not on the allowlist");
        result.Should().NotContain("<img",
            because: "img tag itself is not on the GDS allowlist");
    }

    [Fact]
    public void Sanitize_AccordionContent_InlineEventOnAllowedTag_IsStripped()
    {
        // onclick on an <a> is globally blocked even though <a> itself is allowed.
        const string hostile = "<a href=\"https://gov.uk\" onclick=\"alert(1)\">link</a>";

        var result = _sut.Sanitize(hostile);

        result.Should().NotContain("onclick",
            because: "event handlers are never in the attribute allowlist");
        result.Should().Contain("https://gov.uk",
            because: "the safe href must still be preserved");
    }

    [Fact]
    public void Sanitize_AccordionContent_LegitimateRichText_PassesThroughIntact()
    {
        // Validates that real-world accordion content (headings, lists, links) is not broken.
        const string legitimate = "<h3>Section heading</h3><p>Body text with a <a href=\"https://gov.uk\">link</a>.</p><ul><li>Item one</li><li>Item two</li></ul>";

        var result = _sut.Sanitize(legitimate);

        result.Should().Contain("<h3>Section heading</h3>")
            .And.Contain("<p>Body text with a")
            .And.Contain("https://gov.uk")
            .And.Contain("<ul><li>Item one</li><li>Item two</li></ul>");
    }
}
