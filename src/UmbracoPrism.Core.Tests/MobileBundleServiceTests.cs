using System.IO.Compression;
using AwesomeAssertions;
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
        readme.Should().Contain("On iOS the app has native tabs");
        readme.Should().Contain("Generated config appends `prismMobile=1` to Start URL for server-side mobile detection.");
    }

    [Fact]
    public async Task BuildBundleAsync_BootstrapScripts_RegisterIdentityProviderCookiePluginRegardlessOfBiometricSetting()
    {
        // Reported live: a biometric-tagged sign-out correctly skips the federated Entra
        // redirect (no session for it to end in this WebView), but that leaves Entra's own SSO
        // cookie from the device's original interactive sign-in stale forever, so a later login
        // silently re-authenticates instead of showing credentials again. This plugin clears it
        // natively instead — unconditional, matching prism-biometric-signout.js's own reasoning:
        // it matters for every mobile sign-out, not just biometric-enabled tenants.
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest { AppName = "Test App", AppId = "com.example.test" };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadPlatform(archive, "ios");
        iosBootstrap.Should().Contain("bridge?.registerPluginInstance(PrismIdentityCookiePlugin())");
        iosBootstrap.Should().Contain("class PrismIdentityCookiePlugin: CAPPlugin, CAPBridgedPlugin");
        iosBootstrap.Should().Contain("public let jsName = \"PrismIdentityCookiePlugin\"");
        iosBootstrap.Should().Contain("@objc func clearCookies(_ call: CAPPluginCall)");
        iosBootstrap.Should().Contain("let store = WKWebsiteDataStore.default()");
        iosBootstrap.Should().Contain("store.removeData(ofTypes: [WKWebsiteDataTypeCookies], for: matching)");

        var androidBootstrap = ReadPlatform(archive, "android");
        androidBootstrap.Should().Contain("JAVA_DIR=\"android/app/src/main/java/com/example/test\"");
        androidBootstrap.Should().Contain("cp native/android/java/*.java \"$JAVA_DIR/\"");
        androidBootstrap.Should().Contain("package com.example.test;");
        androidBootstrap.Should().Contain("@CapacitorPlugin(name = \"PrismIdentityCookiePlugin\")");
        androidBootstrap.Should().Contain("cookieManager.removeAllCookies(value -> {");

        // Plain Java only: the app module Capacitor generates has no Kotlin Gradle plugin, so a
        // .kt file would silently never compile.
        androidBootstrap.Should().NotContain("PrismIdentityCookiePlugin.kt");

        // Every tab's bridge registers the plugin, not just a file on disk.
        androidBootstrap.Should().Contain("plugins.add(PrismIdentityCookiePlugin.class);");
    }

    /// <summary>A platform's bootstrap script with the native shell files it copies into the project.</summary>
    private static string ReadPlatform(ZipArchive archive, string platform) =>
        string.Join("\n", archive.Entries
            .Where(entry => entry.FullName == $"scripts/bootstrap-{platform}.sh"
                || entry.FullName.StartsWith($"native/{platform}/", StringComparison.Ordinal)
                || entry.FullName == $"scripts/{platform}-add-native-shell.mjs")
            .Select(entry => ReadEntry(archive, entry.FullName)));

    private static string ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        entry.Should().NotBeNull();

        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task BuildBundleAsync_AlwaysIncludesCapacitorAppDependency()
    {
        // Unconditional, unlike the biometric/push deps below — every generated app needs its
        // own 'resume' lifecycle event (wayfinder-poll.js listens for it directly), regardless of
        // whether biometric auth or push notifications are enabled.
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
        packageJson.Should().Contain("\"@capacitor/app\"");
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
    public async Task BuildBundleAsync_BootstrapIos_WritesTheNativeTabShell_AndRegistersItInTheXcodeProject()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest { AppName = "Test App", AppId = "com.example.test" };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadPlatform(archive, "ios");
        iosBootstrap.Should().Contain("static let startURL = URL(string: \"https://test.example/?prismMobile=1\")!");
        iosBootstrap.Should().Contain("final class PrismTabBarController: UITabBarController");
        iosBootstrap.Should().Contain("window?.rootViewController = PrismTabBarController()");
        iosBootstrap.Should().Contain("static let manifestPath = \"/umbraco/prism/mobile/manifest\"");

        // Xcode builds cleanly without a file that is not in the project, then the app launches blank.
        iosBootstrap.Should().Contain("cp native/ios/*.swift native/ios/App.entitlements ios/App/App/");
        iosBootstrap.Should().Contain("node scripts/ios-add-native-shell.mjs");
        iosBootstrap.Should().Contain("project.addSourceFile(`App/${file}`, { target }, 'App');");

        // Hosted sign-in pages cannot be edited, so the zoom fix is injected natively into every frame.
        iosBootstrap.Should().Contain("maximum-scale=1, user-scalable=no, viewport-fit=cover");
        iosBootstrap.Should().Contain("forMainFrameOnly: false");

        // Each tab's bridge claims notification handling when created; taps must reach the visible tab.
        iosBootstrap.Should().Contain("UNUserNotificationCenter.current().delegate = router");

        iosBootstrap.Should().NotContain("PrismNavigationHoldDelegate", "paint-holding is gone with one webview per tab");
        iosBootstrap.Should().NotContain("PrismContentWatcherPlugin");
    }

    [Fact]
    public async Task BuildBundleAsync_AppsAnnounceNativeTabsAndSystemSignIn_OnBothPlatforms()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };

        var zipBytes = await service.BuildBundleAsync(tenant, new PrismMobileBundleRequest { AppName = "Test App" });
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var config = ReadEntry(archive, "capacitor.config.ts");
        config.Should().Contain("""
              ios: {
                // The tabs are native, so the site leaves out its web tab bar, and sign-in uses the system
                // sign-in sheet (see prism-system-sign-in.js).
                appendUserAgent: 'PrismMobile PrismNativeTabs PrismSystemSignIn',
            """);
        config.Should().Contain("""
              android: {
                appendUserAgent: 'PrismMobile PrismNativeTabs PrismSystemSignIn',
            """);
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

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task BuildBundleAsync_PushNotificationsDisabled_PackageJsonHasNoPushDeps(bool? pushNotificationsEnabled)
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            PushNotificationsEnabled = pushNotificationsEnabled
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().NotContain("@capacitor-firebase/messaging");

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().NotContain("UIBackgroundModes");
        iosBootstrap.Should().NotContain("aps-environment");

        var androidBootstrap = ReadEntry(archive, "scripts/bootstrap-android.sh");
        androidBootstrap.Should().NotContain("google-services.json");

        var readme = ReadEntry(archive, "README.md");
        readme.Should().NotContain("## Push Notification Setup");
    }

    [Fact]
    public async Task BuildBundleAsync_PushNotificationsEnabled_PackageJsonIncludesPushDeps()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            PushNotificationsEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var packageJson = ReadEntry(archive, "package.json");
        packageJson.Should().Contain("\"@capacitor-firebase/messaging\": \"^7.3.0\"");
        packageJson.Should().Contain("\"firebase\": \"^11.0.0\"");
    }

    [Fact]
    public async Task BuildBundleAsync_PushNotificationsEnabled_ReadmeContainsPushSection()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            PushNotificationsEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var readme = ReadEntry(archive, "README.md");
        readme.Should().Contain("## Push Notification Setup");
        readme.Should().Contain("resources/GoogleService-Info.plist");
        readme.Should().Contain("resources/google-services.json");
        readme.Should().Contain("@capacitor-firebase/messaging");

        var agentPrompt = ReadEntry(archive, "AGENT_PROMPT.md");
        agentPrompt.Should().Contain("## Push notifications");
    }

    [Fact]
    public async Task BuildBundleAsync_PushNotificationsEnabled_BootstrapIosInjectsEntitlementsAndPbxprojWiring()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            PushNotificationsEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().Contain("UIBackgroundModes");
        iosBootstrap.Should().Contain("plutil -insert UIBackgroundModes -json '[\"remote-notification\"]'");
        iosBootstrap.Should().Contain("/usr/libexec/PlistBuddy -c \"Add :aps-environment string production\" ios/App/App/App.entitlements");
        iosBootstrap.Should().Contain("cp resources/GoogleService-Info.plist ios/App/App/GoogleService-Info.plist");
        iosBootstrap.Should().Contain("project.updateBuildProperty('CODE_SIGN_ENTITLEMENTS', 'App/App.entitlements');");
        iosBootstrap.Should().Contain("project.addResourceFile('App/GoogleService-Info.plist'");
        iosBootstrap.Should().Contain("didRegisterForRemoteNotificationsWithDeviceToken");
        iosBootstrap.Should().Contain("didFailToRegisterForRemoteNotificationsWithError");
    }

    [Fact]
    public async Task BuildBundleAsync_PushNotificationsEnabled_BootstrapAndroidPlacesGoogleServicesJson()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            PushNotificationsEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var androidBootstrap = ReadEntry(archive, "scripts/bootstrap-android.sh");
        androidBootstrap.Should().Contain("cp resources/google-services.json android/app/google-services.json");
    }

    [Fact]
    public async Task BuildBundleAsync_IosApp_SignsInThroughTheSystemSheet_WithAnHttpsCallbackOnTheSite()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "members.example.org" };

        var zipBytes = await service.BuildBundleAsync(tenant, new PrismMobileBundleRequest { AppName = "Test App", AppId = "com.example.test" });
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadPlatform(archive, "ios");
        iosBootstrap.Should().Contain("bridge?.registerPluginInstance(PrismSystemSignInPlugin())");
        iosBootstrap.Should().Contain("public let jsName = \"PrismSystemSignIn\"");
        iosBootstrap.Should().Contain("callback: .https(host: PrismShell.ownHost, path: path)");
        iosBootstrap.Should().Contain("session.prefersEphemeralWebBrowserSession = true");
        iosBootstrap.Should().Contain("<key>com.apple.developer.associated-domains</key>");
        iosBootstrap.Should().Contain("<string>webcredentials:members.example.org</string>");
        iosBootstrap.Should().Contain("project.updateBuildProperty('IPHONEOS_DEPLOYMENT_TARGET', '17.4');");
        iosBootstrap.Should().Contain("project.updateBuildProperty('CODE_SIGN_ENTITLEMENTS', 'App/App.entitlements');");
    }

    [Fact]
    public async Task BuildBundleAsync_AndroidApp_SignsInThroughAuthTab_WithAVerifiedAppLinkCallback()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "members.example.org" };

        var zipBytes = await service.BuildBundleAsync(tenant, new PrismMobileBundleRequest { AppName = "Test App", AppId = "com.example.test" });
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var androidBootstrap = ReadPlatform(archive, "android");
        androidBootstrap.Should().Contain("@CapacitorPlugin(name = \"PrismSystemSignIn\")");
        androidBootstrap.Should().Contain("plugins.add(PrismSystemSignInPlugin.class);");
        androidBootstrap.Should().Contain(".setEphemeralBrowsingEnabled(true)");
        androidBootstrap.Should().Contain("<intent-filter android:autoVerify=\"true\">");
        androidBootstrap.Should().Contain("android:host=\"members.example.org\" android:path=\"/umbraco/prism/mobile/auth/callback\"");
        androidBootstrap.Should().Contain("if (PrismSystemSignInPlugin.deliver(intent.getData())) return;");
        androidBootstrap.Should().Contain("androidx.browser:browser:1.9.0");
    }
}
