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
            DeviceCaptureEnabled = true
        });

        files["scripts/bootstrap-ios.sh"].Should().Contain("cp native/ios/", "the native tab shell is part of every iOS bootstrap");
        files["scripts/bootstrap-ios.sh"].Should().Contain("sips -z $((683 * scale))", "launch images at 2732px show as a black launch screen");
        files["native/ios/PrismTabBarController.swift"].Should().Contain("final class PrismTabBarController");
        files["native/android/java/MainActivity.java"].Should().Contain("package com.");
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
    public async Task EveryBundle_ShipsAPrivacyManifest_ThatTheIosBootstrapAddsToTheProject()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest());

        var manifest = files["resources/PrivacyInfo.xcprivacy"];
        manifest.Should().Contain("<key>NSPrivacyTracking</key>\n  <false/>");
        manifest.Should().Contain("NSPrivacyAccessedAPICategoryUserDefaults", "the shell's Swift reads and writes UserDefaults");
        manifest.Should().Contain("CA92.1");
        manifest.Should().Contain("NSPrivacyCollectedDataTypeEmailAddress");
        manifest.Should().NotContain("NSPrivacyCollectedDataTypeDeviceID");
        manifest.Should().NotContain("NSPrivacyCollectedDataTypePhotosorVideos");

        files["scripts/bootstrap-ios.sh"].Should().Contain("cp resources/PrivacyInfo.xcprivacy ios/App/App/PrivacyInfo.xcprivacy");
        files["scripts/bootstrap-ios.sh"].Should().Contain("addResourceFile('App/PrivacyInfo.xcprivacy'");
    }

    [Fact]
    public async Task PrivacyManifest_DeclaresTheDataEachEnabledFeatureCollects()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { PushNotificationsEnabled = true, DeviceCaptureEnabled = true });

        var manifest = files["resources/PrivacyInfo.xcprivacy"];
        manifest.Should().Contain("NSPrivacyCollectedDataTypeDeviceID");
        manifest.Should().Contain("NSPrivacyCollectedDataTypePhotosorVideos");
        manifest.Should().Contain("NSPrivacyCollectedDataTypePreciseLocation");
    }

    [Fact]
    public async Task APageThatFailsToLoad_ShowsTheBundledBrandedOfflineScreen()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest
        {
            ErrorTitle = "Can't reach <Contoso>",
            ErrorMessage = "Check your connection.",
            ErrorBackgroundColor = "#112233"
        });

        files["capacitor.config.ts"].Should().Contain("errorPath: 'offline.html'");

        var offline = files["www/offline.html"];
        offline.Should().Contain("Can&#39;t reach &lt;Contoso&gt;");
        offline.Should().Contain("Check your connection.");
        offline.Should().Contain("#112233");
        offline.Should().Contain("role=\"alert\"");
        offline.Should().Contain("https://contoso.example/?prismMobile=1", "retrying returns to the tenant's start page");
        offline.Should().Contain("addEventListener('online'", "the app recovers by itself when the connection returns");
    }

    [Fact]
    public async Task Readme_TellsTheTenantToSubmitTheAppThemselves()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest());

        files["README.md"].Should().Contain("4.2.6");
        files["README.md"].Should().Contain("PrivacyInfo.xcprivacy");
    }

    [Fact]
    public async Task EveryNativeShellFile_HasTheAppsOwnValuesFilledIn()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { AppId = "com.example.portal" });

        var native = files.Where(file => file.Key.StartsWith("native/", StringComparison.Ordinal)).ToList();
        native.Should().NotBeEmpty();
        native.Should().OnlyContain(file => !file.Value.Contains("@@PRISM_"), "every placeholder is replaced when the bundle is generated");
        files["native/android/java/PrismShell.java"].Should().StartWith("package com.example.portal;");
    }

    [Fact]
    public async Task Readme_MapsTheNativeShell_AndSaysHowToReadItsLogs()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { AppId = "com.example.portal" });

        files["README.md"].Should().Contain("## Native shell");
        files["README.md"].Should().Contain("filter on subsystem `com.example.portal` and category\n  `PrismShell`");
        files["README.md"].Should().Contain("adb logcat -s PrismShell");
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
