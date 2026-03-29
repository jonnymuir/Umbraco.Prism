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

        var config = ReadEntry(archive, "capacitor.config.ts");
        config.Should().Contain("appId: 'com.example.northwind'");
        config.Should().Contain("appName: 'Northwind Mobile'");
        config.Should().Contain("appendUserAgent: 'PrismMobile'");
        config.Should().Contain("contentInset: 'automatic'");
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
}
