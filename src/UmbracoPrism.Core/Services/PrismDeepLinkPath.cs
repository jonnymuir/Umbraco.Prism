using System.Diagnostics.CodeAnalysis;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Decides whether a notification's deep link is a path on the tenant's own site. The mobile app
/// navigates to the link when the notification is tapped, so anything else (another site, a
/// protocol-relative <c>//host</c>, a <c>javascript:</c> URL) would turn a notification into a
/// phishing link or script running in the member's signed-in session.
/// </summary>
public static class PrismDeepLinkPath
{
    /// <summary>
    /// True for a site-relative path such as <c>/orders?id=7#latest</c>: it starts with a single
    /// <c>/</c> and contains no backslash, whitespace or control character. Browsers treat
    /// <c>\</c> as <c>/</c> and strip tabs and newlines, so <c>/\host</c> and <c>/\t/host</c>
    /// would otherwise reach another site.
    /// </summary>
    public static bool IsSitePath([NotNullWhen(true)] string? value) =>
        value is ['/'] or ['/', not '/', ..] && !value.Any(IsUnsafe);

    private static bool IsUnsafe(char c) => c == '\\' || char.IsWhiteSpace(c) || char.IsControl(c);
}
