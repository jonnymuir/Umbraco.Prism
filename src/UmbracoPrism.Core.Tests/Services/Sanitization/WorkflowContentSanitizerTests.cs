using FluentAssertions;
using UmbracoPrism.Core.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.Services.Sanitization;

/// <summary>
/// Unit tests for <see cref="WorkflowContentSanitizer"/> — GDS-aligned allowlist, SEC-003 T8.
/// Validates that the allowlist is correctly applied: allowed markup round-trips intact,
/// disallowed markup and dangerous vectors are stripped, and edge-case inputs are handled gracefully.
/// </summary>
public class WorkflowContentSanitizerTests
{
    private readonly WorkflowContentSanitizer _sut = new();

    // ── Null / whitespace ─────────────────────────────────────────────────

    [Fact]
    public void Sanitize_NullInput_ReturnsEmpty()
    {
        _sut.Sanitize(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Sanitize_WhitespaceInput_ReturnsEmpty(string input)
    {
        _sut.Sanitize(input).Should().BeEmpty();
    }

    // ── Plain text ────────────────────────────────────────────────────────

    [Fact]
    public void Sanitize_PlainText_RoundTripsUnchanged()
    {
        const string plain = "Hello, this is plain text with no HTML.";
        _sut.Sanitize(plain).Should().Be(plain);
    }

    // ── Allowed tags round-trip ───────────────────────────────────────────

    [Theory]
    [InlineData("<p>paragraph</p>")]
    [InlineData("<ul><li>item</li></ul>")]
    [InlineData("<ol><li>item</li></ol>")]
    [InlineData("<blockquote>quoted</blockquote>")]
    [InlineData("<h2>Heading 2</h2>")]
    [InlineData("<h3>Heading 3</h3>")]
    [InlineData("<h4>Heading 4</h4>")]
    [InlineData("<strong>bold</strong>")]
    [InlineData("<em>italic</em>")]
    [InlineData("<b>bold</b>")]
    [InlineData("<i>italic</i>")]
    [InlineData("<code>code</code>")]
    [InlineData("<span>span</span>")]
    public void Sanitize_AllowedTag_IsPreserved(string html)
    {
        _sut.Sanitize(html).Should().Be(html,
            because: $"the tag in '{html}' is on the GDS allowlist and must round-trip intact");
    }

    [Fact]
    public void Sanitize_AllowedStrongAndEmInsideParagraph_PreservesMarkup()
    {
        const string html = "<p><strong>Important</strong> <em>note</em></p>";
        var result = _sut.Sanitize(html);

        result.Should().Contain("<p>")
            .And.Contain("<strong>Important</strong>")
            .And.Contain("<em>note</em>");
    }

    // ── External links — rel + target injection ───────────────────────────

    [Fact]
    public void Sanitize_HttpsLink_PreservesHrefAndInjectsRelAndTarget()
    {
        const string html = "<p>See <a href=\"https://gov.uk\">guidance</a>.</p>";
        var result = _sut.Sanitize(html);

        result.Should().Contain("<a href=\"https://gov.uk\"")
            .And.Contain("rel=\"noopener noreferrer\"")
            .And.Contain("target=\"_blank\"");
    }

    [Fact]
    public void Sanitize_MailtoLink_IsPreservedWithoutTargetOrRel()
    {
        const string html = "<a href=\"mailto:gov@example.com\">email us</a>";
        var result = _sut.Sanitize(html);

        result.Should().Contain("href=\"mailto:gov@example.com\"",
            because: "mailto: is on the allowed-scheme list");
        result.Should().NotContain("target=\"_blank\"",
            because: "rel/target injection only applies to http(s) links");
    }

    [Fact]
    public void Sanitize_TelLink_IsPreserved()
    {
        const string html = "<a href=\"tel:+441234567890\">call us</a>";
        var result = _sut.Sanitize(html);

        result.Should().Contain("href=\"tel:+441234567890\"",
            because: "tel: is on the allowed-scheme list");
    }

    // ── Dangerous href schemes stripped ──────────────────────────────────

    [Fact]
    public void Sanitize_JavascriptHref_IsStripped()
    {
        const string html = "<a href=\"javascript:alert(1)\">click</a>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("javascript:",
            because: "javascript: scheme is explicitly blocked by the allowlist");
    }

    [Fact]
    public void Sanitize_DataTextHtmlHref_IsStripped()
    {
        const string html = "<a href=\"data:text/html,<script>alert(1)</script>\">x</a>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("data:",
            because: "data: scheme is explicitly blocked by the allowlist");
    }

    [Fact]
    public void Sanitize_VbscriptHref_IsStripped()
    {
        const string html = "<a href=\"vbscript:msgbox(1)\">x</a>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("vbscript:",
            because: "vbscript: scheme is explicitly blocked");
    }

    [Fact]
    public void Sanitize_ProtocolRelativeHref_IsStripped()
    {
        // Protocol-relative URLs can be used to redirect to attacker-controlled origins
        const string html = "<a href=\"//evil.com\">x</a>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("//evil.com",
            because: "protocol-relative URLs bypass the scheme check and are blocked");
    }

    // ── Event handlers stripped ───────────────────────────────────────────

    [Fact]
    public void Sanitize_OnclickOnAllowedTag_IsStripped()
    {
        const string html = "<a href=\"https://gov.uk\" onclick=\"alert(1)\">x</a>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("onclick",
            because: "event handlers (on*) are never in the attribute allowlist");
        result.Should().Contain("https://gov.uk",
            because: "the safe href must still be preserved");
    }

    [Fact]
    public void Sanitize_OnerrorOnImgTag_IsStripped()
    {
        // <img> is not in the tag allowlist — the entire element (including onerror) is removed
        const string html = "<img src=\"x\" onerror=\"alert(1)\">";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("onerror",
            because: "img is not in the tag allowlist; the whole element is removed");
    }

    // ── Disallowed tags stripped ──────────────────────────────────────────

    [Fact]
    public void Sanitize_ScriptTag_IsStripped_WhileEnclosingParagraphIsKept()
    {
        const string html = "<p>ok<script>alert(1)</script></p>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("<script",
            because: "script is not in the tag allowlist");
        result.Should().Contain("<p>ok</p>",
            because: "the enclosing <p> is on the allowlist and its safe text must be preserved");
    }

    [Fact]
    public void Sanitize_StyleBlock_IsStripped_WhileParagraphIsKept()
    {
        const string html = "<style>body{display:none}</style><p>ok</p>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("<style",
            because: "style is not in the tag allowlist");
        result.Should().Contain("<p>ok</p>");
    }

    [Theory]
    [InlineData("<iframe src=\"https://evil.com\"></iframe>", "iframe")]
    [InlineData("<object data=\"evil.swf\"></object>", "object")]
    [InlineData("<embed src=\"evil.swf\">", "embed")]
    [InlineData("<math><mtext>x</mtext></math>", "math")]
    public void Sanitize_DisallowedTag_IsStripped(string html, string tag)
    {
        var result = _sut.Sanitize(html);
        result.Should().NotContain($"<{tag}",
            because: $"{tag} is not in the GDS tag allowlist");
    }

    [Fact]
    public void Sanitize_SvgWithOnload_IsStripped()
    {
        const string html = "<svg onload=\"alert(1)\"><circle/></svg><p>text</p>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("<svg",
            because: "svg is not in the tag allowlist");
        result.Should().NotContain("onload",
            because: "event handlers on any element must be stripped");
        result.Should().Contain("<p>text</p>",
            because: "the safe sibling <p> must be preserved");
    }

    // ── Inline style stripped ─────────────────────────────────────────────

    [Fact]
    public void Sanitize_InlineStyleAttribute_IsStripped()
    {
        const string html = "<p style=\"color:red\">x</p>";
        var result = _sut.Sanitize(html);

        result.Should().NotContain("style=",
            because: "the style attribute is not in the attribute allowlist (no inline styles in v1)");
        result.Should().Contain("<p>x</p>");
    }

    // ── Idempotency ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("<p>Hello <strong>world</strong></p>")]
    [InlineData("<a href=\"https://gov.uk\">link</a>")]
    [InlineData("<p>ok<script>alert(1)</script></p>")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\">")]
    [InlineData("plain text")]
    public void Sanitize_IsIdempotent(string html)
    {
        var once = _sut.Sanitize(html);
        var twice = _sut.Sanitize(once);

        twice.Should().Be(once,
            because: "sanitizing already-sanitized content must produce identical output");
    }
}
