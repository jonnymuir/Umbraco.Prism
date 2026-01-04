namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a tenant in the Prism multi-tenant system.
/// </summary>
public class PrismTenant
{
    /// <summary>
    /// Gets or sets the unique identifier for the tenant.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the tenant.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the hostname associated with the tenant.
    /// </summary>
    public string Hostname { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the theme color for the tenant's branding.
    /// </summary>
    public string ThemeColor { get; set; } = "#3490dc";
    // Add other brand-specific properties here (Logo URL, etc.)
}
