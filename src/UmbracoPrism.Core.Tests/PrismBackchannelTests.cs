using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core.Tests;

[Collection(EnvVarSensitiveTestCollection.Name)]
public class PrismBackchannelTests : IDisposable
{
    private readonly string? _backchannel = Environment.GetEnvironmentVariable(PrismBackchannel.VariableName);
    private readonly string? _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, _backchannel);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _environment);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData(null)]
    public void GetBase_ReturnsNull_OutsideDevelopment(string? environment)
    {
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, "http://localhost:8080");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

        PrismBackchannel.GetBase().Should().BeNull();
    }

    [Fact]
    public void GetBase_ReturnsTheTrimmedValue_InDevelopment()
    {
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, "http://localhost:8080/");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        PrismBackchannel.GetBase().Should().Be("http://localhost:8080");
    }

    [Fact]
    public void GetBase_ReturnsNull_WhenNotSet()
    {
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, null);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        PrismBackchannel.GetBase().Should().BeNull();
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public void ThrowIfMisconfigured_ThrowsOnlyOutsideDevelopment(string environmentName, bool shouldThrow)
    {
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, "http://localhost:8080");
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);

        var act = () => PrismBackchannel.ThrowIfMisconfigured(environment.Object);

        if (shouldThrow)
        {
            act.Should().Throw<InvalidOperationException>();
        }
        else
        {
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void ThrowIfMisconfigured_DoesNotThrow_WhenTheVariableIsNotSet()
    {
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, null);
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Production");

        var act = () => PrismBackchannel.ThrowIfMisconfigured(environment.Object);

        act.Should().NotThrow();
    }
}
