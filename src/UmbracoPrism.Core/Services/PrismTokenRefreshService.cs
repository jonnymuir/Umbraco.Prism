using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Text.Json;

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
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public PrismTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptions<PrismTokenRefreshOptions> options,
        ILogger<PrismTokenRefreshService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var opts = options.Value;

        var shouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
            .HandleResult(r => (int)r.StatusCode >= 500);

        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
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
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            var client = _httpClientFactory.CreateClient("PrismTokenRefresh");

            response = await _pipeline.ExecuteAsync(async ct =>
            {
                // Re-create content on every attempt: FormUrlEncodedContent is single-use.
                using var content = new FormUrlEncodedContent(
                    formParameters.Select(p => KeyValuePair.Create<string?, string?>(p.Key, p.Value)));
                return await client.PostAsync(tokenEndpoint, content, ct);
            }, cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Token refresh skipped: circuit breaker is open");
            return new TokenRefreshResult(false, null, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Token refresh failed: {ExceptionType}", ex.GetType().Name);
            return new TokenRefreshResult(false, null, null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Token refresh failed with HTTP {Status}", (int)response.StatusCode);
            return new TokenRefreshResult(false, null, null, null);
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
            return new TokenRefreshResult(false, null, null, null);
        }
    }
}
