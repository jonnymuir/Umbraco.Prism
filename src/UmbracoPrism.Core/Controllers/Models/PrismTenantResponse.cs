namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Management API response for tenant records.
/// </summary>
public class PrismTenantResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string? EntraTenantId { get; set; }
    public string? EntraClientId { get; set; }
    public string? SecretKeyName { get; set; }
    public string? OidcAuthority { get; set; }
    public string? OidcClientId { get; set; }
    public string? OidcClientSecretProvider { get; set; }
    public bool HasOidcClientSecret { get; set; }
    public string? BrandingOverrides { get; set; }
    public string? MobileBrandingOverrides { get; set; }
    public string? MobileAppConfig { get; set; }
    public bool AllowBiometricLogin { get; set; }
}
