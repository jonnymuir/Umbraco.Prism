using System.Collections.Concurrent;
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
    /// MaxRetryAttempts is clamped to a minimum of 1 and circuit minimum throughput to 2
    /// to satisfy Polly v8 validation requirements.
    /// </summary>
    private static PrismTokenRefreshOptions FastOptions(
        int maxRetries = 3,
        int cbMinThroughput = 5,
        int breakDurationSeconds = 600) =>
        new()
        {
            MaxRetryAttempts = Math.Max(1, maxRetries),
            InitialBackoffSeconds = 0,          // no delay between retries in tests
            CircuitBreakerMinimumThroughput = Math.Max(2, cbMinThroughput),
            CircuitBreakerFailureRatio = 1.0,
            CircuitBreakerSamplingWindowSeconds = 60,
            CircuitBreakerBreakDurationSeconds = breakDurationSeconds
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

    [Fact]
    public async Task RefreshAsync_RetriesOnTimeout_AndSucceedsAfterRetry()
    {
        var handler = StubHttpMessageHandler.Sequential(
            new TaskCanceledException("simulated timeout"),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = SuccessTokenJson() });

        var service = BuildService(handler, FastOptions(maxRetries: 1));

        var result = await service.RefreshAsync("https://example.com/token", new Dictionary<string, string>());

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("new-access");
        handler.CallCount.Should().Be(2, because: "a transient timeout should be retried once");
    }

    [Fact]
    public async Task RefreshAsync_HalfOpenProbe_ReopensThenRecoversAfterBreakDuration()
    {
        var handler = StubHttpMessageHandler.Sequential(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = SuccessTokenJson() });

        var service = BuildService(handler, FastOptions(maxRetries: 1, cbMinThroughput: 2, breakDurationSeconds: 1));

        var firstFailure = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());
        firstFailure.Success.Should().BeFalse();

        var secondFailure = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());
        secondFailure.Success.Should().BeFalse();

        var openCircuit = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());
        openCircuit.Success.Should().BeFalse();
        handler.CallCount.Should().Be(4, because: "two failed outer calls are required before the circuit can open");

        await Task.Delay(TimeSpan.FromSeconds(1.2));

        var recovered = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());

        recovered.Success.Should().BeTrue();
        recovered.AccessToken.Should().Be("new-access");
        handler.CallCount.Should().Be(5, because: "the half-open probe should be allowed through after the break duration");
    }

    [Fact]
    public async Task RefreshAsync_IsolatesCircuitBreakerState_PerTokenEndpoint()
    {
        const int threshold = 3;
        const int maxRetries = 1;
        const int handlerCallsPerFailedAttempt = maxRetries + 1;

        var handler = StubHttpMessageHandler.FromRequest(request =>
        {
            if (request.RequestUri!.Host.Equals("tenant-a.example.com", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = SuccessTokenJson()
            };
        });

        var service = BuildService(handler, FastOptions(maxRetries: maxRetries, cbMinThroughput: threshold));

        for (var i = 0; i < threshold; i++)
        {
            var failed = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());
            failed.Success.Should().BeFalse();
        }

        var isolated = await service.RefreshAsync("https://tenant-b.example.com/token", new Dictionary<string, string>());
        isolated.Success.Should().BeTrue();
        isolated.AccessToken.Should().Be("new-access");

        var blockedAgain = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());
        blockedAgain.Success.Should().BeFalse();

        handler.CallCount.Should().Be((threshold * handlerCallsPerFailedAttempt) + 1,
            because: "tenant-b should not be blocked by tenant-a's open circuit breaker");
    }

    [Fact]
    public async Task RefreshAsync_ShortCircuitsConcurrentRequests_WhenCircuitIsAlreadyOpen()
    {
        const int initialHandlerCalls = 4;
        var handler = StubHttpMessageHandler.Always(HttpStatusCode.ServiceUnavailable);
        var service = BuildService(handler, FastOptions(maxRetries: 1, cbMinThroughput: 2, breakDurationSeconds: 60));

        var firstFailure = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());
        var secondFailure = await service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>());

        firstFailure.Success.Should().BeFalse();
        secondFailure.Success.Should().BeFalse();
        handler.CallCount.Should().Be(initialHandlerCalls, because: "two failed outer calls are required before the circuit opens");

        var concurrentResults = await Task.WhenAll(
            Enumerable.Range(0, 12)
                .Select(_ => service.RefreshAsync("https://tenant-a.example.com/token", new Dictionary<string, string>())));

        concurrentResults.Should().OnlyContain(result => result.Success == false);
        handler.CallCount.Should().Be(initialHandlerCalls, because: "the open circuit should reject concurrent callers without issuing more HTTP requests");
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCalls_KeepOpenCircuitIsolatedToFailingEndpoint()
    {
        const string failingEndpoint = "https://tenant-a.example.com/token";
        const string healthyEndpoint = "https://tenant-b.example.com/token";

        var handler = StubHttpMessageHandler.FromRequest(request =>
        {
            if (request.RequestUri!.Host.Equals("tenant-a.example.com", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = SuccessTokenJson()
            };
        });

        var service = BuildService(handler, FastOptions(maxRetries: 1, cbMinThroughput: 2, breakDurationSeconds: 60));

        await service.RefreshAsync(failingEndpoint, new Dictionary<string, string>());
        await service.RefreshAsync(failingEndpoint, new Dictionary<string, string>());

        var beforeConcurrentFailingCalls = handler.CallsByRequestUri[failingEndpoint];

        var failingTasks = Enumerable.Range(0, 12)
            .Select(_ => service.RefreshAsync(failingEndpoint, new Dictionary<string, string>()));
        var healthyTasks = Enumerable.Range(0, 12)
            .Select(_ => service.RefreshAsync(healthyEndpoint, new Dictionary<string, string>()));

        var results = await Task.WhenAll(failingTasks.Concat(healthyTasks));
        var failingResults = results.Take(12);
        var healthyResults = results.Skip(12);

        failingResults.Should().OnlyContain(result => result.Success == false);
        healthyResults.Should().OnlyContain(result => result.Success == true);

        handler.CallsByRequestUri[failingEndpoint].Should().Be(beforeConcurrentFailingCalls,
            because: "the open circuit should short-circuit failing endpoint requests during concurrent pressure");
        handler.CallsByRequestUri[healthyEndpoint].Should().Be(12,
            because: "healthy endpoint traffic should remain independent of another endpoint's open circuit state");
    }

    // ------------------------------------------------------------------ stub

    /// <summary>
    /// Test double for HttpMessageHandler.
    /// Uses a factory delegate so each invocation gets a fresh HttpResponseMessage,
    /// avoiding shared-instance issues across retries.
    /// </summary>
    internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public ConcurrentDictionary<string, int> CallsByRequestUri { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns a fresh 5xx or 4xx response on every call.</summary>
        public static StubHttpMessageHandler Always(HttpStatusCode status) =>
            new(_ => new HttpResponseMessage(status));

        /// <summary>Returns a fresh response based on the outgoing request.</summary>
        public static StubHttpMessageHandler FromRequest(Func<HttpRequestMessage, HttpResponseMessage> factory) =>
            new(factory);

        /// <summary>Returns responses or exceptions from the list in order; falls back to 500 when exhausted.</summary>
        public static StubHttpMessageHandler Sequential(params object[] responses)
        {
            var index = 0;
            return new StubHttpMessageHandler(_ =>
            {
                var next = index < responses.Length
                    ? responses[index++]
                    : new HttpResponseMessage(HttpStatusCode.InternalServerError);

                return next switch
                {
                    HttpResponseMessage response => response,
                    TaskCanceledException taskCanceledException => throw taskCanceledException,
                    HttpRequestException httpRequestException => throw httpRequestException,
                    _ => throw new InvalidOperationException($"Unsupported response step type: {next.GetType().Name}")
                };
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (request.RequestUri != null)
            {
                CallsByRequestUri.AddOrUpdate(request.RequestUri.AbsoluteUri, 1, (_, count) => count + 1);
            }

            return Task.FromResult(factory(request));
        }
    }
}
