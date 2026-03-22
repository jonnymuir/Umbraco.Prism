using UmbracoPrism.Core.Models.Branding;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Provides Prism branding variable metadata discovered from CSS and tenant override values.
/// </summary>
public interface IBrandingService
{
    /// <summary>
    /// Returns discovered branding tabs and default variable values.
    /// </summary>
    /// <returns>A read-only list of branding tabs keyed by source stylesheet.</returns>
    IReadOnlyList<PrismBrandingTab> GetBrandingTabs();

    /// <summary>
    /// Returns branding tabs with tenant-specific override values applied.
    /// </summary>
    /// <param name="overrides">Tenant override values keyed by CSS variable name.</param>
    /// <returns>A read-only list of branding tabs with default and override values.</returns>
    IReadOnlyList<PrismBrandingTab> GetBrandingTabsWithOverrides(IReadOnlyDictionary<string, string> overrides);
}
