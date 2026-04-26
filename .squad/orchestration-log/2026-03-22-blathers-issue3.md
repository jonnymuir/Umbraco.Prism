# Orchestration Log — Blathers / Issue #3

**Date:** 2026-03-22  
**Agent:** Blathers  
**Issue:** #3 — Token refresh resilience (Polly retry + circuit breaker)  
**Outcome:** ✅ Build passing, 19/19 tests passing (5 new)

---

## Summary

Blathers introduced `IPrismTokenRefreshService` / `PrismTokenRefreshService` to wrap all outbound token-endpoint HTTP calls in a Polly 8.6.6 resilience pipeline. `PrismContext.RefreshTokenAsync` now delegates HTTP transport to the service while retaining orchestration/cookie-update logic.

## Files Changed

| File | Change |
|------|--------|
| `src/UmbracoPrism.Core/Services/IPrismTokenRefreshService.cs` | **New** — Interface: `RefreshAsync(tenantId, refreshToken, cancellationToken)` |
| `src/UmbracoPrism.Core/Services/PrismTokenRefreshService.cs` | **New** — Singleton; Polly 8.6.6; CircuitBreaker (outer) → Retry (inner) → HTTP; all settings from `IOptions<PrismTokenRefreshOptions>` |
| `src/UmbracoPrism.Core/Models/PrismTokenRefreshOptions.cs` | **New** — Options class bound to `"Prism:TokenRefresh"` config section |
| `src/UmbracoPrism.Core.Tests/PrismTokenRefreshServiceTests.cs` | **New** — 5 tests covering retry, exhaustion, circuit open, 4xx no-retry |

## Package Added

`Polly` 8.6.6 added to `UmbracoPrism.Core.csproj`.

## Pipeline Design

```
CircuitBreaker (outer)
  └─ Retry (inner: exponential backoff with jitter)
       └─ HTTP call
```

**Rationale:**
- Circuit breaker observes one sample per fully-exhausted retry sequence (clean mental model).
- If circuit is open, short-circuits immediately — Retry and HTTP call are never invoked.
- `ShouldHandle` triggers on 5xx, `HttpRequestException`, `TaskCanceledException` only. 4xx is not retried (invalid token; retry would not help).

## Configuration Defaults (all under `"Prism:TokenRefresh"`)

| Setting | Default |
|---------|---------|
| `MaxRetryAttempts` | 3 (+1 initial = 4 total calls) |
| `InitialBackoffSeconds` | 1.0 (exponential: 1s, 2s, 4s with jitter) |
| `CircuitBreakerMinimumThroughput` | 5 |
| `CircuitBreakerFailureRatio` | 1.0 (100%) |
| `CircuitBreakerSamplingWindowSeconds` | 30 |
| `CircuitBreakerBreakDurationSeconds` | 60 |

## Security Note

Token strings are never logged. Only HTTP status codes, retry attempt numbers, delay ms, and exception type names are emitted.

## Known Limitation / Follow-up

Current circuit breaker is **shared app-wide**. One tenant's CIAM failure can accumulate in the same counter as all others. Recommended follow-up: per-tenant `ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>>` keyed by `EntraTenantId`. Warrants a separate issue.

## New Tests

| Test | Scenario |
|------|---------|
| `RefreshAsync_ReturnsSuccess_OnFirstAttempt` | Happy path |
| `RefreshAsync_RetriesOnTransientFailure_AndSucceedsAfterRetry` | Transient 5xx recovers |
| `RefreshAsync_ReturnsFailure_WhenAllRetriesExhausted` | All attempts fail |
| `RefreshAsync_CircuitBreaker_OpensAfterThresholdFailures` | Circuit trips after N failures |
| `RefreshAsync_DoesNotRetry_On4xxClientError` | 400 not retried |

## Test Result

```
Test run for UmbracoPrism.Core.Tests
Passed: 19 / 19
Failed: 0
```
