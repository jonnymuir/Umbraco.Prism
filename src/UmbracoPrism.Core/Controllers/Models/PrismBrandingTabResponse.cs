using UmbracoPrism.Core.Models.Branding;

namespace UmbracoPrism.Core.Controllers.Models;

public class PrismBrandingTabResponse
{
    public int TenantId { get; set; }
    public List<PrismBrandingTab> Tabs { get; set; } = new();
}
