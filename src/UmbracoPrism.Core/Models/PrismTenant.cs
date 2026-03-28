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

    /// <summary>
    /// Gets or sets the Entra Tenant ID for the tenant.
    /// </summary>
    public string? EntraTenantId { get; set; }

    /// <summary>
    /// Gets or sets the Entra Client ID for the tenant.
    /// </summary>
    public string? EntraClientId { get; set; }
    
    /// <summary>
    /// This is NOT the secret itself. It is the NAME of the secret in Azure Key Vault.
    /// Example: "Prism-TenantA-Secret"
    /// </summary>
    public string? SecretKeyName { get; set; }

    /// <summary>
    /// Gets or sets the branding overrides for the tenant.
    /// </summary>
    public Dictionary<string, string> BrandingOverrides { get; set; } = new();

    /// <summary>
    /// Gets or sets the mobile branding overrides for the tenant.
    /// </summary>
    public Dictionary<string, string> MobileBrandingOverrides { get; set; } = new();

    /// <summary>
    /// Gets or sets normalized desktop branding CSS declarations precomputed for request-time injection.
    /// </summary>
    public string BrandingCssDeclarations { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets normalized mobile branding CSS declarations precomputed for request-time injection.
    /// </summary>
    public string MobileBrandingCssDeclarations { get; set; } = string.Empty;
}
