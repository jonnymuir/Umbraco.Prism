using System.Text.RegularExpressions;

namespace UmbracoPrism.Core;

/// <summary>
/// Validates tenant-admin-supplied branding override name/value pairs before they are
/// concatenated into a CSS declaration string that gets rendered, unescaped, as the body of the
/// tenant's own <c>text/css</c> branding response (see
/// <c>PrismBrandingCssBuilder</c>/<c>PrismBrandingAssetsController</c> and
/// <c>TenantService.BuildCssDeclarations</c>). Branding overrides are otherwise unvalidated free
/// text, so a compromised or malicious backoffice admin account could otherwise corrupt the
/// tenant's live stylesheet (e.g. a raw CSS comment breakout affecting rules that follow) —
/// serving branding CSS as its own resource, rather than splicing it inline into a
/// <c>&lt;style&gt;</c> tag on every page as it used to be, already closes off the more severe
/// "escape into surrounding HTML/script" class of attack that motivated this originally, but the
/// validation itself remains the single source of truth both callers must apply the same way.
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
