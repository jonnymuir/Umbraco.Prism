using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Text.Json;
using System.Collections.Concurrent;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Wraps outbound token-endpoint HTTP calls with an exponential-backoff retry policy
/// (inner) and a per-application circuit breaker (outer).
///
/// Pipeline order: CircuitBreaker → Retry → HTTP call.
/// The circuit breaker observes the final outcome of each full retry sequence,
/// so one circuit-breaker failure represents one exhausted refresh attempt.
/// </summary>
public sealed class PrismTokenRefreshService : IPrismTokenRefreshService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PrismTokenRefreshService> _logger;
    private readonly PrismTokenRefreshOptions _options;
    private readonly ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>> _pipelines = new(StringComparer.OrdinalIgnoreCase);

    public PrismTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptions<PrismTokenRefreshOptions> options,
        ILogger<PrismTokenRefreshService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    private ResiliencePipeline<HttpResponseMessage> CreatePipeline()
    {
        var opts = _options;

        var shouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
            .HandleResult(r => (int)r.StatusCode >= 500);

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Outer: circuit breaker observes the final outcome of each full retry sequence.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = shouldHandle,
                MinimumThroughput = opts.CircuitBreakerMinimumThroughput,
                FailureRatio = opts.CircuitBreakerFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(opts.CircuitBreakerSamplingWindowSeconds),
                BreakDuration = TimeSpan.FromSeconds(opts.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Token refresh circuit breaker opened after repeated failures. " +
                        "Break duration: {BreakDurationSeconds}s",
                        opts.CircuitBreakerBreakDurationSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Token refresh circuit breaker closed (service recovered)");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Token refresh circuit breaker half-opened (probing service)");
                    return ValueTask.CompletedTask;
                }
            })
            // Inner: retry with exponential back-off on transient failures.
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = shouldHandle,
                MaxRetryAttempts = opts.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(opts.InitialBackoffSeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Token refresh retry {Attempt}/{Max} after {DelayMs}ms (status: {Status})",
                        args.AttemptNumber + 1,
                        opts.MaxRetryAttempts,
                        (int)args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode.ToString() ?? args.Outcome.Exception?.GetType().Name);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <inheritdoc/>
    public async Task<TokenRefreshResult> RefreshAsync(
        string tokenEndpoint,
        IReadOnlyDictionary<string, string> formParameters,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? requestHeaders = null)
    {
        if (string.IsNullOrWhiteSpace(tokenEndpoint))
        {
            _logger.LogWarning("Token refresh skipped: token endpoint is missing");
            return new TokenRefreshResult(false, null, null, null, "missing-token-endpoint");
        }

        var normalizedEndpoint = tokenEndpoint.Trim();
        var pipeline = _pipelines.GetOrAdd(normalizedEndpoint, _ => CreatePipeline());

        HttpResponseMessage response;
        try
        {
            var client = _httpClientFactory.CreateClient("PrismTokenRefresh");

            response = await pipeline.ExecuteAsync(async ct =>
            {
                // Re-create request + content on every attempt: FormUrlEncodedContent is single-use.
                using var content = new FormUrlEncodedContent(
                    formParameters.Select(p => KeyValuePair.Create<string?, string?>(p.Key, p.Value)));
                using var request = new HttpRequestMessage(HttpMethod.Post, normalizedEndpoint)
                {
                    Content = content
                };
                if (requestHeaders != null)
                {
                    foreach (var (name, value) in requestHeaders)
                        request.Headers.TryAddWithoutValidation(name, value);
                }
                return await client.SendAsync(request, ct);
            }, cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Token refresh skipped: circuit breaker is open");
            return new TokenRefreshResult(false, null, null, null, "circuit-open");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Token refresh failed: {ExceptionType}", ex.GetType().Name);
            return new TokenRefreshResult(false, null, null, null, ex.GetType().Name);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            string? failureReason = null;

            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("error", out var error))
                {
                    failureReason = error.GetString();
                }
            }
            catch (JsonException)
            {
            }

            failureReason ??= $"http-{(int)response.StatusCode}";

            _logger.LogWarning(
                "Token refresh failed with HTTP {Status} ({FailureReason})",
                (int)response.StatusCode,
                failureReason);
            return new TokenRefreshResult(false, null, null, null, failureReason);
        }

        try
        {
            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            int? expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : null;

            return new TokenRefreshResult(true, accessToken, refreshToken, expiresIn);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Token refresh response could not be parsed: {Message}", ex.Message);
            return new TokenRefreshResult(false, null, null, null, "invalid-json");
        }
    }
}
