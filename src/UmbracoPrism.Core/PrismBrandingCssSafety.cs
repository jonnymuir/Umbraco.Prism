using System.Text.RegularExpressions;

namespace UmbracoPrism.Core;

/// <summary>
/// Validates tenant-admin-supplied branding override name/value pairs before they are
/// concatenated into a CSS declaration string that gets rendered, unescaped, inside a
/// <c>&lt;style&gt;</c> tag on every page a tenant serves (see <c>PrismBrandingMiddleware</c>
/// and <c>TenantService.BuildCssDeclarations</c>). A value containing <c>&lt;/style&gt;</c>
/// would terminate that element early and let subsequent "CSS" render as live HTML/script —
/// this is the only thing standing between a compromised or malicious backoffice admin account
/// and stored, tenant-wide script injection, since branding overrides are otherwise unvalidated
/// free text. Both callers must reject the same pair the same way; this is the single place
/// that rule lives.
/// </summary>
public static partial class PrismBrandingCssSafety
{
    private const int MaxLength = 200;

    [GeneratedRegex(@"^--[a-zA-Z][a-zA-Z0-9-]*$")]
    private static partial Regex PropertyNamePattern();

    [GeneratedRegex(@"^[a-zA-Z0-9#%.,()'_\- ]+$")]
    private static partial Regex ValuePattern();

    /// <summary>
    /// True when <paramref name="name"/> is a well-formed CSS custom property name
    /// (e.g. <c>--prism-color-primary</c>) with no characters capable of breaking out of a
    /// CSS declaration or the surrounding <c>&lt;style&gt;</c> element.
    /// </summary>
    public static bool IsSafePropertyName(string name) =>
        name.Length <= MaxLength && PropertyNamePattern().IsMatch(name);

    /// <summary>
    /// True when <paramref name="value"/> contains only characters that appear in legitimate
    /// CSS values (colors, sizes, font stacks) — no angle brackets, quotes, semicolons, braces,
    /// or slashes, so no CSS-comment or markup escape is possible.
    /// </summary>
    public static bool IsSafeValue(string value) =>
        value.Length <= MaxLength && ValuePattern().IsMatch(value);
}
