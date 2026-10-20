using System.Text.Json;
using System.Text.RegularExpressions;
using UmbracoPrism.Core.Controllers.Models;

namespace UmbracoPrism.Core.Models;

/// <summary>
/// The tenant's own apps, as the site vouches for them in apple-app-site-association and
/// assetlinks.json. Only these apps can receive the sign-in callback.
/// </summary>
/// <param name="AppleAppId">The team-prefixed iOS app ID, <c>TEAMID.com.example.app</c>.</param>
/// <param name="AndroidPackageName">The Android package name.</param>
/// <param name="AndroidCertificateFingerprints">Upper-case, colon-separated SHA-256 fingerprints.</param>
public sealed partial record PrismMobileAppLinks(
    string? AppleAppId,
    string? AndroidPackageName,
    IReadOnlyList<string> AndroidCertificateFingerprints)
{
    public static readonly PrismMobileAppLinks None = new(null, null, []);

    public bool HasIos => AppleAppId is not null;

    public bool HasAndroid => AndroidPackageName is not null && AndroidCertificateFingerprints.Count > 0;

    public bool HasAny => HasIos || HasAndroid;

    /// <summary>The app links in a tenant's saved mobile app config; values that fail <see cref="Validate"/> are left out.</summary>
    public static PrismMobileAppLinks FromConfigJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return None;

        try
        {
            return From(JsonSerializer.Deserialize<PrismMobileAppConfig>(json));
        }
        catch (JsonException)
        {
            return None;
        }
    }

    internal static PrismMobileAppLinks From(PrismMobileAppConfig? config)
    {
        if (config is null) return None;

        var appId = config.AppId?.Trim();
        var teamId = config.AppleTeamId?.Trim();
        var appleAppId = IsBundleId(appId) && IsTeamId(teamId) ? $"{teamId}.{appId}" : null;

        var package = string.IsNullOrWhiteSpace(config.AndroidPackageName) ? appId : config.AndroidPackageName.Trim();
        var fingerprints = (config.AndroidCertificateFingerprints ?? [])
            .Select(NormalizeFingerprint)
            .Where(IsFingerprint)
            .Distinct()
            .ToList();

        return new PrismMobileAppLinks(appleAppId, IsPackageName(package) ? package : null, fingerprints);
    }

    /// <summary>Why the app link fields in <paramref name="config"/> are invalid, or null when they are valid.</summary>
    public static string? Validate(PrismMobileAppConfig? config)
    {
        if (config is null) return null;

        if (!string.IsNullOrWhiteSpace(config.AppleTeamId) && !IsTeamId(config.AppleTeamId.Trim()))
        {
            return "Apple team ID must be 10 letters or digits, as shown in the Apple Developer account.";
        }

        if (!string.IsNullOrWhiteSpace(config.AndroidPackageName) && !IsPackageName(config.AndroidPackageName.Trim()))
        {
            return "Android package name must be a reverse-domain identifier, e.g. com.example.portal";
        }

        var invalid = (config.AndroidCertificateFingerprints ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .FirstOrDefault(value => !IsFingerprint(NormalizeFingerprint(value)));

        return invalid is null
            ? null
            : "Android certificate fingerprints must be SHA-256: 32 pairs of hex digits separated by colons.";
    }

    private static string NormalizeFingerprint(string value) => value.Trim().ToUpperInvariant();

    private static bool IsTeamId(string? value) => value is not null && TeamIdPattern().IsMatch(value);

    private static bool IsBundleId(string? value) => value is not null && BundleIdPattern().IsMatch(value);

    private static bool IsPackageName(string? value) => value is not null && PackageNamePattern().IsMatch(value);

    private static bool IsFingerprint(string value) => FingerprintPattern().IsMatch(value);

    [GeneratedRegex("^[A-Z0-9]{10}$")]
    private static partial Regex TeamIdPattern();

    [GeneratedRegex("^[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)+$")]
    private static partial Regex BundleIdPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+$")]
    private static partial Regex PackageNamePattern();

    [GeneratedRegex("^([0-9A-F]{2}:){31}[0-9A-F]{2}$")]
    private static partial Regex FingerprintPattern();
}
