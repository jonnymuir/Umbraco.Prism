using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

[Collection(EnvVarSensitiveTestCollection.Name)]
public class PrismOidcDiscoveryTests : IDisposable
{
    private readonly string? _backchannel = Environment.GetEnvironmentVariable(PrismBackchannel.VariableName);

    public PrismOidcDiscoveryTests() =>
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, null);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, _backchannel);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetAsync_ReadsEndpointsFromTheDocument_NotFromAVendorPath()
    {
        var discovery = CreateDiscovery(new
        {
            issuer = "https://idp.example/oauth2/default",
            authorization_endpoint = "https://idp.example/oauth2/default/v1/authorize",
            token_endpoint = "https://idp.example/oauth2/default/v1/token",
            end_session_endpoint = "https://idp.example/oauth2/default/v1/logout",
            jwks_uri = "https://idp.example/oauth2/default/v1/keys"
        });

        var endpoints = await discovery.GetAsync("https://idp.example/oauth2/default/");

        endpoints.AuthorizationEndpoint.Should().Be("https://idp.example/oauth2/default/v1/authorize");
        endpoints.TokenEndpoint.Should().Be("https://idp.example/oauth2/default/v1/token");
        endpoints.EndSessionEndpoint.Should().Be("https://idp.example/oauth2/default/v1/logout");
    }

    [Fact]
    public async Task GetAsync_Rejects_ADocumentWhoseIssuerIsNotTheConfiguredAuthority()
    {
        var discovery = CreateDiscovery(new
        {
            issuer = "https://evil.example/realms/a",
            authorization_endpoint = "https://evil.example/auth",
            token_endpoint = "https://evil.example/token",
            jwks_uri = "https://evil.example/keys"
        });

        var act = () => discovery.GetAsync("https://idp.example/realms/a");

        await act.Should().ThrowAsync<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public async Task GetAsync_ReturnsNoEndSession_WhenTheProviderHasNone()
    {
        var discovery = CreateDiscovery(new
        {
            issuer = "https://idp.example/realms/a",
            authorization_endpoint = "https://idp.example/auth",
            token_endpoint = "https://idp.example/token",
            jwks_uri = "https://idp.example/keys"
        });

        var endpoints = await discovery.GetAsync("https://idp.example/realms/a");

        endpoints.EndSessionEndpoint.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Rejects_AnAuthorityThatIsNotAnAbsoluteUrl()
    {
        var discovery = CreateDiscovery(new { });

        var act = () => discovery.GetAsync("not a url");

        await act.Should().ThrowAsync<SecurityTokenException>();
    }

    [Fact]
    public void Rewrite_MovesAnEndpointOntoTheBackchannelHost_OnlyInDevelopment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable(PrismBackchannel.VariableName, "http://localhost:8080");

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            PrismBackchannel.Rewrite("https://idp.example/realms/a/token").Should().Be("https://idp.example/realms/a/token");

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            PrismBackchannel.Rewrite("https://idp.example/realms/a/token").Should().Be("http://localhost:8080/realms/a/token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        }
    }

    private static PrismOidcDiscovery CreateDiscovery(object document)
    {
        var json = JsonSerializer.Serialize(document);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
        return new PrismOidcDiscovery(factory.Object);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
