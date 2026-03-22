using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core.Tests;

public class PrismAuthExtensionsSecurityTests
{
    [Fact]
    public void IssuerValidator_RejectsIssuerHostMismatch_EvenWhenTenantIdAppearsInPath()
    {
        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBackOffice:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBackOffice:Tenants:0:ClientId"] = "client-a",
            ["PrismBackOffice:Tenants:0:Code"] = "ta",
            ["PrismBackOffice:Tenants:0:DisplayName"] = "Tenant A"
        });

        var token = CreateToken("tenant-a");

        var act = () => options.TokenValidationParameters.IssuerValidator!(
            "https://evil.example/tenant-a/v2.0",
            token,
            options.TokenValidationParameters);

        act.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public void AudienceValidator_RejectsAudienceBoundToDifferentConfiguredTenant()
    {
        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBackOffice:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBackOffice:Tenants:0:ClientId"] = "client-a",
            ["PrismBackOffice:Tenants:0:Code"] = "ta",
            ["PrismBackOffice:Tenants:0:DisplayName"] = "Tenant A",
            ["PrismBackOffice:Tenants:1:EntraTenantId"] = "tenant-b",
            ["PrismBackOffice:Tenants:1:ClientId"] = "client-b",
            ["PrismBackOffice:Tenants:1:Code"] = "tb",
            ["PrismBackOffice:Tenants:1:DisplayName"] = "Tenant B"
        });

        var token = CreateToken("tenant-a");

        var accepted = options.TokenValidationParameters.AudienceValidator!(
            ["client-b"],
            token,
            options.TokenValidationParameters);

        accepted.Should().BeFalse();
    }

    [Fact]
    public void AudienceValidator_AcceptsAudienceBoundToSameConfiguredTenant()
    {
        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBackOffice:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBackOffice:Tenants:0:ClientId"] = "client-a",
            ["PrismBackOffice:Tenants:0:Code"] = "ta",
            ["PrismBackOffice:Tenants:0:DisplayName"] = "Tenant A",
            ["PrismBackOffice:Tenants:1:EntraTenantId"] = "tenant-b",
            ["PrismBackOffice:Tenants:1:ClientId"] = "client-b",
            ["PrismBackOffice:Tenants:1:Code"] = "tb",
            ["PrismBackOffice:Tenants:1:DisplayName"] = "Tenant B"
        });

        var token = CreateToken("tenant-a");

        var accepted = options.TokenValidationParameters.AudienceValidator!(
            ["client-a"],
            token,
            options.TokenValidationParameters);

        accepted.Should().BeTrue();
    }

    private static JwtBearerOptions BuildJwtOptions(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddPrismAuthentication(configuration);

        var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        return optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private static JwtSecurityToken CreateToken(string tenantId)
    {
        var claims = new[] { new Claim("tid", tenantId) };
        return new JwtSecurityToken(claims: claims);
    }
}
