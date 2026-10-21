using UmbracoPrism.Core.Controllers.Models;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The opt-in features a bundle is generated with. Each defaults to off, so an app declares and ships only
/// what it asked for.
/// </summary>
internal sealed record MobileBundleFeatures
{
    public bool Biometric { get; init; }

    public bool Push { get; init; }

    public bool DeviceCapture { get; init; }

    /// <summary>Copies of the site's pages kept on the device (Design/mobile-offline-pages.md).</summary>
    public bool SavedPages { get; init; }

    /// <summary>
    /// Journey statuses on the lock screen (Design/mobile-journey-status.md). They travel through
    /// push notifications, so only with them.
    /// </summary>
    public bool JourneyStatus { get; init; }

    public static MobileBundleFeatures From(PrismMobileBundleRequest request) => new()
    {
        Biometric = request.BiometricAuthEnabled ?? false,
        Push = request.PushNotificationsEnabled ?? false,
        DeviceCapture = request.DeviceCaptureEnabled ?? false,
        SavedPages = request.SavedPagesEnabled ?? false,
        JourneyStatus = (request.JourneyStatusEnabled ?? false) && (request.PushNotificationsEnabled ?? false)
    };
}
