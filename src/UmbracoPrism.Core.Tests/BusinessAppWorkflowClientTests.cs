using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Verifies that <see cref="BusinessAppWorkflowClient"/> routes server-to-server
/// workflow calls through the internal backchannel when running in Codespaces,
/// and falls back to the configured public URL otherwise.
/// </summary>
[Collection(EnvVarSensitiveTestCollection.Name)]
public class BusinessAppWorkflowClientTests : IDisposable
{
    private readonly string? _savedBackchannelUrl;

    public BusinessAppWorkflowClientTests()
    {
        _savedBackchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL", _savedBackchannelUrl);
    }

    [Fact]
    public async Task GetCurrentAsync_UsesBackchannelUrl_WhenEnvVarIsSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:54321");

        HttpRequestMessage? captured = null;
        var client = BuildClient(
            config: new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://codespace-7245.app.github.dev"
            },
            requestCapture: req => captured = req);

        // Act
        await client.GetCurrentAsync("payment-demo");

        // Assert
        captured.Should().NotBeNull();
        captured!.RequestUri!.ToString().Should().StartWith("http://localhost:54321/",
            because: "workflow API calls must use the internal backchannel in Codespaces to bypass the GitHub forwarded-port proxy");
    }

    [Fact]
    public async Task GetCurrentAsync_UsesConfiguredPublicUrl_WhenBackchannelEnvVarIsAbsent()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL", null);

        HttpRequestMessage? captured = null;
        var client = BuildClient(
            config: new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            requestCapture: req => captured = req);

        // Act
        await client.GetCurrentAsync("payment-demo");

        // Assert
        captured.Should().NotBeNull();
        captured!.RequestUri!.ToString().Should().StartWith("https://localhost:7245/",
            because: "when BUSINESSAPP_BACKCHANNEL_URL is absent the configured public URL should be used");
    }

    [Fact]
    public async Task AdvanceAsync_UsesBackchannelUrl_WhenEnvVarIsSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:54321");

        HttpRequestMessage? captured = null;
        var client = BuildClient(
            config: new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://codespace-7245.app.github.dev"
            },
            requestCapture: req => captured = req);

        // Act
        await client.AdvanceAsync("payment-demo", "instance-1", "submit", 1);

        // Assert
        captured.Should().NotBeNull();
        captured!.RequestUri!.ToString().Should().StartWith("http://localhost:54321/",
            because: "advance calls must also route through the internal backchannel in Codespaces");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static BusinessAppWorkflowClient BuildClient(
        Dictionary<string, string?> config,
        Action<HttpRequestMessage>? requestCapture = null,
        HttpStatusCode responseStatus = HttpStatusCode.OK,
        string responseBody = """{"instanceId":"inst-1","responseState":"render","stateVersion":1,"correlationId":"c1","serverTimeUtc":"2026-01-01T00:00:00Z","render":{"stepType":"form","stateDisplayName":"Step 1","components":[],"availableActions":[]}}""")
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            requestCapture?.Invoke(req);
            return new HttpResponseMessage(responseStatus)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(f => f.CreateClient("PrismBusinessApp")).Returns(httpClient);

        var prismContext = new Mock<IPrismContext>();
        prismContext
            .Setup(c => c.GetAuthorizationHeaderAsync(It.IsAny<bool>()))
            .ReturnsAsync(new AuthenticationHeaderValue("Bearer", "test-token"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        return new BusinessAppWorkflowClient(
            clientFactory.Object,
            configuration,
            prismContext.Object,
            NullLogger<BusinessAppWorkflowClient>.Instance);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }
}
