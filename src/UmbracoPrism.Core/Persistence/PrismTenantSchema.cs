using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using NPoco;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the PrismTenant table.
/// </summary>
[TableName("prismTenants")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismTenantSchema
{
    /// <summary>
    /// Gets or sets the unique identifier for the tenant.
    /// </summary>
    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the tenant.
    /// </summary>
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hostname associated with the tenant.
    /// </summary>
    [Column("hostname")]
    [Index(IndexTypes.UniqueNonClustered)]
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Entra Tenant ID for the tenant.
    /// </summary>
    [Column("EntraTenantId")]
    [NullSetting(NullSetting = NullSettings.Null)] 
    public string? EntraTenantId { get; set; }

    /// <summary>
    /// Gets or sets the Entra Client ID for the tenant.
    /// </summary>
    [Column("EntraClientId")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? EntraClientId { get; set; }

    /// <summary>
    /// This is NOT the secret itself. It is the NAME of the secret in Azure Key Vault.
    /// Example: "Prism-TenantA-Secret"
    /// </summary>
    [Column("SecretKeyName")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? SecretKeyName { get; set; }

    /// <summary>
    /// Stores tenant-specific branding overrides as JSON.
    /// </summary>
    [Column("BrandingOverrides")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? BrandingOverrides { get; set; }

    /// <summary>
    /// Stores tenant-specific mobile branding overrides as JSON.
    /// </summary>
    [Column("MobileBrandingOverrides")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? MobileBrandingOverrides { get; set; }

    /// <summary>
    /// Stores tenant-specific mobile app generator settings as JSON.
    /// </summary>
    [Column("MobileAppConfig")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? MobileAppConfig { get; set; }

    /// <summary>
    /// Whether biometric login is enabled for this tenant (default: true).
    /// </summary>
    [Column("AllowBiometricLogin")]
    public bool AllowBiometricLogin { get; set; } = true;

    /// <summary>
    /// Gets or sets the generic OIDC authority URL for non-Entra providers.
    /// When set, this takes precedence over Entra-specific authority construction.
    /// Example: "http://localhost:8080/realms/prism-dev"
    /// </summary>
    [Column("OidcAuthority")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? OidcAuthority { get; set; }

    /// <summary>
    /// Gets or sets the OIDC client ID for non-Entra providers.
    /// Example: "prism-client"
    /// </summary>
    [Column("OidcClientId")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? OidcClientId { get; set; }

    /// <summary>
    /// Gets or sets the OIDC client secret for non-Entra providers.
    /// Note: For local dev only. Production should use environment variables.
    /// </summary>
    [Column("OidcClientSecret")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? OidcClientSecret { get; set; }
}