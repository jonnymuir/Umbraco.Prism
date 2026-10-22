using System.IO.Compression;
using AwesomeAssertions;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// An app built with journey statuses carries the iOS widget extension and Live Activity code, and
/// the Android Live Update service, and its bootstrap scripts add them; one built without does not.
/// </summary>
public class JourneyStatusBundleTests
{
    [Fact]
    public async Task AnAppWithJourneyStatus_AddsTheLiveActivityExtension_AndTheAndroidService()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { PushNotificationsEnabled = true, JourneyStatusEnabled = true });

        files.Keys.Should().Contain([
            "native/ios-journey-status/App/PrismJourneyStatusAttributes.swift",
            "native/ios-journey-status/App/PrismJourneyStatusDevice.swift",
            "native/ios-journey-status/PrismJourneyStatus/PrismJourneyStatusWidget.swift",
            "native/ios-journey-status/PrismJourneyStatus/Info.plist",
            "scripts/ios-add-journey-status.mjs",
            "native/android-journey-status/java/PrismJourneyStatusService.java",
            "native/android-journey-status/java/PrismJourneyStatusNotifications.java",
            "native/android-journey-status/java/PrismJourneyStatusDevice.java"]);
        files["native/ios-journey-status/App/PrismJourneyStatusAttributes.swift"].Should().Contain("https://contoso.example/?prismMobile=1");
        files["native/android-journey-status/java/PrismJourneyStatusService.java"].Should().StartWith("package com.prism.contoso;");

        files["scripts/bootstrap-ios.sh"].Should().Contain("node scripts/ios-add-journey-status.mjs")
            .And.Contain("NSSupportsLiveActivities")
            .And.Contain("PrismJourneyStatusDevice.shared.start()");
        files["scripts/bootstrap-android.sh"].Should().Contain(".PrismJourneyStatusService")
            .And.Contain("POST_PROMOTED_NOTIFICATIONS")
            .And.Contain("PrismJourneyStatusDevice.start(this);")
            .And.Contain("android/app/src/main/java/com/prism/contoso");
    }

    [Fact]
    public async Task TheExtension_IsSignedAsItsOwnAppIdentity_WithNoneOfTheAppsEntitlements()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { PushNotificationsEnabled = true, JourneyStatusEnabled = true });

        var script = files["scripts/ios-add-journey-status.mjs"];
        script.Should().Contain("`${String(appSettings.PRODUCT_BUNDLE_IDENTIFIER).replaceAll('\"', '')}.JourneyStatus`");
        script.Should().Contain("CODE_SIGN_ENTITLEMENTS: '\"\"'");
        files["scripts/bootstrap-ios.sh"].Should().Contain("updateBuildProperty('CODE_SIGN_ENTITLEMENTS', 'App/App.entitlements', null, 'App')");
        files["scripts/ios-add-native-shell.mjs"].Should().Contain("updateBuildProperty('CODE_SIGN_ENTITLEMENTS', 'App/App.entitlements', null, 'App')");
    }

    [Fact]
    public async Task JourneyStatus_NeedsPushNotifications_WhichStatusesTravelThrough()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { JourneyStatusEnabled = true });

        files.Keys.Should().NotContain(path => path.Contains("journey-status"));
        files["scripts/bootstrap-ios.sh"].Should().NotContain("journey");
        files["scripts/bootstrap-android.sh"].Should().NotContain("journey");
    }

    [Fact]
    public async Task AnAppWithoutJourneyStatus_HasNoneOfIt()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { PushNotificationsEnabled = true });

        files.Keys.Should().NotContain(path => path.Contains("journey-status"));
        files["scripts/bootstrap-ios.sh"].Should().NotContain("PrismJourneyStatus");
        files["scripts/bootstrap-android.sh"].Should().NotContain("PrismJourneyStatus");
    }

    private static async Task<Dictionary<string, string>> BuildAsync(PrismMobileBundleRequest request)
    {
        var bytes = await new MobileBundleService().BuildBundleAsync(new PrismTenantSchema { Name = "Contoso", Hostname = "contoso.example" }, request);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }
}
