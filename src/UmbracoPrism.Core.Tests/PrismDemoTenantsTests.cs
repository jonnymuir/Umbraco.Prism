using AwesomeAssertions;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismDemoTenantsTests
{
    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("development", null, true)]
    [InlineData("Production", null, false)]
    [InlineData("Staging", "false", false)]
    [InlineData(null, null, false)]
    [InlineData("Production", "true", true)]
    [InlineData("Production", " TRUE ", true)]
    [InlineData("Production", "yes", false)]
    public void IsAllowedFor_OnlyInDevelopment_OrWhenExplicitlyOptedIn(string? environment, string? optIn, bool expected) =>
        PrismDemoTenants.IsAllowedFor(environment, optIn).Should().Be(expected);
}
