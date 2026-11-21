using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core.Tests;

public class PrismDataProtectionGuardTests
{
    [Fact]
    public void FrameworkDefaults_AreReportedAsTwoProblems() =>
        PrismDataProtectionGuard.Inspect(new KeyManagementOptions()).Should().HaveCount(2);

    [Fact]
    public void AHostThatChoseStorageAndProtection_HasNoProblems()
    {
        var options = new KeyManagementOptions
        {
            XmlRepository = Mock.Of<IXmlRepository>(),
            XmlEncryptor = Mock.Of<IXmlEncryptor>()
        };

        PrismDataProtectionGuard.Inspect(options).Should().BeEmpty();
    }

    [Fact]
    public async Task OutsideDevelopment_WithRequireSet_StartupFailsOnDefaults()
    {
        var guard = Create("Production", requireConfigured: true);

        var act = () => guard.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Data Protection*");
    }

    [Fact]
    public async Task OutsideDevelopment_WithoutRequire_StartupContinues()
    {
        var guard = Create("Production", requireConfigured: false);

        await guard.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task InDevelopment_NothingIsChecked()
    {
        var guard = Create("Development", requireConfigured: true);

        await guard.StartAsync(CancellationToken.None);
    }

    private static PrismDataProtectionGuard Create(string environmentName, bool requireConfigured)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [PrismDataProtectionGuard.RequireConfigurationKey] = requireConfigured.ToString() })
            .Build();
        return new PrismDataProtectionGuard(
            Options.Create(new KeyManagementOptions()),
            environment.Object,
            configuration,
            NullLogger<PrismDataProtectionGuard>.Instance);
    }
}
