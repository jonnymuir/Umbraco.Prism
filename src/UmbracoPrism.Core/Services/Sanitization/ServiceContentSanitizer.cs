using System.Text.RegularExpressions;
using Ganss.Xss;
using Wayfinder.Services.Sanitization;

namespace UmbracoPrism.Core.Services.Sanitization;

/// <summary>
/// HTML sanitizer for workflow definition content, backed by Ganss.Xss.
/// Implements the GDS-aligned allowlist defined in SEC-003 §4.3.
/// Register as singleton — HtmlSanitizer is thread-safe for concurrent Sanitize calls
/// when its configuration is not mutated after construction.
/// </summary>
internal sealed class ServiceContentSanitizer : IServiceContentSanitizer
{
    // GDS allowlist §4.3 — block-level and inline tags only.
    // No <div>, <table>, <form>, <input>, <img>, <video>, <svg>, <math>, <script>, <style>, <iframe>, etc.
    private static readonly HashSet<string> AllowedTagNames =
    [
        // Block
        "p", "ul", "ol", "li", "blockquote", "br", "h2", "h3", "h4",
        // Inline
        "strong", "em", "b", "i", "code", "abbr", "span", "a",
    ];

    // Matches <a> opening tags whose href is an external http/https URL.
    // Used after sanitization to inject rel + target for opener/referrer hardening.
    // The pattern is safe at this point because Ganss.Xss has already validated the HTML.
    private static readonly Regex ExternalAnchorPattern = new(
        @"<a\b[^>]*\bhref=""(https?://[^""]+)""[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private readonly HtmlSanitizer _sanitizer;

    public ServiceContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        // ── Tags ──────────────────────────────────────────────────────────────
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in AllowedTagNames)
            _sanitizer.AllowedTags.Add(tag);

        // ── Attributes ────────────────────────────────────────────────────────
        // Start with an empty global allowlist; per-tag exceptions wired below via RemovingAttribute.
        // No class, id, style, data-*, title (except abbr), rel/href (except a).
        _sanitizer.AllowedAttributes.Clear();

        // ── URI schemes ───────────────────────────────────────────────────────
        // Populated as belt-and-suspenders; primary enforcement is the RemovingAttribute handler.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("tel");

        // ── CSS ───────────────────────────────────────────────────────────────
        // No inline styles in v1. If GDS authoring needs colour/spacing, it goes through
        // CSS classes — and we don't allow class either (see §4.3 rationale).
        _sanitizer.AllowedCssProperties.Clear();

        // ── Per-tag attribute exceptions ──────────────────────────────────────
        // RemovingAttribute fires for every attribute not in AllowedAttributes.
        // Cancel=true keeps the attribute; Cancel=false (default) removes it.
        _sanitizer.RemovingAttribute += OnRemovingAttribute;
    }

    /// <inheritdoc />
    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var sanitized = _sanitizer.Sanitize(html);
        return AddExternalLinkAttributes(sanitized);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void OnRemovingAttribute(object? sender, RemovingAttributeEventArgs e)
    {
        var tag = e.Tag.TagName; // AngleSharp uppercases tag names in the DOM
        var attr = e.Attribute.Name.ToLowerInvariant();

        switch (attr)
        {
            case "href" when tag == "A":
                // Keep href on <a> only when the scheme is in our allowlist.
                // We perform the scheme check here rather than relying solely on AllowedSchemes
                // because RemovingAttribute fires with Reason=NotAllowedAttribute (not NotAllowedValue)
                // when the attribute is absent from AllowedAttributes — the scheme check in the
                // Ganss.Xss pipeline is only applied to attributes that survive the AllowedAttributes gate.
                if (IsAllowedHrefScheme(e.Attribute.Value))
                    e.Cancel = true;
                break;

            case "rel" when tag == "A":
                // Author-provided rel is kept and then overridden by post-processing for external links.
                e.Cancel = true;
                break;

            case "title" when tag == "ABBR":
                // title is meaningful on <abbr> (provides expanded form for screen readers).
                e.Cancel = true;
                break;
        }
    }

    /// <summary>
    /// Allows only the four URI schemes listed in §4.3.
    /// Trims whitespace before comparison to defeat "  javascript:" bypass attempts.
    /// </summary>
    private static bool IsAllowedHrefScheme(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.AsSpan().TrimStart();
        return v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Post-sanitization step: rewrites every external http(s) anchor to add
    /// <c>rel="noopener noreferrer"</c> and <c>target="_blank"</c>.
    /// Any author-supplied rel or target is discarded in favour of the hardened values.
    /// </summary>
    private static string AddExternalLinkAttributes(string sanitized)
    {
        return ExternalAnchorPattern.Replace(
            sanitized,
            m => $"<a href=\"{m.Groups[1].Value}\" rel=\"noopener noreferrer\" target=\"_blank\">");
    }
}
