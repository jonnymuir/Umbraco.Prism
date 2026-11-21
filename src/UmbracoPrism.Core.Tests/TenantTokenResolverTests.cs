using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class TenantTokenResolverTests
{
    private static TenantTokenResolver Create(Dictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void AnOrdinaryToken_Resolves()
    {
        var resolver = Create(new() { ["TENANT_A_HOST"] = "a.example.com" });

        resolver.Resolve("{{TENANT_A_HOST}}").Should().Be("a.example.com");
    }

    [Theory]
    [InlineData("DB_PASSWORD")]
    [InlineData("OIDC_CLIENT_SECRET")]
    [InlineData("STORAGE_ACCOUNT_KEY")]
    [InlineData("SOME_API_TOKEN")]
    [InlineData("CONNECTIONSTRINGS_UMBRACODBDSN")]
    [InlineData("ASPNETCORE_URLS")]
    [InlineData("AZURE_CLIENT_ID")]
    public void ATokenThatLooksSensitive_NeverResolves(string name)
    {
        var resolver = Create(new() { [name] = "super-sensitive" });

        resolver.Resolve($"https://host.example/{{{{{name}}}}}").Should().Be($"https://host.example/{{{{{name}}}}}",
            "a tenant field must not be able to read or send out a credential");
    }

    [Fact]
    public void WhenAllowedPrefixesAreConfigured_OnlyThoseTokensResolve()
    {
        var resolver = Create(new()
        {
            ["Prism:TenantTokens:AllowedPrefixes:0"] = "PRISM_TENANT_",
            ["PRISM_TENANT_HOST"] = "a.example.com",
            ["OTHER_HOST"] = "b.example.com"
        });

        resolver.Resolve("{{PRISM_TENANT_HOST}}").Should().Be("a.example.com");
        resolver.Resolve("{{OTHER_HOST}}").Should().Be("{{OTHER_HOST}}");
    }

    [Fact]
    public void TokenStatus_NeverReturnsTheValueOfABlockedToken()
    {
        var resolver = Create(new() { ["DB_PASSWORD"] = "super-sensitive", ["TENANT_A_HOST"] = "a.example.com" });
        var tenant = new PrismTenantSchema { OidcAuthority = "https://{{TENANT_A_HOST}}/{{DB_PASSWORD}}" };

        var results = resolver.ExtractTokenStatus(tenant);

        results.Single(r => r.TokenName == "DB_PASSWORD").Should().Match<TenantTokenResult>(
            r => r.IsBlocked && !r.IsResolved && r.ResolvedValue == null);
        results.Single(r => r.TokenName == "TENANT_A_HOST").ResolvedValue.Should().Be("a.example.com");
    }
}
