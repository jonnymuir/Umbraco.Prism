using UmbracoPrism.Core.Models.Branding;

namespace UmbracoPrism.Core.Services;

public interface IBrandingService
{
    IReadOnlyList<PrismBrandingTab> GetBrandingTabs();
    IReadOnlyList<PrismBrandingTab> GetBrandingTabsWithOverrides(IReadOnlyDictionary<string, string> overrides);
}
