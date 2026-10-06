using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using static UmbracoPrism.Core.Services.MobileBundles.BundleText;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// Everything a bundle is generated from, resolved from the tenant and the backoffice request:
/// defaults applied, the start URL made absolute, and invalid input rejected up front.
/// </summary>
internal sealed record MobileBundleSettings(
    PrismTenantSchema Tenant,
    string AppName,
    string AppId,
    string Version,
    string UserAgentMarker,
    string StartUrl,
    string? IconUrl,
    string? SplashUrl,
    string ErrorBackgroundColor,
    string ErrorTextColor,
    string ErrorTitle,
    string ErrorMessage,
    bool ShowErrorDiagnostics,
    MobileBundleFeatures Features)
{
    public bool BiometricAuthEnabled => Features.Biometric;

    public bool MobileDiagnosticsEnabled => Features.Diagnostics;

    public bool PushNotificationsEnabled => Features.Push;

    public bool DeviceCaptureEnabled => Features.DeviceCapture;

    /// <exception cref="ArgumentException">Thrown when request input contains invalid app identifiers or URLs.</exception>
    public static MobileBundleSettings Resolve(PrismTenantSchema tenant, PrismMobileBundleRequest request)
    {
        var appName = OrDefault(request.AppName, tenant.Name);
        var appId = OrDefault(request.AppId, $"com.prism.{ToSafeIdentifier(tenant.Name)}");
        var version = OrDefault(request.Version, "1.0.0");
        var marker = OrDefault(request.UserAgentMarker, "PrismMobile");
        var startUrl = BuildStartUrl(request.StartUrl, tenant.Hostname);
        var iconUrl = RewriteMediaHost(request.IconUrl?.Trim(), startUrl);
        var splashUrl = RewriteMediaHost(request.SplashUrl?.Trim(), startUrl);
        var errorBackgroundColor = OrDefault(request.ErrorBackgroundColor, "#0f172a");
        var errorTextColor = OrDefault(request.ErrorTextColor, "#f8fafc");
        var errorTitle = OrDefault(request.ErrorTitle, "We’re having trouble connecting");
        var errorMessage = OrDefault(request.ErrorMessage, "Please check your connection and try again.");
        var showErrorDiagnostics = request.ShowErrorDiagnostics ?? true;

        if (!IsValidAppId(appId))
        {
            throw new ArgumentException("App ID must be a reverse-domain identifier, e.g. com.example.portal");
        }

        if (!string.IsNullOrWhiteSpace(iconUrl) && iconUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Icon URL must point to a raster image (PNG or JPG). SVG files cannot be converted by the image pipeline. Please export your icon as a 1024×1024 PNG.");
        }

        return new MobileBundleSettings(
            tenant, appName, appId, version, marker, startUrl, iconUrl, splashUrl,
            errorBackgroundColor, errorTextColor, errorTitle, errorMessage, showErrorDiagnostics,
            MobileBundleFeatures.From(request));
    }

    private static string BuildStartUrl(string? startUrl, string hostname)
    {
        if (!string.IsNullOrWhiteSpace(startUrl))
        {
            if (Uri.TryCreate(startUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return uri.ToString().TrimEnd('/');
            }

            throw new ArgumentException("Start URL must be an absolute URL, e.g. https://portal.example.com");
        }

        var host = hostname.Trim();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return host.TrimEnd('/');
        }

        return $"https://{host}";
    }

    private static string? RewriteMediaHost(string? mediaUrl, string resolvedStartUrl)
    {
        if (string.IsNullOrEmpty(mediaUrl)) return mediaUrl;
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var mediaUri)) return mediaUrl;
        if (!mediaUri.IsLoopback) return mediaUrl;

        if (!Uri.TryCreate(resolvedStartUrl, UriKind.Absolute, out var originUri)) return mediaUrl;

        var builder = new UriBuilder(mediaUri)
        {
            Scheme = originUri.Scheme,
            Host = originUri.Host,
            Port = originUri.IsDefaultPort ? -1 : originUri.Port
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static bool IsValidAppId(string appId)
    {
        var segments = appId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2) return false;

        return segments.All(segment => segment.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'));
    }
}
