using System.IO.Compression;
using AwesomeAssertions;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class MobileBundleTemplatesTests
{
    private static readonly PrismTenantSchema Tenant = new() { Id = 7, Name = "Contoso", Hostname = "contoso.example" };

    private static async Task<Dictionary<string, string>> BuildAsync(PrismMobileBundleRequest request)
    {
        var bytes = await new MobileBundleService().BuildBundleAsync(Tenant, request);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public async Task WithEveryFeatureOn_EveryFixedPieceOfTheBundleIsPresentInTheOutput()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest
        {
            BiometricAuthEnabled = true,
            PushNotificationsEnabled = true,
            MobileDiagnosticsEnabled = true,
            DeviceCaptureEnabled = true
        });

        files["scripts/bootstrap-ios.sh"].Should().Contain("PrismBridgeViewController", "the iOS zoom fix is part of every iOS bootstrap");
        files["scripts/bootstrap-ios.sh"].Should().Contain("PrismMobileDiagnosticsFlag", "the diagnostics flag is spliced into the Swift");
        files["scripts/bootstrap-ios.sh"].Should().Contain("NSFaceIDUsageDescription", "biometric adds the Face ID usage string");
        files["scripts/bootstrap-ios.sh"].Should().Contain("UIBackgroundModes", "push adds remote-notification background mode");
        files["scripts/bootstrap-ios.sh"].Should().Contain("NSLocationWhenInUseUsageDescription", "device capture adds the location usage string");
        files["scripts/bootstrap-android.sh"].Should().Contain("ACCESS_FINE_LOCATION", "device capture adds the location permission");
        files["scripts/bootstrap-android.sh"].Should().Contain("USE_BIOMETRIC");
        files["scripts/bootstrap-android.sh"].Should().Contain("google-services.json");
        files["www/index.html"].Should().Contain("tryBiometricSignIn");
        files["www/mobile-overrides.css"].Should().NotBeNullOrWhiteSpace();
        files["resources/icon.svg"].Should().StartWith("<svg");
        files["resources/android-manifest-additions.xml"].Should().Contain("USE_BIOMETRIC");
        files["README.md"].Should().Contain("Biometric Login Setup");
    }

    [Fact]
    public async Task WithEveryFeatureOff_NoFeatureSpecificPieceLeaksIntoTheOutput()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest());

        files["scripts/bootstrap-ios.sh"].Should().NotContain("NSFaceIDUsageDescription");
        files["scripts/bootstrap-ios.sh"].Should().NotContain("NSLocationWhenInUseUsageDescription");
        files["scripts/bootstrap-ios.sh"].Should().NotContain("NSCameraUsageDescription");
        files["scripts/bootstrap-ios.sh"].Should().NotContain("UIBackgroundModes");
        files["scripts/bootstrap-android.sh"].Should().NotContain("USE_BIOMETRIC");
        files["scripts/bootstrap-android.sh"].Should().NotContain("ACCESS_FINE_LOCATION");
        files["scripts/bootstrap-android.sh"].Should().NotContain("android.hardware.camera");
        files["www/index.html"].Should().NotContain("tryBiometricSignIn");
        files.Should().NotContainKey("resources/android-manifest-additions.xml");
        files["README.md"].Should().NotContain("Biometric Login Setup");
    }

    [Fact]
    public async Task DeviceCaptureAlone_AddsTheCameraAndLocationPermissions_AndNothingForBiometrics()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { DeviceCaptureEnabled = true });

        // iOS shows no prompt, and the webview never gets a location fix, unless the app says why it wants it.
        files["scripts/bootstrap-ios.sh"].Should().Contain("NSLocationWhenInUseUsageDescription");
        files["scripts/bootstrap-ios.sh"].Should().Contain("NSCameraUsageDescription");
        files["scripts/bootstrap-ios.sh"].Should().NotContain("NSFaceIDUsageDescription");

        files["scripts/bootstrap-android.sh"].Should().Contain("ACCESS_FINE_LOCATION ACCESS_COARSE_LOCATION CAMERA");

        files["scripts/bootstrap-android.sh"].Should().Contain("android.hardware.camera", "a phone without a camera can still install the app");
        files["scripts/bootstrap-android.sh"].Should().NotContain("USE_BIOMETRIC");
    }

    [Fact]
    public async Task GeneratedScripts_UseUnixLineEndings()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { BiometricAuthEnabled = true, PushNotificationsEnabled = true });

        foreach (var (path, content) in files.Where(file => file.Key.StartsWith("scripts/")))
        {
            content.Should().NotContain("\r", $"{path} is run by bash");
        }
    }
}
