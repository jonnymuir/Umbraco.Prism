using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// The opt-in strict audience rule for a generic OIDC tenant: with <c>Audience</c> configured, a business API
/// accepts only a token minted for that API. The default rule (no <c>Audience</c>) is covered in
/// <see cref="PrismAuthExtensionsSecurityTests"/> and is deliberately unchanged.
/// </summary>
[Collection(EnvVarSensitiveTestCollection.Name)]
public class PrismAuthStrictAudienceTests
{
    private const string Authority = "https://localhost:8443/realms/prism-dev";
    private const string Api = "prism-business-app";

    [Theory]
    [InlineData(Api, true)]               // minted for this API
    [InlineData("account", false)]        // issued to the web client for something else
    [InlineData("prism-client", false)]   // the web client's own ID token
    [InlineData("some-other-api", false)] // minted for a different API
    public void OnlyATokenMintedForTheConfiguredApiIsAccepted(string audience, bool expected)
    {
        var options = JwtOptionsFor(audienceSetting: Api);
        var token = new JwtSecurityToken(issuer: Authority, claims: [new Claim("aud", audience), new Claim("azp", "prism-client")]);

        options.TokenValidationParameters.AudienceValidator!([audience], token, options.TokenValidationParameters)
            .Should().Be(expected);
    }

    [Fact]
    public void WithNoAudienceConfigured_TheDefaultRuleStillAcceptsATokenIssuedToTheClient()
    {
        var options = JwtOptionsFor(audienceSetting: null);
        var token = new JwtSecurityToken(issuer: Authority, claims: [new Claim("aud", "account"), new Claim("azp", "prism-client")]);

        options.TokenValidationParameters.AudienceValidator!(["account"], token, options.TokenValidationParameters)
            .Should().BeTrue("strict mode is opt-in, so existing hosts are unaffected");
    }

    private static JwtBearerOptions JwtOptionsFor(string? audienceSetting)
    {
        var settings = new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:0:ClientId"] = "prism-client",
            ["PrismBusinessApp:Tenants:0:OidcAuthority"] = Authority,
            ["PrismBusinessApp:Tenants:0:Code"] = "PRISM-DEMO",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Demo (Keycloak)",
            ["PrismBusinessApp:Tenants:0:Audience"] = audienceSetting,
        };
        var services = new ServiceCollection();
        services.AddPrismAuthentication(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
    }
}
