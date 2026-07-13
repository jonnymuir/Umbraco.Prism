# SKILL: Ganss.Xss GDS-Style HTML Allowlist Configuration

**Domain:** Security / HTML Sanitization
**Author:** Copper
**Date:** 2026-04-30

---

## Summary

Pattern for configuring `Ganss.Xss.HtmlSanitizer` (v9.x) with a strict, GDS-aligned content allowlist for rendering operator-authored HTML via `@Html.Raw`. Applicable to any system where admins or workflow authors can provide rich-text content that will be rendered unencoded.

---

## When to use

- A `@Html.Raw(...)` or equivalent unencoded render path exists
- Content originates from operator input (CMS, admin editor, workflow definitions, API import)
- A specific, auditable set of safe tags is known (e.g. GDS or WCAG-aligned authoring guide)
- Encoding the content would break legitimate rich text (links, lists, headings)

---

## Core configuration pattern

```csharp
using Ganss.Xss;

internal sealed class WorkflowContentSanitizer : IWorkflowContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public WorkflowContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        // 1. Allowlisted tags only — clear defaults, set explicitly
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "ul", "ol", "li", "blockquote", "br",
                                     "h2", "h3", "h4", "strong", "em", "b", "i",
                                     "code", "abbr", "span", "a" })
            _sanitizer.AllowedTags.Add(tag);

        // 2. No globally allowed attributes — per-tag exceptions via RemovingAttribute
        _sanitizer.AllowedAttributes.Clear();

        // 3. Allowed URI schemes (belt-and-suspenders; primary check is in the event handler)
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("tel");

        // 4. No inline CSS
        _sanitizer.AllowedCssProperties.Clear();

        // 5. Per-tag attribute exceptions
        _sanitizer.RemovingAttribute += OnRemovingAttribute;
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var sanitized = _sanitizer.Sanitize(html);
        return AddExternalLinkAttributes(sanitized);
    }

    private static void OnRemovingAttribute(object? sender, RemovingAttributeEventArgs e)
    {
        var tag = e.Tag.TagName; // AngleSharp uppercases tag names
        var attr = e.Attribute.Name.ToLowerInvariant();

        switch (attr)
        {
            case "href" when tag == "A":
                if (IsAllowedHrefScheme(e.Attribute.Value)) e.Cancel = true;
                break;
            case "rel" when tag == "A":
                e.Cancel = true;
                break;
            case "title" when tag == "ABBR":
                e.Cancel = true;
                break;
        }
    }

    private static bool IsAllowedHrefScheme(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.AsSpan().TrimStart();
        return v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
    }

    // Post-sanitization: inject rel + target on external http(s) links
    private static readonly Regex ExternalAnchorPattern = new(
        @"<a\b[^>]*\bhref=""(https?://[^""]+)""[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static string AddExternalLinkAttributes(string sanitized) =>
        ExternalAnchorPattern.Replace(sanitized,
            m => $"<a href=\"{m.Groups[1].Value}\" rel=\"noopener noreferrer\" target=\"_blank\">");
}
```

---

## Key design decisions

### 1. Empty `AllowedAttributes` + `RemovingAttribute` for per-tag enforcement

Ganss.Xss applies `AllowedSchemes` only to attributes that survive the `AllowedAttributes` gate. If `href` is in `AllowedAttributes`, it's globally allowed (including on `<div>`). To achieve strict per-tag restriction, clear `AllowedAttributes` and use the `RemovingAttribute` event with `Cancel = true` to selectively keep attributes on specific tags.

The `RemovingAttribute` event fires with `RemoveReason.NotAllowedAttribute` for attributes not in `AllowedAttributes`. Setting `Cancel = true` keeps the attribute. Perform your own scheme check in the handler since `AllowedSchemes` isn't applied at this stage.

### 2. Singleton registration (thread safety)

`HtmlSanitizer` is thread-safe for concurrent `Sanitize()` calls **when its configuration is not mutated after construction**. Wire all event handlers in the constructor; never mutate configuration after the instance is used. Register as singleton in DI.

### 3. Post-processing for `rel` + `target` injection

Ganss.Xss cannot add attributes — only remove them. To enforce `rel="noopener noreferrer"` and `target="_blank"` on external links, apply a compiled Regex replacement on the already-sanitized output. Since the input to the regex is guaranteed sanitized, this is safe.

### 4. HTML comments

Ganss.Xss removes HTML comments by default (`RemovingCommentEventArgs` default `Cancel = false`). No additional configuration needed.

### 5. Idempotency

The pattern produces idempotent output because:
- After a first pass, only allowlisted tags and attributes remain
- The `target` attribute (added by post-processing) is not in `AllowedAttributes`, so a second Ganss.Xss pass strips it; then post-processing re-adds it with identical values
- Net result: `Sanitize(Sanitize(x)) == Sanitize(x)`

---

## DI registration

```csharp
// Singleton: one HtmlSanitizer instance, thread-safe for concurrent Sanitize calls
services.AddSingleton<IWorkflowContentSanitizer, WorkflowContentSanitizer>();
```

---

## Testing checklist

For each allowlist you configure, test:
- [ ] Each allowed tag round-trips intact
- [ ] `href` with allowed schemes preserved; with `javascript:`, `data:`, `vbscript:`, `file:`, `//` stripped
- [ ] All `on*` event handlers stripped from all tags
- [ ] `<script>`, `<style>`, `<iframe>`, `<svg>`, `<math>` dropped
- [ ] `class`, `id`, `style` attributes stripped
- [ ] Plain text passthrough unchanged
- [ ] `null` and whitespace inputs return `string.Empty`
- [ ] Idempotency: `Sanitize(Sanitize(x)) == Sanitize(x)`
- [ ] External http(s) links have `rel="noopener noreferrer"` and `target="_blank"`

---

## Reference implementation

`src/UmbracoPrism.Core/Services/Sanitization/WorkflowContentSanitizer.cs` (commit `ae616a2`)

Tests: `src/UmbracoPrism.Core.Tests/Services/Sanitization/WorkflowContentSanitizerTests.cs`
