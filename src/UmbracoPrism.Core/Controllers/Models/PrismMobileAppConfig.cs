namespace UmbracoPrism.Core.Controllers.Models;

public class PrismMobileAppConfig
{
    public string? AppName { get; set; }
    public string? AppId { get; set; }
    public string? Version { get; set; }
    public string? StartUrl { get; set; }
    public string? UserAgentMarker { get; set; }
    public string? IconUrl { get; set; }
    public string? SplashUrl { get; set; }
    public string? ErrorBackgroundColor { get; set; }
    public string? ErrorTextColor { get; set; }
    public string? ErrorTitle { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? ShowErrorDiagnostics { get; set; }

    /// <summary>The Apple developer team ID the iOS app is signed with, for apple-app-site-association.</summary>
    public string? AppleTeamId { get; set; }

    /// <summary>The Android package name, when it differs from <see cref="AppId"/>.</summary>
    public string? AndroidPackageName { get; set; }

    /// <summary>SHA-256 fingerprints of the certificates the Android app is signed with, for assetlinks.json.</summary>
    public List<string>? AndroidCertificateFingerprints { get; set; }
}
