using System.Text.Json;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core;

/// <summary>
/// Puts the configured app identities (<c>Prism:SeedTenant:MobileApp</c>) into a tenant's saved
/// mobile config, keeping its other mobile settings (name, icon, error screen) as they are.
/// </summary>
internal static class PrismSeededMobileAppConfig
{
    /// <summary>
    /// The saved config with each configured value in place of the saved one. Returns the saved
    /// config unchanged when nothing is configured, or when the configured values are invalid, in
    /// which case <paramref name="error"/> says why.
    /// </summary>
    internal static string? Merge(string? savedJson, PrismSeedMobileAppOptions seeded, out string? error)
    {
        error = null;
        if (!seeded.HasAny)
        {
            return savedJson;
        }

        var config = Read(savedJson);
        config.AppId = Configured(seeded.AppId) ?? config.AppId;
        config.AppleTeamId = Configured(seeded.AppleTeamId) ?? config.AppleTeamId;
        config.AndroidPackageName = Configured(seeded.AndroidPackageName) ?? config.AndroidPackageName;
        if (seeded.AndroidCertificateFingerprints.Count > 0)
        {
            config.AndroidCertificateFingerprints = [.. seeded.AndroidCertificateFingerprints.Select(fingerprint => fingerprint.Trim())];
        }

        error = PrismMobileAppLinks.Validate(config);
        return error is null ? JsonSerializer.Serialize(config) : savedJson;
    }

    private static string? Configured(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PrismMobileAppConfig Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PrismMobileAppConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<PrismMobileAppConfig>(json) ?? new PrismMobileAppConfig();
        }
        catch (JsonException)
        {
            return new PrismMobileAppConfig();
        }
    }
}
