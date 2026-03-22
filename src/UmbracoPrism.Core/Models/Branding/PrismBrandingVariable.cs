namespace UmbracoPrism.Core.Models.Branding;

/// <summary>
/// Represents a CSS variable that can be branded per tenant.
/// </summary>
public class PrismBrandingVariable
{
    /// <summary>
    /// Gets or sets the CSS variable name (for example, <c>--prism-primary-color</c>).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default CSS value discovered from source stylesheets.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the tenant override value when configured.
    /// </summary>
    public string? OverrideValue { get; set; }
}
