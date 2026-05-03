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

    [Fact]
    public async Task GetCurrentAsync_SurfacesErrorEnvelope_WhenAuthHeaderIsNull()
    {
        // Arrange — simulates PrismContext.GetAuthorizationHeaderAsync returning null
        // (e.g. CurrentTenant not resolved by PrismTenantMiddleware).
        // The client must still send the request (without an Authorization header), and the
        // Business App JWT middleware will reject it with 401, which must surface as an
        // error envelope rather than being thrown.
        List<string?> capturedAuthHeaders = [];
        var prismContext = new Mock<IPrismContext>();
        prismContext
            .Setup(c => c.GetAuthorizationHeaderAsync(It.IsAny<bool>()))
            .ReturnsAsync((AuthenticationHeaderValue?)null);

        var client = BuildClientWithContextMock(
            config: new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "http://localhost:7001"
            },
            prismContext: prismContext,
            requestCapture: req => capturedAuthHeaders.Add(req.Headers.Authorization?.ToString()),
            responseStatus: HttpStatusCode.Unauthorized);

        // Act
        var envelope = await client.GetCurrentAsync("payment-demo");

        // Assert — the two requests (initial + 401 retry) must both lack an Authorization header
        capturedAuthHeaders.Should().NotBeEmpty();
        capturedAuthHeaders.Should().AllSatisfy(h => h.Should().BeNull(
            because: "when GetAuthorizationHeaderAsync returns null, no Authorization header should be sent"));
        envelope.ResponseState.Should().Be("error",
            because: "a 401 from the Business App must be surfaced as an error envelope, not thrown");
        envelope.Problems.Should().ContainSingle(p => p.Code == "BUSINESS_APP_ERROR",
            because: "HTTP 401 maps to the BUSINESS_APP_ERROR code");
    }

    [Fact]
    public async Task GetCurrentAsync_AttemptsTokenRefreshOnce_WhenBusinessAppReturns401()
    {
        // Arrange — Business App always returns 401, even with a valid token.
        // The client must attempt a single forced token refresh (forceRefresh: true)
        // before giving up; it must NOT retry more than once.
        List<bool> forceRefreshArgs = [];
        var prismContext = new Mock<IPrismContext>();
        prismContext
            .Setup(c => c.GetAuthorizationHeaderAsync(It.IsAny<bool>()))
            .Callback<bool>(forceRefreshArgs.Add)
            .ReturnsAsync(new AuthenticationHeaderValue("Bearer", "test-token"));

        var client = BuildClientWithContextMock(
            config: new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "http://localhost:7001"
            },
            prismContext: prismContext,
            responseStatus: HttpStatusCode.Unauthorized);

        // Act
        await client.GetCurrentAsync("payment-demo");

        // Assert
        forceRefreshArgs.Should().Contain(true,
            because: "a forced token refresh must be attempted when the Business App returns 401");
        forceRefreshArgs.Count(f => f).Should().Be(1,
            because: "the client must not retry more than once");
    }

    [Fact]
    public async Task GetCurrentAsync_SurfacesErrorEnvelope_NotExceptionThrown_WhenBothRequestsReturn401()
    {
        // Arrange — Business App returns 401 on both the initial request and the
        // forced-refresh retry. The error must surface as a BUSINESS_APP_ERROR envelope,
        // never as a thrown exception.
        var requestCount = 0;
        var client = BuildClient(
            config: new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "http://localhost:7001"
            },
            requestCapture: _ => Interlocked.Increment(ref requestCount),
            responseStatus: HttpStatusCode.Unauthorized);

        // Act
        var envelope = await client.GetCurrentAsync("payment-demo");

        // Assert
        requestCount.Should().Be(2, because: "the client must make exactly two attempts (initial + one retry)");
        envelope.ResponseState.Should().Be("error");
        envelope.Problems.Should().ContainSingle(p =>
            p.Code == "BUSINESS_APP_ERROR" && p.Message.Contains("401"),
            because: "the final 401 must surface as a BUSINESS_APP_ERROR envelope with the status code");
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
        var prismContext = new Mock<IPrismContext>();
        prismContext
            .Setup(c => c.GetAuthorizationHeaderAsync(It.IsAny<bool>()))
            .ReturnsAsync(new AuthenticationHeaderValue("Bearer", "test-token"));

        return BuildClientWithContextMock(config, prismContext, requestCapture, responseStatus, responseBody);
    }

    private static BusinessAppWorkflowClient BuildClientWithContextMock(
        Dictionary<string, string?> config,
        Mock<IPrismContext> prismContext,
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
