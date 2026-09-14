using System.IO.Compression;
using FluentAssertions;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class MobileBundleServiceTests
{
    [Fact]
    public async Task BuildBundleAsync_CreatesZipWithCapacitorConfigAndExpectedFiles()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema
        {
            Id = 42,
            Name = "Northwind",
            Hostname = "northwind.example"
        };

        var payload = new PrismMobileBundleRequest
        {
            AppName = "Northwind Mobile",
            AppId = "com.example.northwind",
            Version = "1.2.3",
            StartUrl = "https://northwind.example",
            UserAgentMarker = "PrismMobile"
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);

        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        archive.GetEntry("capacitor.config.ts").Should().NotBeNull();
        archive.GetEntry("package.json").Should().NotBeNull();
        archive.GetEntry("README.md").Should().NotBeNull();
        archive.GetEntry("AGENT_PROMPT.md").Should().NotBeNull();
        archive.GetEntry("www/index.html").Should().NotBeNull();
        archive.GetEntry("www/mobile-overrides.css").Should().NotBeNull();
        archive.GetEntry("scripts/doctor-mobile.sh").Should().NotBeNull();
        archive.GetEntry("scripts/bootstrap-ios.sh").Should().NotBeNull();
        archive.GetEntry("scripts/bootstrap-android.sh").Should().NotBeNull();
        archive.GetEntry("scripts/trust-ios-localhost-cert.sh").Should().NotBeNull();
        archive.GetEntry("resources/icon.svg").Should().NotBeNull();

        var config = ReadEntry(archive, "capacitor.config.ts");
        config.Should().Contain("appId: 'com.example.northwind'");
        config.Should().Contain("appName: 'Northwind Mobile'");
        config.Should().Contain("appendUserAgent: 'PrismMobile'");
        config.Should().Contain("contentInset: 'never'");
        config.Should().Contain("url: 'https://northwind.example/?prismMobile=1'");
        config.Should().Contain("allowNavigation:");
        config.Should().Contain("'northwind.example'");
        config.Should().Contain("'login.microsoftonline.com'");
        config.Should().Contain("'*.ciamlogin.com'");
        config.Should().Contain("overlaysWebView: false");

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().Contain("\"doctor\": \"bash scripts/doctor-mobile.sh\"");
        packageJson.Should().Contain("\"bootstrap:ios\": \"bash scripts/bootstrap-ios.sh\"");
        packageJson.Should().Contain("\"bootstrap:android\": \"bash scripts/bootstrap-android.sh\"");

        var index = ReadEntry(archive, "www/index.html");
        index.Should().Contain("We’re having trouble connecting");
        index.Should().Contain("showDiagnostics: true");
        index.Should().Contain("parsed.searchParams.set('prismMobile', '1');");
        index.Should().Contain("window.location.replace(mobileStartUrl);");

        var readme = ReadEntry(archive, "README.md");
        readme.Should().Contain("npm run doctor");
        readme.Should().Contain("npm run bootstrap:ios");
        readme.Should().Contain("App startup uses Capacitor top-level WebView loading of your Start URL.");
        readme.Should().Contain("Generated config appends `prismMobile=1` to Start URL for server-side mobile detection.");
    }

    private static string ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        entry.Should().NotBeNull();

        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricDisabled_PackageJsonHasNoBiometricDeps()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = false
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().NotContain("@aparajita/capacitor-biometric-auth");
        packageJson.Should().NotContain("@aparajita/capacitor-secure-storage");
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricNull_PackageJsonHasNoBiometricDeps()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = null
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().NotContain("@aparajita/capacitor-biometric-auth");
        packageJson.Should().NotContain("@aparajita/capacitor-secure-storage");

        archive.GetEntry("resources/ios-info-plist-additions.xml").Should().BeNull();
        archive.GetEntry("resources/android-manifest-additions.xml").Should().BeNull();
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricEnabled_PackageJsonIncludesBiometricDeps()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().Contain("\"@aparajita/capacitor-biometric-auth\": \"^7.0.0\"");
        packageJson.Should().Contain("\"@aparajita/capacitor-secure-storage\": \"^7.0.0\"");
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricEnabled_ReadmeContainsBiometricSection()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var readme = ReadEntry(archive, "README.md");
        readme.Should().Contain("## Biometric Login Setup");
        readme.Should().Contain("NSFaceIDUsageDescription");
        readme.Should().Contain("USE_BIOMETRIC");
        readme.Should().Contain("isAvailable: false");
        readme.Should().Contain("adb emu finger touch 1");
        readme.Should().Contain("@aparajita/capacitor-biometric-auth");
        readme.Should().Contain("@aparajita/capacitor-secure-storage");
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricDisabled_ReadmeHasNoBiometricSection()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = false
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var readme = ReadEntry(archive, "README.md");
        readme.Should().NotContain("## Biometric Login Setup");
        readme.Should().NotContain("@aparajita/capacitor-biometric-auth");
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricEnabled_IncludesResourceFiles()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosPlist = ReadEntry(archive, "resources/ios-info-plist-additions.xml");
        iosPlist.Should().Contain("NSFaceIDUsageDescription");
        iosPlist.Should().Contain("Test App");

        var androidManifest = ReadEntry(archive, "resources/android-manifest-additions.xml");
        androidManifest.Should().Contain("android.permission.USE_BIOMETRIC");
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricEnabled_AgentPromptContainsBiometricContext()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var agentPrompt = ReadEntry(archive, "AGENT_PROMPT.md");
        agentPrompt.Should().Contain("## Biometric authentication");
        agentPrompt.Should().Contain("@aparajita/capacitor-biometric-auth");
        agentPrompt.Should().Contain("adb emu finger touch 1");
    }

    [Fact]
    public async Task BuildBundleAsync_BiometricEnabled_BootstrapScriptsInjectEntitlements()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            BiometricAuthEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().Contain("NSFaceIDUsageDescription");
        iosBootstrap.Should().Contain("plutil -insert NSFaceIDUsageDescription");

        var androidBootstrap = ReadEntry(archive, "scripts/bootstrap-android.sh");
        androidBootstrap.Should().Contain("USE_BIOMETRIC");
        androidBootstrap.Should().Contain("android.permission.USE_BIOMETRIC");
    }

    [Fact]
    public async Task BuildBundleAsync_BootstrapScripts_SkipSimulatorAndEmulatorLaunchInCi()
    {
        // A CI runner has no booted simulator/emulator, so the scripts' normal "run app, or open
        // the IDE" fallback would try to launch Xcode/Android Studio's GUI — hanging or failing
        // headlessly. Both scripts must check the standard $CI env var (set by GitHub Actions and
        // most other CI systems) and stop after sync instead, leaving the native project ready
        // for a separate signing/build step (xcodebuild / gradlew) to take over.
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest { AppName = "Test App", AppId = "com.example.test" };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().Contain("\"${CI:-}\" == \"true\"");
        iosBootstrap.Should().Contain("skipping simulator run/open");

        var androidBootstrap = ReadEntry(archive, "scripts/bootstrap-android.sh");
        androidBootstrap.Should().Contain("\"${CI:-}\" == \"true\"");
        androidBootstrap.Should().Contain("skipping emulator run/open");
    }

    [Fact]
    public async Task BuildBundleAsync_BootstrapIos_InjectsZoomFixRegardlessOfBiometricSetting()
    {
        // Reported live on the Entra sign-in page's password screen (a WKWebView-level
        // zoom-into-focused-input bug on hosted content Prism has no CSS/viewport control over —
        // see bootstrap-ios.sh's own comment for the full rationale). Unlike the biometric
        // Info.plist injection, this fix is unconditional — it isn't specific to biometric auth —
        // so it must be present with BiometricAuthEnabled left at its default (false/unset) too.
        //
        // Also asserts the project.pbxproj registration step: a prior version of this fix wrote
        // PrismBridgeViewController.swift to disk but never added it to the Xcode project, which
        // built clean (xcodebuild has no way to notice an unreferenced file) but was dead on
        // arrival on a real device — the storyboard's customClass reference couldn't resolve at
        // runtime, leaving a blank screen with no crash. Only a real simulator install+launch
        // caught that; this test guards the registration step exists, not just the Swift file.
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest { AppName = "Test App", AppId = "com.example.test" };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().Contain("class PrismBridgeViewController: CAPBridgeViewController");
        iosBootstrap.Should().Contain("maximum-scale=1, user-scalable=no");
        iosBootstrap.Should().Contain("forMainFrameOnly: false");
        iosBootstrap.Should().Contain("ios/App/App/PrismBridgeViewController.swift");

        // Reported live on the same Entra password screen: contentInset:'always' (a scroll-offset
        // setting) did NOT stop hosted content rendering under the status bar/notch — it only
        // offsets scroll position, which does nothing for content that doesn't scroll or that a
        // page positions outside normal flow. The real fix is one level up, in AppDelegate: wraps
        // the storyboard's own root view controller in a plain container via standard view
        // controller containment, safe-area-pinned. Not done inside PrismBridgeViewController
        // itself — confirmed via Capacitor's own vendored source that its loadView() is `final`
        // and unconditionally does `view = webView`, so the view controller's `view` and its
        // `webView` are the same object; there's no separate webview-inside-a-container to
        // reconstrain from in there. Verified end-to-end via a real simulator install+launch
        // against the actual Entra page (not just this generator's own output): the title no
        // longer overlaps the status bar/Dynamic Island, and renders with its full text intact.
        iosBootstrap.Should().Contain("bridgeViewController.view.topAnchor.constraint(equalTo: container.view.safeAreaLayoutGuide.topAnchor)");
        iosBootstrap.Should().Contain("window?.rootViewController = container");

        // A normal browser tab avoids a blank flash between pages via "paint holding" — WKWebView,
        // embedded the way Capacitor uses it, doesn't do this on its own (confirmed by reading
        // Capacitor's own vendored iOS source: no snapshot/hold mechanism anywhere in it). This
        // approximates it: freezes the outgoing page as a snapshot the instant a navigation
        // starts, only revealing a spinner on top of it if the real navigation is slow enough to
        // notice. Forwards every other WKNavigationDelegate call straight through to Capacitor's
        // own delegate (the standard Cocoa decorator pattern) rather than reimplementing its own
        // navigation policy/redirect/auth-challenge handling by hand.
        iosBootstrap.Should().Contain("class PrismNavigationHoldDelegate: NSObject, WKNavigationDelegate");
        iosBootstrap.Should().Contain("webView.navigationDelegate = hold");
        iosBootstrap.Should().Contain("func forwardingTarget(for aSelector: Selector!) -> Any?");
        iosBootstrap.Should().Contain("webView.snapshotView(afterScreenUpdates: false)");
        iosBootstrap.Should().Contain("DispatchQueue.main.asyncAfter(deadline: .now() + 0.1");

        iosBootstrap.Should().Contain("customClass=\"PrismBridgeViewController\" customModule=\"App\"");
        iosBootstrap.Should().Contain("Main.storyboard");
        iosBootstrap.Should().Contain("import xcode from 'xcode'");
        iosBootstrap.Should().Contain("project.addSourceFile('App/PrismBridgeViewController.swift'");
        iosBootstrap.Should().Contain("registered in project.pbxproj");

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().Contain("\"xcode\": \"^3.0.1\"");
    }

    [Fact]
    public async Task BuildBundleAsync_IncludesADefaultAppIcon_AndWiresUpAssetGeneration()
    {
        // Found live on the first real TestFlight build: every generated app shipped Capacitor's
        // own generic default icon — nothing in the pipeline had ever baked in a real one.
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest { AppName = "Test App", AppId = "com.example.test" };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var icon = ReadEntry(archive, "resources/icon.svg");
        // iOS App Store icons must be fully opaque — no alpha channel — so the source itself must
        // carry a solid background rather than relying on @capacitor/assets to add one.
        icon.Should().Contain("<rect width=\"1024\" height=\"1024\" fill=\"#1B264F\"/>",
            "the icon source must have an opaque background — iOS App Store icons reject alpha");

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().Contain("\"@capacitor/assets\"");

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().Contain("npx capacitor-assets generate --ios");

        var androidBootstrap = ReadEntry(archive, "scripts/bootstrap-android.sh");
        androidBootstrap.Should().Contain("npx capacitor-assets generate --android");
    }
}
