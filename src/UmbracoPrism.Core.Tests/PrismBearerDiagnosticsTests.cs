using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Logging;
using Moq;

namespace UmbracoPrism.Core.Tests;

public class PrismBearerDiagnosticsTests
{
    [Fact]
    public void AClaimWithLineBreaks_CannotForgeALogLine()
    {
        var token = Token(new { alg = "RS256", kid = "k\nforged" }, new { iss = "https://idp\r\nINFO forged entry", azp = "a" });

        var (issuer, _, keyId) = PrismBearerDiagnostics.ReadUnvalidatedClaims(token);

        PrismBearerDiagnostics.Clean(issuer).Should().NotContain("\n").And.NotContain("\r");
        PrismBearerDiagnostics.Clean(keyId).Should().NotContain("\n");
    }

    [Fact]
    public void AnOversizedClaim_IsCutShort() =>
        PrismBearerDiagnostics.Clean(new string('x', 5000)).Should().HaveLength(200);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???.###")]
    public void AnUnparseableToken_YieldsNothing_AndDoesNotThrow(string? token) =>
        PrismBearerDiagnostics.ReadUnvalidatedClaims(token).Should().Be((null, null, null));

    [Fact]
    public void TheFailureLine_NamesTheClaims_ButNotTheServersConfiguration()
    {
        var logger = new CapturingLogger();
        var token = Token(new { alg = "RS256", kid = "kid-1" }, new { iss = "https://idp.example/realms/a", azp = "web" });

        PrismBearerDiagnostics.LogFailure(logger, new SecurityTokenInvalidIssuerException("x"), token);

        logger.Lines.Should().ContainSingle().Which.Should()
            .Contain("https://idp.example/realms/a").And.Contain("kid-1").And.Contain("SecurityTokenInvalidIssuerException")
            .And.NotContain("ASPNETCORE_ENVIRONMENT").And.NotContain("KEYCLOAK_BACKCHANNEL_URL");
    }

    [Fact]
    public void TheBearerHandler_OnlyAcceptsAsymmetricAlgorithms()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddPrismAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var algorithms = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters.ValidAlgorithms;

        algorithms.Should().NotBeNullOrEmpty();
        algorithms.Should().Contain(SecurityAlgorithms.RsaSha256);
        algorithms.Should().NotContain(a => a.StartsWith("HS", StringComparison.Ordinal) || a == "none");
    }

    [Theory]
    [InlineData("{\"error\":\"invalid_grant\",\"error_description\":\"Code not valid\"}", "invalid_grant Code not valid")]
    [InlineData("", "(empty body)")]
    [InlineData("<html>proxy error</html>", "(unparseable body)")]
    [InlineData("{\"other\":1}", "(no error field)")]
    public void TheTokenErrorLogged_IsTheOAuthErrorNotTheBody(string body, string expected) =>
        UmbracoPrism.Core.Auth.PrismOidcFlowHelpers.DescribeTokenError(body).Should().Be(expected);

    private static string Token(object header, object payload)
    {
        static string Encode(object value) =>
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(value)));
        return $"{Encode(header)}.{Encode(payload)}.signature";
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
