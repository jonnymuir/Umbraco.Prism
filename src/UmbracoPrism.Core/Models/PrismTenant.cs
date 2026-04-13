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
    /// Gets or sets the Entra Tenant ID for the tenant.
    /// </summary>
    public string? EntraTenantId { get; set; }

    /// <summary>
    /// Gets or sets the Entra Client ID for the tenant.
    /// </summary>
    public string? EntraClientId { get; set; }
    
    /// <summary>
    /// This is NOT the secret itself. It is the vault-backed secret reference used by Entra tenants.
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

    /// <summary>
    /// Whether biometric login is enabled for this tenant. Defaults to true.
    /// </summary>
    public bool AllowBiometricLogin { get; set; } = true;

    /// <summary>
    /// Gets or sets the generic OIDC authority URL for non-Entra providers.
    /// When set, this takes precedence over Entra-specific authority construction.
    /// Example: "http://localhost:8080/realms/prism-dev"
    /// </summary>
    public string? OidcAuthority { get; set; }

    /// <summary>
    /// Gets or sets the OIDC client ID for non-Entra providers.
    /// Example: "prism-client"
    /// </summary>
    public string? OidcClientId { get; set; }

    /// <summary>
    /// Gets or sets the provider used to resolve the generic OIDC client secret at runtime.
    /// Normal tenants should use <c>azure-key-vault</c>; the repo-owned localhost demo may use <c>inline</c>.
    /// </summary>
    public string? OidcClientSecretProvider { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific reference used to resolve the generic OIDC client secret at runtime.
    /// For Key Vault this is the secret name; for the localhost demo it is the repo-owned inline secret.
    /// </summary>
    public string? OidcClientSecretReference { get; set; }
}
