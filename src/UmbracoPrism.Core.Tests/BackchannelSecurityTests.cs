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

/// <summary>
/// Security regression tests for KEYCLOAK_BACKCHANNEL_URL feature.
/// Ensures backchannel URL does not bypass critical security validations.
/// </summary>
public class BackchannelSecurityTests
{
    private const string OidcAuthority = "https://keycloak.example.com/realms/prism-dev";
    private const string BackchannelUrl = "http://localhost:8080";

    [Fact]
    public void IssuerValidator_RejectsIssuerMismatch_WhenBackchannelUrlIsSet()
    {
        // SECURITY: Setting KEYCLOAK_BACKCHANNEL_URL changes where metadata is fetched from,
        // but must NOT bypass issuer claim validation. A token with mismatched issuer should
        // still be rejected even when backchannel URL is configured.

        using var envVar = new TempEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:OidcAuthority"] = OidcAuthority,
            ["PrismBusinessApp:Tenants:0:OidcClientId"] = "test-client"
        });

        var token = CreateToken(OidcAuthority);

        // Attempt to validate a token with a completely different issuer - should reject
        var act = () => options.TokenValidationParameters.IssuerValidator!(
            "https://evil.example.com/realms/bad-realm",
            token,
            options.TokenValidationParameters);

        act.Should().Throw<SecurityTokenInvalidIssuerException>(
            "because KEYCLOAK_BACKCHANNEL_URL should only affect metadata fetch location, not issuer validation");
    }

    private static JwtBearerOptions BuildJwtOptions(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var services = new ServiceCollection();
        services.AddPrismAuthentication(configuration);
        var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        return optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private static JwtSecurityToken CreateToken(string issuer)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: "test-client",
            subject: new ClaimsIdentity(new[] { new Claim("sub", "user123") }),
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddHours(1));
        return token;
    }

    private class TempEnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public TempEnvironmentVariable(string name, string value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}
