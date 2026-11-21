using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core.Tests;

public class PrismBearerConfigurationCheckTests
{
    private static async Task StartWith(params (string Code, string? Authority, string? Audience)[] tenants)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < tenants.Length; i++)
        {
            settings[$"PrismBusinessApp:Tenants:{i}:EntraTenantId"] = "";
            settings[$"PrismBusinessApp:Tenants:{i}:ClientId"] = "client";
            settings[$"PrismBusinessApp:Tenants:{i}:Code"] = tenants[i].Code;
            settings[$"PrismBusinessApp:Tenants:{i}:DisplayName"] = tenants[i].Code;
            settings[$"PrismBusinessApp:Tenants:{i}:OidcAuthority"] = tenants[i].Authority;
            settings[$"PrismBusinessApp:Tenants:{i}:Audience"] = tenants[i].Audience;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        await new PrismBearerConfigurationCheck(configuration).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AnOidcTenantWithNoAudience_StopsStartup_NamingIt()
    {
        var act = () => StartWith(("GOOD", "https://idp.example.com/a", "api-a"), ("BAD", "https://idp.example.com/b", null));

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*BAD*");
    }

    [Fact]
    public async Task OidcTenantsWithAnAudience_AndEntraTenants_StartNormally() =>
        await StartWith(("OIDC", "https://idp.example.com/a", "api-a"), ("ENTRA", null, null));
}
