using FluentAssertions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismIdentityProviderHostsTests
{
    [Fact]
    public void Resolve_NullTenant_ReturnsEmpty()
    {
        PrismIdentityProviderHosts.Resolve(null).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_EntraTenant_ReturnsConcreteCiamHosts_NoWildcards()
    {
        var tenant = new PrismTenant { EntraTenantId = "my-tenant-id" };

        var hosts = PrismIdentityProviderHosts.Resolve(tenant);

        hosts.Should().Contain("login.microsoftonline.com");
        hosts.Should().Contain("my-tenant-id.ciamlogin.com");
        hosts.Should().Contain("my-tenant-id.b2clogin.com");
        hosts.Should().NotContain(h => h.Contains('*'));
    }

    [Fact]
    public void Resolve_GenericOidcTenant_ReturnsAuthorityHost()
    {
        var tenant = new PrismTenant { OidcAuthority = "http://localhost:8080/realms/prism-dev" };

        var hosts = PrismIdentityProviderHosts.Resolve(tenant);

        hosts.Should().ContainSingle().Which.Should().Be("localhost:8080");
    }

    [Fact]
    public void Resolve_TenantWithNeitherProviderConfigured_ReturnsEmpty()
    {
        var tenant = new PrismTenant();

        PrismIdentityProviderHosts.Resolve(tenant).Should().BeEmpty();
    }
}
