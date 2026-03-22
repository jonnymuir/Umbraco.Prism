namespace UmbracoPrism.Core.Models.Branding;

/// <summary>
/// Groups related branding variables into a tab for editing and display.
/// </summary>
public class PrismBrandingTab
{
    /// <summary>
    /// Gets or sets the display label for the branding tab.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the branding variables associated with this tab.
    /// </summary>
    public List<PrismBrandingVariable> Variables { get; set; } = new();
}
