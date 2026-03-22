using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismTokenRefreshServiceTests
{
    // ------------------------------------------------------------------ helpers

    private static PrismTokenRefreshService BuildService(
        StubHttpMessageHandler handler,
        PrismTokenRefreshOptions? opts = null)
    {
        opts ??= FastOptions();

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        return new PrismTokenRefreshService(
            mockFactory.Object,
            Options.Create(opts),
            NullLogger<PrismTokenRefreshService>.Instance);
    }

    /// <summary>
    /// Options with zero delay and configurable thresholds for fast tests.
    /// MaxRetryAttempts is clamped to a minimum of 1 (Polly v8 requirement).
    /// </summary>
    private static PrismTokenRefreshOptions FastOptions(int maxRetries = 3, int cbMinThroughput = 5) =>
        new()
        {
            MaxRetryAttempts = Math.Max(1, maxRetries),
            InitialBackoffSeconds = 0,          // no delay between retries in tests
            CircuitBreakerMinimumThroughput = cbMinThroughput,
            CircuitBreakerFailureRatio = 1.0,
            CircuitBreakerSamplingWindowSeconds = 60,
            CircuitBreakerBreakDurationSeconds = 600
        };

    private static StringContent SuccessTokenJson() =>
        new("""{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600}""",
            Encoding.UTF8, "application/json");

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task RefreshAsync_ReturnsSuccess_OnFirstAttempt()
    {
        var handler = StubHttpMessageHandler.Sequential(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = SuccessTokenJson() });

        var service = BuildService(handler);

        var result = await service.RefreshAsync("https://example.com/token", new Dictionary<string, string>());

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().Be("new-refresh");
        result.ExpiresIn.Should().Be(3600);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RefreshAsync_RetriesOnTransientFailure_AndSucceedsAfterRetry()
    {
        // Two 503s then 200 — should succeed on the third attempt (two retries used).
        var handler = StubHttpMessageHandler.Sequential(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = SuccessTokenJson() });

        var service = BuildService(handler, FastOptions(maxRetries: 3));

        var result = await service.RefreshAsync("https://example.com/token", new Dictionary<string, string>());

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("new-access");
        handler.CallCount.Should().Be(3, because: "503, 503, 200 — two retries consumed");
    }

    [Fact]
    public async Task RefreshAsync_ReturnsFailure_WhenAllRetriesExhausted()
    {
        // All responses are 503; retries exhausted → failure.
        // Factory creates a fresh HttpResponseMessage on every handler call to avoid shared-instance issues.
        var handler = StubHttpMessageHandler.Always(HttpStatusCode.ServiceUnavailable);
        var service = BuildService(handler, FastOptions(maxRetries: 3));

        var result = await service.RefreshAsync("https://example.com/token", new Dictionary<string, string>());

        result.Success.Should().BeFalse();
        // 1 initial + 3 retries = 4 total handler calls.
        handler.CallCount.Should().Be(4, because: "MaxRetryAttempts=3 means 1 initial + 3 retries");
    }

    [Fact]
    public async Task RefreshAsync_CircuitBreaker_OpensAfterThresholdFailures()
    {
        // MinimumThroughput=3, FailureRatio=1.0 → circuit opens after 3 failing outer calls.
        // MaxRetryAttempts=1 (min allowed by Polly v8): each outer call produces 2 handler invocations.
        const int threshold = 3;
        const int maxRetries = 1; // 1 initial + 1 retry = 2 handler calls per outer attempt
        const int handlerCallsPerOuterAttempt = maxRetries + 1;

        var handler = StubHttpMessageHandler.Always(HttpStatusCode.ServiceUnavailable);
        var service = BuildService(handler, FastOptions(maxRetries: maxRetries, cbMinThroughput: threshold));

        // Trip the circuit with `threshold` consecutive failing outer calls.
        for (var i = 0; i < threshold; i++)
        {
            var r = await service.RefreshAsync("https://example.com/token", new Dictionary<string, string>());
            r.Success.Should().BeFalse();
        }

        handler.CallCount.Should().Be(
            threshold * handlerCallsPerOuterAttempt,
            because: $"{threshold} outer calls × {handlerCallsPerOuterAttempt} handler calls each");

        // Circuit is now open — next call must fail WITHOUT hitting the handler.
        var afterOpen = await service.RefreshAsync("https://example.com/token", new Dictionary<string, string>());
        afterOpen.Success.Should().BeFalse();
        handler.CallCount.Should().Be(
            threshold * handlerCallsPerOuterAttempt,
            because: "circuit breaker is open; handler must not be called again");
    }

    [Fact]
    public async Task RefreshAsync_DoesNotRetry_On4xxClientError()
    {
        // 400 Bad Request means invalid refresh token; retrying is pointless.
        var handler = StubHttpMessageHandler.Always(HttpStatusCode.BadRequest);
        var service = BuildService(handler, FastOptions(maxRetries: 3));

        var result = await service.RefreshAsync("https://example.com/token", new Dictionary<string, string>());

        result.Success.Should().BeFalse();
        handler.CallCount.Should().Be(1, because: "4xx errors are not in the retry ShouldHandle predicate");
    }

    // ------------------------------------------------------------------ stub

    /// <summary>
    /// Test double for HttpMessageHandler.
    /// Uses a factory delegate so each invocation gets a fresh HttpResponseMessage,
    /// avoiding shared-instance issues across retries.
    /// </summary>
    internal sealed class StubHttpMessageHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        /// <summary>Returns a fresh 5xx or 4xx response on every call.</summary>
        public static StubHttpMessageHandler Always(HttpStatusCode status) =>
            new(() => new HttpResponseMessage(status));

        /// <summary>Returns responses from the list in order; falls back to 500 when exhausted.</summary>
        public static StubHttpMessageHandler Sequential(params HttpResponseMessage[] responses)
        {
            var index = 0;
            return new StubHttpMessageHandler(() =>
                index < responses.Length
                    ? responses[index++]
                    : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(factory());
        }
    }
}
