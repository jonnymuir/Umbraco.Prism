using UmbracoPrism.Core.Controllers.Models;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The opt-in features a bundle is generated with. Each defaults to off, so an app declares and ships only
/// what it asked for.
/// </summary>
internal sealed record MobileBundleFeatures(bool Biometric, bool Diagnostics, bool Push, bool DeviceCapture)
{
    public static MobileBundleFeatures From(PrismMobileBundleRequest request) => new(
        request.BiometricAuthEnabled ?? false,
        request.MobileDiagnosticsEnabled ?? false,
        request.PushNotificationsEnabled ?? false,
        request.DeviceCaptureEnabled ?? false);
}
