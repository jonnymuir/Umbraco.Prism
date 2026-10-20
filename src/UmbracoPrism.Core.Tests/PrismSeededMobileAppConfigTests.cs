using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Tests;

public class PrismSeededMobileAppConfigTests
{
    private const string Fingerprint = "36:49:A1:FF:BB:53:3D:30:53:25:29:D0:27:8B:CE:C0:90:66:8A:DD:02:BF:50:B2:26:49:D6:E0:89:8D:B9:A9";

    private static PrismMobileAppConfig Read(string? json) => JsonSerializer.Deserialize<PrismMobileAppConfig>(json!)!;

    [Fact]
    public void ConfiguredAppIdentities_ReplaceTheSavedOnes_AndKeepTheOtherMobileSettings()
    {
        var saved = JsonSerializer.Serialize(new PrismMobileAppConfig { AppName = "Members", AppId = "com.example.old", ErrorTitle = "Offline" });

        var merged = PrismSeededMobileAppConfig.Merge(saved, new PrismSeedMobileAppOptions
        {
            AppId = "com.example.members",
            AppleTeamId = "ABCDE12345",
            AndroidCertificateFingerprints = [Fingerprint]
        }, out var error);

        error.Should().BeNull();
        var config = Read(merged);
        config.AppName.Should().Be("Members");
        config.ErrorTitle.Should().Be("Offline");
        config.AppId.Should().Be("com.example.members");
        config.AppleTeamId.Should().Be("ABCDE12345");
        config.AndroidCertificateFingerprints.Should().Equal(Fingerprint);
    }

    [Fact]
    public void UnconfiguredValues_LeaveTheSavedOnes()
    {
        var saved = JsonSerializer.Serialize(new PrismMobileAppConfig { AppId = "com.example.members", AppleTeamId = "ABCDE12345" });

        var merged = PrismSeededMobileAppConfig.Merge(saved, new PrismSeedMobileAppOptions { AndroidCertificateFingerprints = [Fingerprint] }, out _);

        var config = Read(merged);
        config.AppId.Should().Be("com.example.members");
        config.AppleTeamId.Should().Be("ABCDE12345");
    }

    [Fact]
    public void NothingConfigured_LeavesTheSavedConfigAlone()
    {
        PrismSeededMobileAppConfig.Merge(null, new PrismSeedMobileAppOptions(), out var error).Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void InvalidConfiguredValues_ChangeNothing_AndSayWhy()
    {
        var saved = JsonSerializer.Serialize(new PrismMobileAppConfig { AppId = "com.example.members" });

        var merged = PrismSeededMobileAppConfig.Merge(saved, new PrismSeedMobileAppOptions
        {
            AndroidCertificateFingerprints = ["A8:EE:10:C3:1B:71:C6:D0:05:9F:95:99:26:54:DB:26:AD:03:93:E4"]
        }, out var error);

        merged.Should().Be(saved);
        error.Should().Contain("SHA-256");
    }

    [Fact]
    public void EnvironmentVariables_BindToTheSeededMobileApp()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Prism:SeedTenant:MobileApp:AppId"] = "com.example.members",
            ["Prism:SeedTenant:MobileApp:AppleTeamId"] = "ABCDE12345",
            ["Prism:SeedTenant:MobileApp:AndroidCertificateFingerprints:0"] = Fingerprint
        }).Build();

        var options = configuration.GetSection(PrismConfiguration.SectionName).Get<PrismConfiguration>()!.SeedTenant.MobileApp;

        options.AppId.Should().Be("com.example.members");
        options.AppleTeamId.Should().Be("ABCDE12345");
        options.AndroidCertificateFingerprints.Should().Equal(Fingerprint);
    }
}
