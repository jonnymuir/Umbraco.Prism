using System.Text.Json;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Copies a tenant request's branding (light, mobile and dark) and mobile app settings onto the
/// stored tenant row, the same way for a new tenant and an edited one.
/// </summary>
internal static class TenantRequestSettings
{
    public static void ApplyTo(PrismTenantSchema schema, PrismTenantRequest request)
    {
        schema.BrandingOverrides = SerializeOverrides(request.BrandingOverrides);
        schema.MobileBrandingOverrides = SerializeOverrides(request.MobileBrandingOverrides);
        schema.DarkBrandingOverrides = SerializeOverrides(request.DarkBrandingOverrides);
        schema.MobileAppConfig = SerializeMobileAppConfig(request.MobileAppConfig);
        schema.AllowBiometricLogin = request.AllowBiometricLogin;
    }

    private static string? SerializeOverrides(Dictionary<string, string>? overrides) =>
        overrides is { Count: > 0 } ? JsonSerializer.Serialize(overrides) : null;

    private static string? SerializeMobileAppConfig(PrismMobileAppConfig? config)
    {
        var hasAnyValue = config is not null && typeof(PrismMobileAppConfig).GetProperties().Any(property => property.GetValue(config) switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            System.Collections.ICollection items => items.Count > 0,
            _ => true
        });

        return hasAnyValue ? JsonSerializer.Serialize(config) : null;
    }
}
