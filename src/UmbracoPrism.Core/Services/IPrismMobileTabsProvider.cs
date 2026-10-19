using Microsoft.AspNetCore.Http;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Supplies the tabs a generated Prism app shows in its native tab bar. Prism does not own any
/// content of the host site, so the host implements this from wherever its editors maintain
/// navigation, and registers it in place of the default single Home tab.
/// </summary>
public interface IPrismMobileTabsProvider
{
    /// <summary>
    /// The tabs for this request. <paramref name="context"/> carries the resolved tenant and, when
    /// a member is signed in, their identity, so tabs the member cannot use can be left out.
    /// </summary>
    Task<IReadOnlyList<PrismMobileTab>> GetTabsAsync(HttpContext context, CancellationToken cancellationToken);
}

/// <param name="Label">Shown under the tab's icon.</param>
/// <param name="Path">Site-relative path the tab opens, for example <c>/dashboard</c>.</param>
/// <param name="Icon">
/// A built-in icon name (<c>home</c>, <c>dashboard</c>, <c>account</c>, <c>settings</c>,
/// <c>transactions</c>, <c>notifications</c>, <c>more</c>) or the URL of an image, such as a media
/// item. Null shows a generic icon.
/// </param>
public sealed record PrismMobileTab(string Label, string Path, string? Icon = null);

internal sealed class PrismDefaultMobileTabsProvider : IPrismMobileTabsProvider
{
    private static readonly IReadOnlyList<PrismMobileTab> HomeOnly = [new("Home", "/", "home")];

    public Task<IReadOnlyList<PrismMobileTab>> GetTabsAsync(HttpContext context, CancellationToken cancellationToken)
        => Task.FromResult(HomeOnly);
}
