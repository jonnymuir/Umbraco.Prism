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

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().Contain("bridge?.registerPluginInstance(PrismIdentityCookiePlugin())");
        iosBootstrap.Should().Contain("class PrismIdentityCookiePlugin: CAPPlugin, CAPBridgedPlugin");
        iosBootstrap.Should().Contain("public let jsName = \"PrismIdentityCookiePlugin\"");
        iosBootstrap.Should().Contain("@objc func clearCookies(_ call: CAPPluginCall)");
        iosBootstrap.Should().Contain("let store = WKWebsiteDataStore.default()");
        iosBootstrap.Should().Contain("store.removeData(ofTypes: [WKWebsiteDataTypeCookies], for: matching)");

        var androidBootstrap = ReadEntry(archive, "scripts/bootstrap-android.sh");
        androidBootstrap.Should().Contain("JAVA_DIR=\"android/app/src/main/java/com/example/test\"");
        androidBootstrap.Should().Contain("$JAVA_DIR/PrismIdentityCookiePlugin.kt");
        androidBootstrap.Should().Contain("package com.example.test");
        androidBootstrap.Should().Contain("@CapacitorPlugin(name = \"PrismIdentityCookiePlugin\")");
        androidBootstrap.Should().Contain("cookieManager.removeAllCookies {");

        // MainActivity.java is rewritten wholesale (same precedent as AppDelegate.swift on iOS) —
        // asserts the registration actually happens, not just that the plugin file exists on
        // disk, the same class of gap the PrismBridgeViewController.swift pbxproj test above
        // guards against.
        androidBootstrap.Should().Contain("$JAVA_DIR/MainActivity.java");
        androidBootstrap.Should().Contain("registerPlugin(PrismIdentityCookiePlugin.class);");
        androidBootstrap.Should().Contain("super.onCreate(savedInstanceState);");
    }

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
    public async Task BuildBundleAsync_MobileDiagnosticsEnabled_BootstrapIosCompilesInDiagnosticsFlagTrue()
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            MobileDiagnosticsEnabled = true
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        iosBootstrap.Should().Contain("fileprivate enum PrismMobileDiagnosticsFlag {");
        iosBootstrap.Should().Contain("static let enabled = true");
        iosBootstrap.Should().Contain("UILongPressGestureRecognizer(target: hold, action: #selector(PrismNavigationHoldDelegate.handleDiagnosticsGesture(_:)))");

        // Reported live: a single-finger version, restricted to near the top of the screen, didn't
        // work at all there (that area isn't part of the app's own view hierarchy) and triggered
        // WebKit's own text-selection callout everywhere else. Two fingers, attached directly to
        // webView (not a screen region), fixes both.
        iosBootstrap.Should().Contain("diagnosticsGesture.numberOfTouchesRequired = 2");
        iosBootstrap.Should().Contain("webView.addGestureRecognizer(diagnosticsGesture)");
        iosBootstrap.Should().NotContain("webView.superview?.addGestureRecognizer(diagnosticsGesture)");
        iosBootstrap.Should().Contain("func gestureRecognizer(_ gestureRecognizer: UIGestureRecognizer, shouldRecognizeSimultaneouslyWith otherGestureRecognizer: UIGestureRecognizer) -> Bool");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task BuildBundleAsync_MobileDiagnosticsNotEnabled_BootstrapIosCompilesInDiagnosticsFlagFalse(bool? mobileDiagnosticsEnabled)
    {
        var service = new MobileBundleService();
        var tenant = new PrismTenantSchema { Id = 1, Name = "TestTenant", Hostname = "test.example" };
        var payload = new PrismMobileBundleRequest
        {
            AppName = "Test App",
            AppId = "com.example.test",
            MobileDiagnosticsEnabled = mobileDiagnosticsEnabled
        };

        var zipBytes = await service.BuildBundleAsync(tenant, payload);
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var iosBootstrap = ReadEntry(archive, "scripts/bootstrap-ios.sh");
        // The gating code itself is still always present — PrismMobileDiagnosticsFlag.enabled is a
        // plain build-time constant this reads, not code that's stripped out at the C# generation
        // level (see BuildBootstrapIosScript's own remarks on why: the alternative, splicing the
        // conditional straight into the middle of the surrounding raw string, risks colliding with
        // that string's own embedded JS, which already contains literal "}}" sequences). What must
        // differ is only the constant's value.
        iosBootstrap.Should().Contain("static let enabled = false");
        iosBootstrap.Should().NotContain("static let enabled = true");
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
        iosBootstrap.Should().Contain("maximum-scale=1, user-scalable=no, viewport-fit=cover");
        iosBootstrap.Should().Contain("forMainFrameOnly: false");
        iosBootstrap.Should().Contain("ios/App/App/PrismBridgeViewController.swift");

        // viewport-fit=cover is what makes env(safe-area-inset-top) resolve to anything but 0 in
        // the first place — needed unconditionally (not just for hosted content) because this
        // same script's own meta-tag overwrite was silently stripping the viewport-fit=cover
        // Master.cshtml already sets, on every page including this app's own, which is why
        // TestSite's own layout.css (env(safe-area-inset-top) on .portal-header/.dash-header)
        // hadn't actually been doing anything.
        iosBootstrap.Should().Contain("fileprivate enum PrismOwnHost");
        iosBootstrap.Should().Contain("static let value = \"test.example\"");

        // Reported live on the same Entra password screen: contentInset:'always' (a scroll-offset
        // setting) did NOT stop hosted content rendering under the status bar/notch — it only
        // offsets scroll position, which does nothing for content that doesn't scroll or that a
        // page positions outside normal flow. The real fix is one level up, in AppDelegate: wraps
        // the storyboard's own root view controller in a plain container via standard view
        // controller containment. Not done inside PrismBridgeViewController itself — confirmed
        // via Capacitor's own vendored source that its loadView() is `final` and unconditionally
        // does `view = webView`, so the view controller's `view` and its `webView` are the same
        // object; there's no separate webview-inside-a-container to reconstrain from in there.
        // Verified end-to-end via a real simulator install+launch against the actual Entra page
        // (not just this generator's own output): the title no longer overlaps the status
        // bar/Dynamic Island, and renders with its full text intact.
        //
        // Reported live afterward: with the top permanently safe-area-reserved regardless of
        // page, this app's own header rendered detached from the true top of the screen with a
        // plain blank gap above it. topAnchor now pins to the container's own top by default, and
        // a mutable constant (not the fixed 0 that alone would imply) is handed to
        // PrismSafeAreaTopCoordinator: reported live AGAIN afterward that a static top-or-not
        // choice, made once for whichever page happens to load first, isn't enough either — some
        // Entra screens (password entry, "pick an account", create-account, username entry)
        // still overlapped the status bar while others ("stay signed in") didn't, because those
        // are different client-side view states within ONE hosted page, not separate navigations.
        // PrismNavigationHoldDelegate is what actually toggles this constant, per real navigation
        // — see its own remarks for why that's reliable where a CSS fix pushed into hosted
        // content wasn't. bottomAnchor stays safe-area-pinned; nothing here was ever about the
        // home-indicator area.
        iosBootstrap.Should().Contain("let topConstraint = bridgeViewController.view.topAnchor.constraint(equalTo: container.view.topAnchor)");
        iosBootstrap.Should().Contain("bridgeViewController.view.bottomAnchor.constraint(equalTo: container.view.safeAreaLayoutGuide.bottomAnchor)");
        iosBootstrap.Should().Contain("PrismSafeAreaTopCoordinator.topConstraint = topConstraint");
        iosBootstrap.Should().Contain("PrismSafeAreaTopCoordinator.containerView = container.view");
        iosBootstrap.Should().Contain("window?.rootViewController = container");

        // Reported live: neither a CSS fix injected into hosted content, nor a safe-area choice
        // fixed once per page load, survives however many internal view-state changes a hosted
        // SPA-like flow (Entra's own sign-in screens) goes through afterward — this does, because
        // it's a property of the whole webview's own frame for that page's entire lifetime,
        // completely independent of the page's own DOM/CSS. Toggled on both didStartProvisional
        // Navigation AND didReceiveServerRedirectForProvisionalNavigation — the latter because a
        // server-side redirect (Entra hopping between its own subdomains) keeps the SAME
        // provisional-navigation lifecycle, so without it the reservation would stay stuck on
        // whichever host the ORIGINAL request targeted even after redirecting elsewhere.
        iosBootstrap.Should().Contain("enum PrismSafeAreaTopCoordinator");
        iosBootstrap.Should().Contain("static weak var topConstraint: NSLayoutConstraint?");
        iosBootstrap.Should().Contain("static weak var containerView: UIView?");
        iosBootstrap.Should().Contain("updateSafeAreaTopReservation(for: webView.url)");
        iosBootstrap.Should().Contain("func webView(_ webView: WKWebView, didReceiveServerRedirectForProvisionalNavigation navigation: WKNavigation!)");
        iosBootstrap.Should().Contain("private func updateSafeAreaTopReservation(for url: URL?)");
        iosBootstrap.Should().Contain("let isOwnHost = url?.host == PrismOwnHost.value");
        iosBootstrap.Should().Contain("let target: CGFloat = isOwnHost ? 0 : containerView.safeAreaInsets.top");

        // WKWebView shows a real blank gap between navigations. This closes it by caching a
        // snapshot of each page once it settles, then handing that already-resolved image over
        // synchronously the next time a navigation starts — no async snapshot call happens at the
        // moment it's needed, only when the previous page had time to settle first. The cached
        // frame is also refreshed while the user stays on a page: injected JS detects
        // input/change/scroll activity, debounced to one message per 100ms of quiet, and tells
        // the delegate to recapture — so a page the user has typed into or scrolled doesn't keep
        // showing its just-loaded frame for as long as they stay on it. A spinner is layered on
        // top regardless, revealed if the real navigation is slow enough to notice. Forwards every
        // other WKNavigationDelegate call straight through to Capacitor's own delegate (the
        // standard Cocoa decorator pattern) rather than reimplementing its own navigation
        // policy/redirect/auth-challenge handling by hand.
        iosBootstrap.Should().Contain("class PrismNavigationHoldDelegate: NSObject, WKNavigationDelegate");
        iosBootstrap.Should().Contain("webView.navigationDelegate = hold");
        iosBootstrap.Should().Contain("func forwardingTarget(for aSelector: Selector!) -> Any?");
        iosBootstrap.Should().Contain("private var lastGoodSnapshot: UIImage?");
        iosBootstrap.Should().Contain("private func scheduleSnapshotCapture(of webView: WKWebView, delay: TimeInterval, source: String)");
        iosBootstrap.Should().Contain("webView.takeSnapshot(with: nil)");
        iosBootstrap.Should().Contain("if let snapshot = lastGoodSnapshot {");
        iosBootstrap.Should().Contain("let spinner = UIActivityIndicatorView(style: .medium)");

        // Reported live: a dead-center spinner is easy to miss on a slow connection, because the
        // user's eyes are already on wherever they just tapped, not the screen's center. An
        // unconditional (not diagnostics-gated) tap-tracking gesture records where to anchor it
        // instead, falling back to dead-center when there's no recent-enough tap on record.
        iosBootstrap.Should().Contain("private var lastTapLocation: (point: CGPoint, at: Date)?");
        iosBootstrap.Should().Contain("let tapTracker = UITapGestureRecognizer(target: hold, action: #selector(PrismNavigationHoldDelegate.handleTapForSpinnerPositioning(_:)))");
        iosBootstrap.Should().Contain("tapTracker.cancelsTouchesInView = false");
        iosBootstrap.Should().Contain("@objc fileprivate func handleTapForSpinnerPositioning(_ recognizer: UITapGestureRecognizer)");
        iosBootstrap.Should().Contain("lastTapLocation = (recognizer.location(in: view), Date())");
        iosBootstrap.Should().Contain("if let tap = lastTapLocation, Date().timeIntervalSince(tap.at) < 2.0 {");
        iosBootstrap.Should().Contain("center = webView.convert(tap.point, to: hostView)");
        iosBootstrap.Should().Contain("spinner.center = center");
        iosBootstrap.Should().Contain("DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: capture)");
        iosBootstrap.Should().Contain("scheduleSnapshotCapture(of: webView, delay: 0.3, source: \"settle\")");
        iosBootstrap.Should().Contain("fileprivate func contentDidChange(in webView: WKWebView)");
        iosBootstrap.Should().Contain("scheduleSnapshotCapture(of: webView, delay: 0.1, source: \"change\")");

        // Cancelling the scheduled timer above doesn't stop a takeSnapshot call that's already in
        // flight — isCaptureInFlight guards against two overlapping captures; a trigger that
        // arrives mid-capture is deferred (captureNeededAfterInFlight) rather than starting a
        // second one or being silently dropped.
        iosBootstrap.Should().Contain("private var isCaptureInFlight = false");
        iosBootstrap.Should().Contain("private var captureNeededAfterInFlight = false");
        iosBootstrap.Should().Contain("guard !isCaptureInFlight else {");

        iosBootstrap.Should().Contain("customClass=\"PrismBridgeViewController\" customModule=\"App\"");
        iosBootstrap.Should().Contain("Main.storyboard");
        iosBootstrap.Should().Contain("import xcode from 'xcode'");
        iosBootstrap.Should().Contain("project.addSourceFile('App/PrismBridgeViewController.swift'");
        iosBootstrap.Should().Contain("registered in project.pbxproj");

        // Apple's own App Store Connect warning (90068) on the reference app's own recent uploads:
        // MinimumOSVersion 14.0 becomes unsubmittable from Spring 2027. Fixed generically here (not
        // just for this reference app's own CI pipeline) so every "Produce Mobile" consumer gets it.
        iosBootstrap.Should().Contain("project.updateBuildProperty('IPHONEOS_DEPLOYMENT_TARGET', '15.0')");

        // Root-caused from Capacitor's own vendored iOS source: a webViewConfiguration(for:)
        // override's returned WKWebViewConfiguration.userContentController gets discarded and
        // replaced wholesale with Capacitor's own internal one before the real webview is ever
        // built — confirmed live, previously, by a counter proving that override WAS called while
        // everything added to its content controller (this app's own viewport-zoom-fix for Entra's
        // hosted login page — a real product bug, not just a diagnostic gap — and paint-holding's
        // content-change detection) never actually ran. capacitorDidLoad() targets the real, live
        // controller directly instead.
        iosBootstrap.Should().Contain("class PrismBridgeViewController: CAPBridgeViewController, WKScriptMessageHandler");
        iosBootstrap.Should().Contain("override func capacitorDidLoad()");
        iosBootstrap.Should().Contain("let contentController = webView.configuration.userContentController");
        iosBootstrap.Should().Contain("contentController.addUserScript(viewportFixScript)");
        iosBootstrap.Should().Contain("contentController.add(WeakScriptMessageHandler(target: self), name: Self.viewportFixDiagnosticMessageName)");
        iosBootstrap.Should().Contain("class WeakScriptMessageHandler: NSObject, WKScriptMessageHandler");
        iosBootstrap.Should().Contain("private weak var target: WKScriptMessageHandler?");

        // TEMPORARY — confirms live that this fix actually reaches the real webview, the same way
        // an earlier version of this exact counter proved the previous injection point never did.
        iosBootstrap.Should().Contain("fileprivate static var viewportScriptPingCount = 0");
        iosBootstrap.Should().Contain("postMessage('viewport-ready')");
        iosBootstrap.Should().Contain("message.body as? String == \"viewport-ready\"");
        iosBootstrap.Should().Contain("vp=\\(PrismBridgeViewController.viewportScriptPingCount)");

        // Capacitor's own canonical JS-to-native bridge, replacing an earlier
        // WKScriptMessageHandler-based attempt for paint-holding's own content-change detection —
        // confirmed working via the exact same registerPluginInstance mechanism
        // @aparajita/capacitor-biometric-auth's own plugin already uses successfully in this app.
        // registerPluginType would silently no-op here (autoRegisterPlugins defaults to true and
        // is never overridden in this app) — registerPluginInstance always registers unconditionally.
        iosBootstrap.Should().Contain("bridge?.registerPluginInstance(PrismContentWatcherPlugin())");
        iosBootstrap.Should().Contain("class PrismContentWatcherPlugin: CAPPlugin, CAPBridgedPlugin");
        iosBootstrap.Should().Contain("public let jsName = \"PrismContentWatcher\"");
        iosBootstrap.Should().Contain("fileprivate static weak var activeHold: PrismNavigationHoldDelegate?");
        iosBootstrap.Should().Contain("fileprivate static var callCount = 0");
        iosBootstrap.Should().Contain("@objc func contentChanged(_ call: CAPPluginCall)");
        iosBootstrap.Should().Contain("Self.activeHold?.contentDidChange(in: webView)");
        iosBootstrap.Should().Contain("PrismContentWatcherPlugin.activeHold = hold");
        iosBootstrap.Should().Contain("plugin=\\(PrismContentWatcherPlugin.callCount)");

        // TEMPORARY — reported live: plugin= stayed at 0 despite genuine on-page interaction, even
        // though every step of the registration/JS-export/message-routing path was independently
        // confirmed correct against Capacitor's own vendored source. These pings isolate each
        // stage of prism-mobile-content-watcher.js's own execution through the same
        // already-proven-reliable WKScriptMessageHandler channel vp= uses, deliberately bypassing
        // the still-unproven plugin bridge itself, the same isolation technique that found the
        // capacitorDidLoad() root cause.
        iosBootstrap.Should().Contain("private static let contentWatcherDiagnosticMessageName = \"prismContentWatcherDiag\"");
        iosBootstrap.Should().Contain("fileprivate static var contentWatcherScriptStartedPingCount = 0");
        iosBootstrap.Should().Contain("fileprivate static var contentWatcherReadyPingCount = 0");
        iosBootstrap.Should().Contain("fileprivate static var contentWatcherMutationPingCount = 0");
        iosBootstrap.Should().Contain("fileprivate static var contentWatcherErrorPingCount = 0");
        iosBootstrap.Should().Contain("contentController.add(WeakScriptMessageHandler(target: self), name: Self.contentWatcherDiagnosticMessageName)");
        iosBootstrap.Should().Contain("case \"script-started\": Self.contentWatcherScriptStartedPingCount += 1");
        iosBootstrap.Should().Contain("case \"ready\": Self.contentWatcherReadyPingCount += 1");
        iosBootstrap.Should().Contain("case \"mutation\": Self.contentWatcherMutationPingCount += 1");
        iosBootstrap.Should().Contain("case \"error\": Self.contentWatcherErrorPingCount += 1");
        iosBootstrap.Should().Contain("ws=\\(PrismBridgeViewController.contentWatcherScriptStartedPingCount)");
        iosBootstrap.Should().Contain("wr=\\(PrismBridgeViewController.contentWatcherReadyPingCount)");
        iosBootstrap.Should().Contain("wm=\\(PrismBridgeViewController.contentWatcherMutationPingCount)");
        iosBootstrap.Should().Contain("we=\\(PrismBridgeViewController.contentWatcherErrorPingCount)");

        // Reported live: Sign Out bounces the whole app out to system Safari, landing on this
        // app's own /auth/logout, blank. Observes (never alters) Capacitor's own real
        // decidePolicyFor decision, logging exactly which URL/method got cancelled — the one thing
        // that can't be settled by reading Capacitor's source alone. UserDefaults, not an in-memory
        // property, since sign-out leaves the app entirely and may be force-quit while away.
        iosBootstrap.Should().Contain("func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void)");
        iosBootstrap.Should().Contain("private static let navigationDecisionLogKey = \"prism.diag.navigationDecisionLog\"");
        iosBootstrap.Should().Contain("private static func recordNavigationDecision(url: String, method: String, decision: String)");

        // Reported live: decidePolicyFor's own log showed nothing but "allow" during a sign-out
        // attempt that still ended up in system Safari, including for the logout POST itself — a
        // suspicious "allow GET about:blank" was the fingerprint of a window.open() call, which
        // goes through a completely different WebKit delegate method this app wasn't observing.
        iosBootstrap.Should().Contain("class PrismNavigationHoldDelegate: NSObject, WKNavigationDelegate, WKUIDelegate, UIGestureRecognizerDelegate");
        iosBootstrap.Should().Contain("private let uiTarget: WKUIDelegate?");
        iosBootstrap.Should().Contain("init(forwardingTo target: WKNavigationDelegate?, forwardingUIDelegateTo uiTarget: WKUIDelegate?, isHostTrustedInApp: @escaping (String) -> Bool)");
        iosBootstrap.Should().Contain("webView.uiDelegate = hold");

        // Reported live: a window.open()-style popup for this app's OWN /auth/logout — a URL a
        // plain top-level navigation was already handling correctly — still got sent to system
        // Safari, because Capacitor's own createWebViewWith has no allowlist check at all, unlike
        // decidePolicyFor. Reuses the SAME allowlist decidePolicyFor already trusts (bridge.config.
        // shouldAllowNavigation) rather than inventing a second one: a popup targeting one of this
        // app's own trusted hosts now loads in the same webview instead, regardless of what
        // triggered it; anything else still goes to Capacitor's own real uiDelegate unchanged.
        iosBootstrap.Should().Contain("isHostTrustedInApp: { [weak self] host in self?.bridge?.config.shouldAllowNavigation(to: host) ?? false }");
        iosBootstrap.Should().Contain("if let host = navigationAction.request.url?.host, isHostTrustedInApp(host) {");
        iosBootstrap.Should().Contain("Self.recordNavigationDecision(url: urlString, method: \"WINDOW.OPEN\", decision: \"IN-APP\")");
        iosBootstrap.Should().Contain("webView.load(navigationAction.request)");
        iosBootstrap.Should().Contain("Self.recordNavigationDecision(url: urlString, method: \"WINDOW.OPEN\", decision: \"EXTERNAL\")");
        iosBootstrap.Should().Contain("func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration, for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView?");
        iosBootstrap.Should().Contain("UserDefaults.standard.stringArray(forKey: navigationDecisionLogKey)");

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
        iosBootstrap.Should().NotContain("App.entitlements");

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
        iosBootstrap.Should().Contain("aps-environment");
        iosBootstrap.Should().Contain("<string>production</string>");
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
}
