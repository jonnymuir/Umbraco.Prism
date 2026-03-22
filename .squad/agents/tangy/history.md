# Tangy — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Tom Nook: Architecture, scope, code review, leadership
- Isabelle: Web Components, Storybook, UI logic, accessibility
- Blathers: C# backend, services, databases, auth
- Scribe: Session logging, decisions, team memory

## Test Landscape

**Frontend Tests (Playwright):**
- `prism-create-tenant-modal.spec.ts` — Modal create flow tests
- Playwright config at `/src/UmbracoPrism.Client/playwright.config.ts`
- Test results in `/test-results/`

**Backend Tests (XUnit):**
- Test project: `/src/UmbracoPrism.Core.Tests/`
- Coverage: BrandingServiceTests, MobileBundleServiceTests, PrismContextTests, middleware tests
- Run: `dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests`

**Accessibility:**
- Storybook's axe integration (WCAG 2.0/2.1) validates accessibility
- Run: `npm run test-storybook:ci:all` (all browsers + WCAG checks)

**Key Concern:** Mobile detection (query flag, user-agent, cookie) + safe-area edge cases on notched devices

## Learnings & Handoff (2026-03-22)

**From Tom Nook Architecture Review:**
- OIDC edge cases + token failure chaos tests marked high priority
- Critical edge cases to test:
  1. Unknown tenant domain (currently logs warning, continues with null tenant — risky)
  2. Token refresh when CIAM down (should fail gracefully, not crash)
  3. OIDC key rotation (force 401 on old token; app should retry with fresh metadata)
  4. Concurrent tenant updates (race conditions on cache invalidation)
  5. Mobile bundle special characters (sanitize to valid identifier)
  6. Admin policy drift (Umbraco group removed but still in Entra)

**Test Coverage Gaps:**
- Missing OAuth redirect → token exchange → cookie set (happy path integration test)
- Missing token refresh failure scenarios (CIAM timeout, network error)
- Missing OIDC key rotation tests (metadata invalidation)
- Missing mobile bundle edge cases (concurrent generation, SSRF validation)
- Playground: mocked CIAM endpoint for auth flow testing

**Current Test Suite:**
- Core: PrismContextTests, PrismTenantMiddlewareTests, BrandingServiceTests, MobileBundleServiceTests
- UI: Playwright E2E for Web Components

**Next:** Design integration test harness with mocked CIAM endpoint; add chaos test scenarios; expand mobile bundle edge case suite

## Learnings & Handoff (2026-03-22, P0 Round 1 — Test coverage context from Blathers)

**Issues #2 and #3 now have dedicated test coverage to build on.**

**Issue #2 — Async signing-key cache (14 tests passing):**
- `PrismTenantMiddlewareTests.cs` updated to cover the new `IPrismSigningKeyCache` injection in `InvokeAsync`.
- Tests confirm pre-warm behavior: cache is populated during tenant resolution; `IssuerSigningKeyResolver` reads from cache synchronously.
- **Test gaps Tangy should fill:**
  - Cache miss → OIDC metadata fetch path under concurrent tenant resolution (race on first warm-up).
  - Cache TTL expiry under load: keys expire mid-request while new keys are being fetched.
  - Key rotation scenario: force 401 with old keys, verify re-warm populates fresh keys and retry succeeds.

**Issue #3 — Token refresh resilience (19 tests passing, 5 new in `PrismTokenRefreshServiceTests.cs`):**
- `RefreshAsync_ReturnsSuccess_OnFirstAttempt` — happy path.
- `RefreshAsync_RetriesOnTransientFailure_AndSucceedsAfterRetry` — transient 5xx recovers.
- `RefreshAsync_ReturnsFailure_WhenAllRetriesExhausted` — all attempts fail.
- `RefreshAsync_CircuitBreaker_OpensAfterThresholdFailures` — circuit trips after N failures.
- `RefreshAsync_DoesNotRetry_On4xxClientError` — 400 not retried.
- **Test gaps Tangy should fill (per reliability plan for issue #7):**
  - `TaskCanceledException` (timeout) under concurrent refresh requests triggers circuit open.
  - Circuit half-open probe: after `BreakDurationSeconds`, one probe attempt is allowed; verify recovery path.
  - Per-tenant isolation gap: current shared circuit breaker means one bad tenant can block others. Chaos scenario: simulate scenario where this occurs.
  - Token refresh integration: full path from `PrismContext.RefreshTokenAsync` → `IPrismTokenRefreshService` → mocked CIAM endpoint (cookie-update side-effects verified).

**Polly v8 gotchas (from Blathers, avoid in tests):**
- `MaxRetryAttempts` minimum is **1**; use `Math.Max(1, n)` in test-option helpers.
- `Enumerable.Repeat(singleInstance, n)` for stub `HttpResponseMessage` causes `ObjectDisposedException` on second attempt — use factory delegates instead.
- `BrokenCircuitException` is in `Polly.CircuitBreaker` namespace; catch before broad `Exception` handler.
