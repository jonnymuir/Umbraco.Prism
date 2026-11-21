using AwesomeAssertions;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismTenantValidatorTests
{
    [Theory]
    [InlineData("https://idp.example.com/realms/members")]
    [InlineData("https://login.example.com/oauth2/default/")]
    [InlineData("https://203.0.113.10/realms/members")]
    public void AnOrdinaryPublicHttpsAuthority_IsAccepted(string authority) =>
        PrismTenantValidator.Validate(authority, null, allowDemo: false).Should().BeNull();

    [Theory]
    [InlineData("http://idp.example.com/realms/a", "must use https")]
    [InlineData("ftp://idp.example.com/realms/a", "must use https")]
    [InlineData("not a url", "absolute URL")]
    [InlineData("https://user:pass@idp.example.com/realms/a", "credentials")]
    [InlineData("https://idp.example.com/realms/a?x=1", "query string")]
    [InlineData("https://idp.example.com/realms/a#frag", "fragment")]
    [InlineData("https://localhost:8443/realms/a", "public host")]
    [InlineData("https://127.0.0.1/realms/a", "public host")]
    [InlineData("https://169.254.169.254/latest/meta-data", "public host")]
    [InlineData("https://10.0.0.5/realms/a", "public host")]
    [InlineData("https://172.20.1.1/realms/a", "public host")]
    [InlineData("https://192.168.1.1/realms/a", "public host")]
    [InlineData("https://[::1]/realms/a", "public host")]
    [InlineData("https://[fe80::1]/realms/a", "public host")]
    [InlineData("https://[fd00::1]/realms/a", "public host")]
    [InlineData("https://[::ffff:10.0.0.5]/realms/a", "public host")]
    [InlineData("https://keycloak/realms/a", "public host")]
    [InlineData("https://idp.internal/realms/a", "public host")]
    public void AnUnsafeAuthority_IsRefused_OutsideDemoMode(string authority, string expectedFragment) =>
        PrismTenantValidator.Validate(authority, null, allowDemo: false).Should().Contain(expectedFragment);

    [Theory]
    [InlineData("https://localhost:8443/realms/prism-dev")]
    [InlineData("http://localhost:8080/realms/prism-dev")]
    public void ALoopbackAuthority_IsAccepted_InDemoMode(string authority) =>
        PrismTenantValidator.Validate(authority, null, allowDemo: true).Should().BeNull();

    [Fact]
    public void PlainHttp_IsRefused_ForAPublicHost_EvenInDemoMode() =>
        PrismTenantValidator.Validate("http://idp.example.com/realms/a", null, allowDemo: true).Should().Contain("https");

    [Theory]
    [InlineData("0b7d3c5e-1a2b-4c3d-8e9f-001122334455")]
    [InlineData("contoso")]
    [InlineData("tenant-1")]
    public void AnEntraTenantId_ThatIsAGuidOrDnsLabel_IsAccepted(string id) =>
        PrismTenantValidator.Validate(null, id, allowDemo: false).Should().BeNull();

    [Theory]
    [InlineData("evil.example/#")]
    [InlineData("evil.example")]
    [InlineData("a/b")]
    [InlineData("tenant@evil")]
    [InlineData("-leading")]
    public void AnEntraTenantId_ThatCouldRedirectTheHost_IsRefused(string id) =>
        PrismTenantValidator.Validate(null, id, allowDemo: false).Should().Contain("Entra tenant id");

    [Fact]
    public void NothingConfigured_IsAccepted() =>
        PrismTenantValidator.Validate(null, null, allowDemo: false).Should().BeNull();
}
