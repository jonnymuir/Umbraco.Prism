namespace UmbracoPrism.Core.Controllers.Models;

public class PrismTenantRequest
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string? EntraTenantId { get; set; }
    public string? EntraClientId { get; set; }
    public string? SecretKeyName { get; set; }
    public string? OidcAuthority { get; set; }
    public string? OidcClientId { get; set; }
    public string? OidcClientSecret { get; set; }
    public Dictionary<string, string>? BrandingOverrides { get; set; }
    public Dictionary<string, string>? MobileBrandingOverrides { get; set; }
    public PrismMobileAppConfig? MobileAppConfig { get; set; }
    public bool AllowBiometricLogin { get; set; } = true;
}
