using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class TenantServiceIdentityValidationTests
{
    [Theory]
    [InlineData("http://idp.example.com/realms/a", null)]
    [InlineData("https://idp.example.com/realms/a?redirect=1", null)]
    [InlineData(null, "evil.example/#")]
    public async Task ARowWithAnUnacceptableIdentity_LosesItsSignIn_WhateverWroteIt(string? authority, string? entraTenantId)
    {
        var tenant = await ResolveAsync(new PrismTenantSchema
        {
            Id = 1,
            Name = "Tenant",
            Hostname = "tenant.example.com",
            OidcAuthority = authority,
            OidcClientId = authority is null ? null : "client",
            EntraTenantId = entraTenantId,
            EntraClientId = entraTenantId is null ? null : "client"
        });

        tenant.Should().NotBeNull("the hostname still resolves so the site can answer");
        tenant!.HasSignInConfigured().Should().BeFalse("sign-in must fail closed rather than reach an unvalidated address");
    }

    [Fact]
    public async Task ARowThatResolvesToAnUnacceptableAuthority_ThroughAToken_LosesItsSignIn()
    {
        var tenant = await ResolveAsync(
            new PrismTenantSchema
            {
                Id = 1,
                Name = "Tenant",
                Hostname = "tenant.example.com",
                OidcAuthority = "{{TENANT_IDP}}",
                OidcClientId = "client"
            },
            new Dictionary<string, string?> { ["TENANT_IDP"] = "http://idp.example.com/realms/a" });

        tenant!.HasSignInConfigured().Should().BeFalse();
    }

    [Fact]
    public async Task AnAcceptableRow_KeepsItsSignIn()
    {
        var tenant = await ResolveAsync(new PrismTenantSchema
        {
            Id = 1,
            Name = "Tenant",
            Hostname = "tenant.example.com",
            OidcAuthority = "https://idp.example.com/realms/a",
            OidcClientId = "client"
        });

        tenant!.HasSignInConfigured().Should().BeTrue();
    }

    private static async Task<UmbracoPrism.Core.Models.PrismTenant?> ResolveAsync(PrismTenantSchema schema, Dictionary<string, string?>? configuration = null)
    {
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>())).Returns(schema);
        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var appCaches = new AppCaches(
            new ObjectCacheAppCache(),
            new Mock<IRequestCache>().Object,
            new IsolatedCaches(_ => new ObjectCacheAppCache()));
        var resolver = new TenantTokenResolver(new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build());
        var service = new TenantService(dbFactory.Object, appCaches, resolver, NullLogger<TenantService>.Instance);

        return await service.GetByDomainAsync("tenant.example.com");
    }
}
