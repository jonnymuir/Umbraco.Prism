namespace UmbracoPrism.Core.Models.Branding;

public class PrismBrandingTab
{
    public string Label { get; set; } = string.Empty;
    public List<PrismBrandingVariable> Variables { get; set; } = new();
}
