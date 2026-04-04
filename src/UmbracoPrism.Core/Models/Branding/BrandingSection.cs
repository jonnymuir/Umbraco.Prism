namespace UmbracoPrism.Core.Models.Branding;

/// <summary>
/// Represents a section grouping of branding variables for the tenant editor UI.
/// </summary>
public class BrandingSection
{
    /// <summary>
    /// Gets or sets the section name (e.g., "Brand Colours").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the variables in this section.
    /// </summary>
    public List<BrandingVariableMetadata> Variables { get; set; } = new();
}
