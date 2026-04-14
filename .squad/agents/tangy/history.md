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

## Learnings

- 2026-03-28 (Issue #6 branding optimization): Added backend tests to lock in cache-coherence behavior for branding tabs and per-request override projection.
- Sequential multi-tenant requests now have explicit regression coverage to ensure one tenant's branding values never appear in another tenant response.
- Added same-tenant update coverage to verify that changed desktop/mobile overrides are reflected on subsequent requests, with no stale response carry-over assumptions.
- Tests were validated with a focused run of branding-related Core test classes (14 passing, 0 failing).
- 2026-03-28 (Cross-agent): Blathers' precomputed declaration implementation informed assertion targets so the test suite now covers both optimized and fallback rendering paths.
- 2026-03-28 (Issue #7 reliability): Removed a duplicated `PrismOidcConfigurationTests` fixture that was causing the current compile blocker before any runtime assertions could execute.
- Added non-blocking OIDC signing-key refresh coverage for both cache-cold and stale-`kid` rotation paths, keeping assertions aligned to the existing two-plane authorization split rather than changing policy expectations.
- Added outage fan-out coverage for `PrismTokenRefreshService` to verify an open circuit rejects concurrent callers without extra HTTP traffic, and corrected the test harness to respect Polly v8's minimum circuit-breaker throughput of 2.
- Added concurrent tenant/branding race coverage that only accepts coherent old-or-new tenant snapshots and verifies branding-tab cache defaults remain immutable during parallel override projection.
- Focused Core validation for `PrismOidcConfigurationTests`, `PrismTokenRefreshServiceTests`, `PrismTenantMiddlewareTests`, `BrandingServiceTests`, and `TenantServiceCacheStrategyTests` passed green: 27 tests, 0 failures.
- 2026-03-28 (Issue #7 reliability verification): Re-ran an expanded focused reliability filter including `PrismSigningKeyCacheTests`, `PrismOidcConfigurationTests`, `PrismTokenRefreshServiceTests`, `TenantServiceCacheStrategyTests`, `BrandingServiceTests`, and `PrismTenantMiddlewareTests`; result was 32 passed, 0 failed, 0 skipped in 1.7s.
- Coverage now explicitly demonstrates: unknown/rotated signing-key handling with non-blocking warm requests, transient timeout/outage refresh resilience with half-open recovery and concurrent open-circuit short-circuiting, and tenant/branding update races constrained to coherent snapshots with no cross-tenant leakage.

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.

## Learnings

- 2026-07-10 (Issue — OIDC cold-start test coverage): Added three tests to `PrismAuthExtensionsSecurityTests.cs` covering Copper's identified gaps after the cold-start signing-key fix:
  1. `ResolveSigningKeys_PropagatesException_WhenWarmAsyncThrowsDuringColdStart` — verifies `HttpRequestException` from `WarmAsync` is not swallowed; uses Moq `ThrowsAsync`.
  2. `ResolveSigningKeys_DeduplicatesConcurrentColdStartFetches_ForSameTenant` — verifies the semaphore in `PrismSigningKeyCache` means only one underlying fetch fires across N concurrent cold callers; uses a `ConcurrentWarmSigningKeyCache` test double with a `TaskCompletionSource` gate and internal `SemaphoreSlim` mirroring real-cache deduplication semantics.
  3. `ResolveSigningKeys_MatchesTenantId_CaseInsensitively` — verifies a token `tid` of `"TENANT-A"` is matched to a configured tenant of `"tenant-a"` via `OrdinalIgnoreCase`; no warm triggered when cache already contains the key.
- Total test count: 168 (165 existing + 3 new), 0 failures.
- Gotcha: `GetAwaiter().GetResult()` on `WarmAsync` inside `ResolveSigningKeys` means blocking the calling thread; for concurrency tests use `Task.Run` callers to avoid deadlock on the test thread. Use `warmStarted` TCS to ensure callers are blocking before releasing the gate — avoids `Task.Delay` timing fragility.

## Learnings

- 2026-04-14 (localhost Aspire auth flake strategy): For the live Playwright suite, split readiness into three layers: machine-readable startup probes first, page-specific CTA/affordance checks second, and only then the behaviour assertion being tested.
- Do not let unrelated auth or workflow tests use direct deep links as generic setup during cold start. In this repo, deep links like `/dashboard`, `/my-workflows`, and `/get-in-touch` should either come from authored CTAs with asserted `href`s or be exercised as explicit route-contract checks after `/api/prism/downstream-demo/seed-contract-ready` is true.
- In the current localhost auth flow, a home-page sign-in CTA that carries `returnUrl=/dashboard` can hide a dashboard redirect loop behind a browser blank page. Capture recent 302 `Location` headers around sign-in/dashboard navigation so Playwright failures show `signin-oidc -> /dashboard -> /dashboard...` instead of hanging.
- 2026-04-14 (localhost startup diagnosis): The Aspire-backed harness does reach its readiness gate on a clean boot; current startup blockers were harness-side observability and rerun hygiene, not the readiness contract itself.
- Use listener-based port ownership checks for the localhost Aspire lane, not HTTP/root probes, because the Aspire resource-service port (`22194`) can be bound without answering a readiness GET and stale listeners can otherwise slip past preflight.
- Immediate reruns after a start/stop or failed lane can spend ~30s draining listeners; give the prereq gate and `LiveAppHost.ensurePortsAreAvailable()` a bounded grace period before failing fast, and include occupied PIDs plus per-check readiness details in timeout errors.
- With startup stable, the current full-lane product failure reproduced at `signed-in member can still call the mock business app API after the whole stack restarts`, where the dashboard shows `401 Request Failed` after restart even though the pre-restart startup/auth path passed.

## 2026-03-29 — OIDC Signing Key Cold-Start Test Coverage

**Session:** OIDC Signing Key Fix  
**Work Type:** Test implementation

**Context:** Copper security-reviewed the synchronous key resolver cold-start fix and identified 3 test coverage gaps. All three implemented.

**Tests Implemented (PrismAuthExtensionsSecurityTests.cs):**

1. **Exception Propagation**
   - Validates that exceptions during synchronous `WarmAsync` fetch are propagated correctly
   - Confirms token validation fails when OIDC metadata endpoint is unreachable (fail-closed)

2. **Cold-Start Concurrency Deduplication**
   - Tests per-tenant `SemaphoreSlim` inside `PrismSigningKeyCache.WarmAsync`
   - Verifies only first waiter performs HTTP fetch; subsequent waiters see cached result
   - Validates atomic cache update semantics

3. **Case-Insensitive Tenant ID Matching**
   - Tests `OrdinalIgnoreCase` comparison in `Any(t => Equals(t.EntraTenantId, tokenTenantId, OrdinalIgnoreCase))`
   - Tests `ConcurrentDictionary` case-insensitive lookup
   - Validates mixed-case `tid` claims are handled correctly

**Architectural Insights Documented:**
- Exception propagation is intentional (fail-loud, not fail-open)
- Deduplication barrier lives in `PrismSigningKeyCache.WarmAsync`, not in `ResolveSigningKeys`
- Case-insensitive matching is end-to-end (both tenant lookup and cache store)

**Test Results:** 168/168 passing (100%)

**Related:**
- Orchestration log: `.squad/orchestration-log/2026-03-29T13-53Z-tangy.md`
- Decision record: `.squad/decisions.md` → "OIDC Signing Key Cold-Start Fix"

## 2025-04-02 — prism-mobile-nav Playwright Test Coverage

**Session:** Mobile Nav Testing  
**Work Type:** Playwright E2E test implementation

**Context:** Created comprehensive Playwright test suite for the `prism-mobile-nav` Web Component to verify rendering, active states, accessibility, layout, and edge cases against Storybook stories.

**Tests Implemented (prism-mobile-nav.spec.ts):**

**Rendering Tests (7 tests):**
1. Verifies correct number of items in Default story (3 items)
2. Checks all nav items have labels and icons
3. Confirms nav is visible in Storybook (display not none)
4. Tests Many Items story renders 5 items
5. Tests Max Items story renders 6 items  
6. Tests No Icons story renders items without icons
7. Validates Light Theme story renders correctly

**Active State Tests (4 tests):**
1. Confirms no active items in Default story (currentPath="")
2. Validates correct item is highlighted in WithActiveItem story
3. Ensures only one item is active at a time
4. Checks inactive items lack aria-current and active class

**Accessibility Tests (4 tests):**
1. Verifies nav has correct ARIA role="navigation" and aria-label
2. Confirms nav items are semantic anchor links (not divs/buttons)
3. Validates icons have aria-hidden="true" and focusable="false"
4. Tests custom nav-label is applied correctly

**Structure & Layout Tests (3 tests):**
1. Confirms nav uses CSS grid layout
2. Validates nav items meet WCAG minimum tap target height (44px+)
3. Verifies nav has fixed positioning at bottom

**Edge Cases Tests (4 tests):**
1. Handles empty items array gracefully (renders 0 items, no crash)
2. Handles malformed JSON in items property (renders 0 items, no crash)
3. Handles items with missing optional properties (icons, target)
4. Tests case-insensitive path matching for active state

**Test Results:** 22/22 passing (100%) in 9.8s

**Learnings:**
- Storybook story URL conversion: "Many Items (5)" → `--many-items` (kebab-case, drops parenthetical suffixes)
- Shadow DOM piercing pattern: `nav.evaluate((el) => el.shadowRoot?.querySelector(...))` for querying inside shadow roots
- Component visibility in Storybook: Story decorators apply `display: block !important` to override component's default `display: none`
- Test pattern: Load story via iframe → pierce shadow DOM → assert observable behavior (not implementation details)
- WCAG tap target minimum: 44px for mobile interactive elements

**Coverage Gaps (intentionally skipped):**
- Navigation event testing (click triggers navigation) — blocked by Storybook iframe navigation constraints
- CSS custom property theming tests — covered by visual Storybook tests with axe integration


## 2025-04-03 — Key Vault Integration Code Review

**Session:** Key Vault Security Review  
**Work Type:** Code review, edge case analysis, production readiness assessment

**Context:** Jonny requested a thorough pre-release review of the new Key Vault integration (`PrismKeyVaultConfigureOptions.cs`) before shipping to production. He can't easily test in production, so this review acts as the quality gate.

**Scope:** 8 files, ~676 lines reviewed across configuration, services, tests, and extensions.

**Findings:**

**Critical Issues (BLOCKING):**
1. **Synchronous HTTP Call (Lines 51, 54):** `client.GetSecret()` is synchronous and blocks request threads during DI resolution. Under concurrent cold-start scenarios, this can cause thread pool exhaustion and HTTP 503 errors. **Fix:** Add `IHostedService` to pre-warm options during app startup, moving the blocking call off request threads.

2. **Missing 401 Unauthorized Handling (Line 57):** Current error handling catches 404 (not found) and 403 (no permission), but missing 401 (authentication failed). When Managed Identity isn't configured, error message says "network connectivity" instead of "enable Managed Identity." **Fix:** Add 401 to specific error handling with actionable message.

**Quality Issues (Non-blocking):**
3. Partial configuration risk (fetch both secrets before mutating options)
4. Secret name typo risk (extract to constants in `PrismBiometricOptions`)
5. Double registration confusion (deprecate old `AddPrismKeyVault()` extension)

**Test Coverage Gaps:**
- No tests for `PrismKeyVaultConfigureOptions.Configure()` path
- No tests for 404/403/401 error scenarios
- No tests for appsettings + Key Vault overlay behavior
- Deferred to backlog (requires mocking infrastructure)

**Verdict:** **FAIL** ❌ — Issues 1 and 3 must be fixed before production deployment.

**What's Good:**
- HTTPS validation (security best practice)
- Retry configuration (3x, exponential backoff)
- Silent local dev (no-op when VaultUri not set)
- Correct secret naming convention (`Prism--Biometric--SigningKey`)
- Correct registration order (overlay pattern works)
- Thread-safe (`SecretClient` + singleton `IOptions<T>`)

**Deliverable:** Written comprehensive review to `.squad/decisions/inbox/tangy-keyvault-review.md` with detailed analysis of all 7 findings, production readiness assessment, and specific fix recommendations.

**Key Insight:** `IConfigureOptions<T>.Configure()` is synchronous by design, but it's invoked during DI resolution (often on first request thread). Blocking I/O in this path is a classic ASP.NET Core deadlock pattern. The fix is to pre-warm options during app startup via `IHostedService`, moving the blocking call to a background thread.

**Related:**
- Orchestration log: (not applicable, direct request from Jonny)
- Decision record: `.squad/decisions/inbox/tangy-keyvault-review.md`

**Next Steps:** Jonny will decide whether to fix blocking issues now or defer. If fixing, Blathers (backend specialist) should implement the `IHostedService` warmup pattern.

## 2026-04-03 — v1.5.0 Release: IConfigureOptions Code Review

**Task Type:** Technical code review  
**Status:** ✅ FINDINGS (2 blockers identified)  
**Orchestration Log:** `.squad/orchestration-log/2026-04-03T10:27:49Z-tangy.md`

### Review Scope

Code review of `PrismKeyVaultConfigureOptions` implementation for v1.5.0 release. Examined:
- IConfigureOptions pattern for lazy Key Vault integration
- Error message handling (401/403/404/transient)
- Options assignment atomicity
- Health check caching strategy

### Findings

**Blocker 1: Fail-Late Validation (IHostedService Warm-Up)**
- **Finding:** Suggested IHostedService warm-up for early Key Vault validation at startup
- **Reasoning:** Fail-late approach delays config errors until first biometric request; production deployments could run for hours/days with misconfigured Key Vault
- **Response from Team:** Jonny explicitly rejected warm-up pattern; fail-late is intentional design choice
- **Resolution:** APPROVED — documented as intentional; no code change required
- **Lesson:** When fail-late is by design, document the implications clearly and provide monitoring guidance (health check + post-deployment smoke test)

**Blocker 2: 401 Error Message Handling**
- **Finding:** HTTP 401 responses were falling through to generic "transient" error message
- **Issue:** 401 means authentication failure (wrong/missing Managed Identity, not logged in locally), not a transient error
- **Status:** FIXED ✅
- **Implementation:** 401 now treated as non-retryable `InvalidOperationException` with actionable message pointing to Managed Identity + `az login`
- **Resolution:** APPROVED and merged

### Test Coverage Validation

- ✅ 168/168 tests passed
- ✅ Error handling tests cover 401/403/404/transient scenarios
- ✅ Atomic assignment tests verify options remain empty on failure
- ✅ Health check caching tests verify result is cached and vault URI is included in cache key

### Code Quality Assessment

**Strengths:**
- Secret name constants (`SigningKeySecretName`, `EncryptionKeySecretName`) eliminate magic literals
- Atomic assignment pattern prevents half-configured options state
- Explicit retry policy (3×, 0.8–8s) documented in code
- Health check properly sanitizes error messages

**Patterns Established:**
- 401 = configuration error (non-retryable)
- 403/404 = secrets not found or no access (non-retryable)
- Transient (429, 503, network) = retry per policy
- Other exceptions = retry exhausted, report "temporarily unavailable"

### Handoff Notes

All findings resolved before release. No outstanding issues.

**For Future Reviews:**
- Fail-late patterns need explicit monitoring and post-deployment validation guidance
- Options pattern must use atomic assignment to avoid half-configured state leaking
- Distinguish auth errors (401) from resource errors (403/404) in error messages

---

**Verdict:** ✅ READY FOR RELEASE
All findings addressed. 168/168 tests passing. Error handling hardened per feedback. Fail-late design is intentional and properly documented.

## 2026-04-04 — Push Notification Service Test Coverage

**Session:** Push Notification Testing  
**Work Type:** Unit test implementation

**Context:** Implemented comprehensive unit test coverage for the new push notification feature (FCM integration) covering service layer, controller API, and content-published event handler.

**Tests Implemented:**

**1. PrismNotificationServiceTests (10 tests):**
- Token Registration & Unregistration:
  - `RegisterDeviceToken_SavesToDatabase_WhenNoExistingRecord` — creates new device credential stub with push token
  - `RegisterDeviceToken_UpdatesToken_WhenRecordAlreadyExists` — updates existing record instead of inserting duplicate
  - `UnregisterDeviceToken_NullsToken` — sets token to NULL (preserves device record for biometric auth)
  
- Genre Subscriptions:
  - `SubscribeToGenre_CreatesSubscription_WhenNotAlreadySubscribed` — creates subscription record
  - `SubscribeToGenre_IsIdempotent_WhenAlreadySubscribed` — repeated subscribe is safe (no duplicate insert)
  - `UnsubscribeFromGenre_RemovesSubscription` — DELETE from subscriptions table
  
- Notification Delivery:
  - `SendToGenreSubscribers_NoSubscribers_DoesNotThrow` — graceful empty-subscriber case
  - `SendToGenreSubscribers_WithSubscribers_QueuesTokensFromDatabase` — subscription → token resolution path verified
  - `SendToAllMembers_NoTokens_DoesNotThrow` — graceful empty-token case
  - `SendToAllMembers_WithTokens_QueriesDatabase` — broadcast query verified

**2. PrismNotificationControllerTests (17 tests):**
- Device Token Registration:
  - `Register_ValidToken_Returns200` — happy path with service call verification
  - `Register_MissingToken_Returns400` — validation failure (empty token)
  - `Register_NullRequest_Returns400` — validation failure (null body)
  - `Register_NoUserOid_Returns401` — missing user claim
  - `Register_NoTenant_Returns401` — missing tenant context
  - `Unregister_AuthenticatedUser_Returns200` — happy path unregister
  - `Unregister_NoUserOid_Returns401` — missing user claim
  
- Genre Subscriptions:
  - `Subscribe_ValidGenre_Returns200` — happy path with service call verification
  - `Subscribe_MissingGenre_Returns400` — validation failure
  - `Subscribe_NullRequest_Returns400` — validation failure
  - `Subscribe_NoTenant_Returns401` — missing tenant context
  - `Unsubscribe_ValidGenre_Returns200` — happy path with service call verification
  - `Unsubscribe_MissingGenre_Returns400` — validation failure
  - `Unsubscribe_NoUserOid_Returns401` — missing user claim
  
- User Identity Resolution:
  - `Register_FallbackClaim_ResolvesUserOid` — tests alternate claim type (`http://schemas.microsoft.com/identity/claims/objectidentifier`)

**3. PrismContentPublishedHandlerTests (11 tests):**
- Notification Routing:
  - `Handle_ContentWithNotificationGenre_SendsToGenreSubscribers` — genre property present → targeted send
  - `Handle_ContentWithoutNotificationGenre_SendsToAllMembers` — no genre → broadcast send
  - `Handle_ContentWithWhitespaceGenre_SendsToAllMembers` — whitespace genre treated as missing
  - `Handle_ContentTypeNotInNotifiableList_DoesNotSend` — content type filtering verified
  - `Handle_NoConfiguredNotifiableTypes_DoesNotSend` — empty config → no-op
  - `Handle_ContentWithoutTenantId_DoesNotSend` — missing tenant property → skip (logged)
  - `Handle_MultiplePublishedEntities_ProcessesEach` — batch publish support
  - `Handle_CaseInsensitiveContentTypeMatch_SendsNotification` — content type alias comparison is case-insensitive
  
- Exception Handling:
  - `Handle_ServiceThrows_DoesNotRethrow` — service exceptions swallowed (never break publish pipeline)
  - `Handle_GenreServiceThrows_DoesNotRethrow` — genre-specific send failures swallowed

**Test Results:** 206/206 passing (100%)
- 168 existing tests
- 38 new notification tests (10 service, 18 controller, 10 handler)
- 0 regressions

**Learnings:**
- **Firebase Initialization:** `PrismNotificationService` initializes Firebase directly in the constructor via `TryInitFirebase`. This is difficult to mock, so tests verify the database query path and rely on the fact that without `Prism:Firebase:CredentialJson` configured, Firebase is null and the service logs a warning but continues gracefully.
- **Test Strategy for External SDKs:** When an external SDK (FirebaseAdmin) is tightly coupled to the service, tests focus on:
  1. Database interaction (token storage, subscription management)
  2. Control flow (empty cases, routing logic)
  3. Exception swallowing (publish pipeline safety)
  4. Integration tests (not unit tests) would cover actual FCM delivery
- **IContent Mocking:** Umbraco's `IContent.ContentType` returns `ISimpleContentType`, not `IContentType`. Mocking pattern: `Mock<ISimpleContentType>` → set `Alias` property → inject into `IContent` mock.
- **Publish Pipeline Safety:** `PrismContentPublishedHandler` must never throw — all exceptions are caught and logged. Tests verify `DoesNotRethrow` on service failures.
- **Controller User Resolution:** Controller resolves user OID from two claim types: `"oid"` (preferred) and `"http://schemas.microsoft.com/identity/claims/objectidentifier"` (fallback). Tests verify both paths.

**Coverage Gaps (deferred to integration tests):**
- Actual FCM multicast delivery (requires Firebase test doubles or emulator)
- Stale token nullification (requires FCM response simulation)
- FCM batch processing (500-token chunks)
- Token encryption roundtrip on device registration (covered in `BiometricControllerTests`)

**Next Steps:** Integration tests with Firebase emulator or test doubles would validate the full FCM delivery path. Current unit tests ensure core logic is sound and the service degrades gracefully when Firebase is unavailable.

---

## 2026-04-03 — Phase 4 Complete (Notifications Testing)

**Orchestration Log:** `.squad/orchestration-log/2026-04-03T12:57:36Z-tangy-notifications.md`  
**Decision Merged:** `.squad/decisions.md` (Test Strategy)

**Test Deliverable:**
- **38 new tests created** across 3 test classes:
  - `PrismNotificationServiceTests` — 10 tests (service layer, database ops, error handling)
  - `PrismNotificationControllerTests` — 18 tests (API endpoints, auth, validation)
  - `PrismContentPublishedHandlerTests` — 10 tests (event routing, genre filtering, exception safety)

**Suite Status:**
- Total tests: 206
- Passing: 206 (100%)
- Failing: 0
- Build: ✅ 0 errors

**Test Strategy Highlights:**
- Firebase mocking deferred to integration tests (FCM is sealed, hard to mock)
- Service degrades gracefully when Firebase unavailable (tests verify this)
- Exception swallowing verified: publish pipeline safe even if notification fails
- Rate limiting mocks integrated
- Genre validation (regex) tests included

---


## 2025-03-23 — Playwright Test Fixes (Mobile Nav & Push Notifications)

**Context:**  
Two test issues identified by Jonny Muir:
1. Broken Playwright test referencing missing `LightTheme` story in `prism-mobile-nav.stories.ts`
2. Missing test coverage for push notifications toggle in `prism-create-tenant-modal`
3. Hydration bug: `pushNotificationsEnabled` not read from persisted config

**Changes Made:**

### Issue 1: Missing LightTheme Story
**File:** `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.stories.ts`
- Added `LightTheme` story (export `LightTheme`, name `'Light Theme'`)
- Modeled after `DarkTheme` but with light background (`#f2f2f7`)
- Uses `THREE_ITEMS` fixture, `current-path="/"`, and `nav-label="Mobile navigation (light)"`
- Fixes broken test at line 114-126 in `prism-mobile-nav.spec.ts`

### Issue 2: Push Notifications Test Coverage
**File:** `src/UmbracoPrism.Client/tests/prism-create-tenant-modal.spec.ts`
- Added 2 new tests:
  1. `'Produce Mobile tab shows push notifications toggle'` — Verifies toggle exists, defaults to unchecked
  2. `'Push notifications toggle can be enabled'` — Enables toggle, verifies state change persists

**Test Pattern Used:**
- Navigate to `editStoryUrl`
- Click "Produce Mobile" tab via `el.shadowRoot?.querySelector('uui-tab[label="Produce Mobile"]')`
- Find toggle via `el.shadowRoot?.querySelector('input[aria-label="Push Notifications"]')`
- Check `checked` property

### Issue 3: Hydration Bug Fix
**File:** `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts`

**Changes:**
1. Updated `_readMobileAppConfig` return value to include `pushNotificationsEnabled`
2. Added hydration in `connectedCallback` (line 146): `this._pushNotificationsEnabled = mobileConfig?.pushNotificationsEnabled ?? false;`
3. Added hydration in `updated` (line 175): `this._pushNotificationsEnabled = mobileConfig?.pushNotificationsEnabled ?? false;`

**Before:** Toggle always defaulted to `false` on edit modal load, ignoring saved config  
**After:** Toggle correctly hydrates from `mobileAppConfig.pushNotificationsEnabled`

**Verification:**
- ✅ TypeScript compilation: `npx tsc --noEmit` — 0 errors
- ✅ All existing tests pass (no regressions)
- ✅ New tests cover toggle visibility, default state, and interaction

**Rationale:**
- Push notifications toggle added by Kicks (previous commit) but had no test coverage
- Hydration bug would cause data loss on every edit (toggle would reset to false)
- Tests now enforce correct shadow DOM patterns (`aria-label` selectors, evaluate for shadow access)

**Test Files Updated:**
- `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.stories.ts` (+28 lines, 1 new story)
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` (+3 lines hydration)
- `src/UmbracoPrism.Client/tests/prism-create-tenant-modal.spec.ts` (+47 lines, 2 new tests)

---

---

## 2025-07-15 — Branding Tab Test Fixes & Test Philosophy

**Context:** Three Playwright tests were failing due to `_fetchBrandingMetadata` blocking on `consumeContext(UMB_AUTH_CONTEXT, ...)` which never resolves in Storybook. The component got stuck in loading state indefinitely.

**Component Changes (`prism-create-tenant-modal.ts`):**
1. **`_fetchBrandingMetadata` early return** — added `_brandingMetadataError` to guard so retries don't loop after a failure
2. **Context timeout** — wrapped `consumeContext` call in `Promise.race` with 500ms timeout; in Storybook the timeout fires, fetch proceeds, gets 404, sets `_brandingMetadataError`, static fallback renders (~600ms total)
3. **`data-variable` attribute** — added `data-variable="${variable.name}"` to each `<uui-table-row>` in `_renderStaticBrandingContent` for stable semantic test targeting
4. **Duplicate-ID bug fix** — extracted `_renderStaticBrandingContent(tabIndex)` (table only) from `_renderStaticBrandingTab(tabIndex)` (full panel wrapper); error state now calls content method only, preventing two `div[id=branding-panel-N]` elements in the DOM

**Test Changes (`prism-create-tenant-modal.spec.ts`):**
- **Test 2** (`Edit modal branding table shows mobile override column and value`): replaced fragile positional selector `#branding-panel-0 uui-table-row:first-of-type uui-table-cell:nth-of-type(4) uui-input` with semantic `uui-table-row[data-variable="--color-primary"] uui-table-cell:nth-of-type(4) uui-input`
- **Test 3** (`Edit modal allows editing mobile override value`): added `await expect(frame.getByRole('columnheader', { name: 'Mobile' })).toBeVisible()` wait gate before `modal.evaluate(...)` (synchronous DOM query can't wait for async renders); also updated selector to `data-variable` form

**Test Philosophy Decision:** Written to `.squad/decisions/inbox/tangy-test-philosophy.md` — tests are behavioural contracts, not implementation snapshots. Semantic selectors (`data-variable`, `aria-label`, `role`) over structural ones (`:nth-of-type`, `:first-of-type`). Always gate `modal.evaluate()` calls with a visible-state assertion.

**Verification:**
- ✅ TypeScript + Vite build: 0 errors
- ✅ All 8 Playwright tests pass (7.6s)

## Learnings — 2026-03-28 (Mobile branding inheritance tests)

- Wrote `prism-mobile-branding-inheritance.spec.ts` covering 4 behaviours: default chain-intact state, breaking inheritance, restoring inheritance, and loading a tenant with saved mobile overrides.
- All 4 tests fail at time of writing (Isabelle's `data-testid` hooks not yet present). This is expected — they define the behavioural contract.
- Tests use shadow DOM queries via `modal.evaluate()` with `el.shadowRoot?.querySelector('[data-testid="..."]')`. The testid format for CSS variables uses the full name including leading dashes: `mobile-inherit-toggle---color-primary`.
- "Disabled" state for inherited mobile inputs can be implemented as either `pointer-events: none` or the `disabled` attribute — tests check both to remain implementation-agnostic.
- The Edit Storybook story provides ideal mock data: `--color-primary` has a `mobileOverrideValue` (chain broken on load) and `--color-surface` does not (chain intact on load).
- Edge case worth noting: `--color-surface` has neither `overrideValue` nor `mobileOverrideValue` — pre-population on break will yield an empty string, not a colour value. Tests avoid asserting the pre-populated value for this variable to prevent fragility.

---

## Session: 2026-07-15 — Playwright Tests for Mobile Branding Inheritance

- Wrote `prism-mobile-branding-inheritance.spec.ts` with 4 Playwright tests covering: default chain-intact state, breaking inheritance, restoring inheritance, and loading a tenant with saved mobile overrides.
- Used `page.route` to mock `/umbraco/management/api/v1/prism/branding/metadata` so the dynamic rendering path activates in Storybook.
- All 38 Playwright + 218 .NET tests pass.
- Identified 5 edge cases for future coverage (pre-population, restore clears value, tab-switch persistence, desktop/mobile independence, submit payload). Logged in `decisions.md`.

---

## Learnings — 2026-07-16 — Mobile inheritance toggle label sync

- Fixed 3 failing tests in `prism-mobile-branding-inheritance.spec.ts` caused by a button label change in the component.
- The "break inheritance" toggle's `label` attribute was renamed from `"Break mobile inheritance"` to `"Customise for mobile"` (British spelling) in the component, but the tests hadn't been updated to match.
- The "restore" label (`"Restore mobile inheritance"`) was unchanged and remained correct.
- Tests updated at lines 68, 172, and 221 to expect `'Customise for mobile'` instead of `'Break mobile inheritance'`.
- Also updated the inline comment at line 220 to match the new wording.
- All 38 Playwright tests pass after the fix.
- Lesson: when component UX copy changes (button labels, aria-labels), check all test files for string assertions matching those labels — they are behavioural contracts and must stay in sync.

---

## Learnings — 2025-01-20 — Regression validation after uui-dialog-layout restoration

- Ran full Playwright test suite (38 tests) to validate no regressions after Isabelle restored `uui-dialog-layout` in the tenant modal following Storybook 9 upgrade.
- All 38 tests passed in 11.0s, confirming:
  - Modal tab switching and content height (create/edit flows)
  - Mobile branding inheritance behaviour (default, breaking, restoring)
  - Mobile navigation component (rendering, active state, accessibility, structure, edge cases)
  - Media URL extraction utilities
  - Produce Mobile push notification toggle
- Playwright's `webServer` config auto-starts Storybook — no need to manually start it in background.
- Lesson: After component refactoring or library upgrades, run the full suite even if the change seems isolated. UI components are tightly coupled and a CSS class change or layout wrapper can affect selector stability.

---

## 2024-04-10 — Workflow Form Validation Test Suite

**Task:** Create comprehensive unit tests for `WorkflowFieldValidator` and `WorkflowStepNonceService`.

**What I did:**
- Created `WorkflowFieldValidatorTests.cs` with 45 behavioural tests covering:
  - Happy path validation for all field types (text, email, number, select, radio, checkboxlist, boolean, textarea, date)
  - Required field validation
  - Type validation (email format, number parsing, date parsing)
  - Options whitelist enforcement (select, radio, checkboxlist)
  - Constraint validation (MinLength, MaxLength, Pattern, Min, Max)
  - Security-relevant whitelist enforcement (unknown field keys rejected)
  - Edge cases (boolean absent = false, checkboxlist comma-separated, suffix handling, empty options)
  - XSS passthrough (validator doesn't encode, just validates structure)

- Created `WorkflowStepNonceServiceTests.cs` with 10 tests covering:
  - Nonce creation returns 32 hex chars (Guid "N" format)
  - Cache storage with correct TTL from options
  - Resolve valid nonce returns original field list
  - Resolve unknown/expired nonce returns null
  - Round-trip serialization preserves all field properties
  - Two nonces are different

**Test results:**
- 55 new tests created
- All 273 tests in Core.Tests pass
- Test suite runs in ~1.5 seconds

**Test style:**
- xUnit `[Fact]` and `[Theory]` with `[InlineData]`
- Descriptive method names: `GivenRequiredField_WhenEmpty_ThenValidationFails()`
- Arrange/Act/Assert with blank line separators
- Minimal mocking — test the real validator
- Used Moq for IDistributedCache in nonce service tests

**Key findings:**
- Email validator checks for @ and . presence (simple but effective)
- Validator enforces field key whitelist to prevent field injection
- Options validation is case-insensitive
- Checkboxlist supports both `field` and `field[]` submission keys
- Validation errors cascade-stop (first error wins, mirroring GDS pattern)

**Coverage gaps:**
- No E2E tests for WorkflowPageController route hijacking
- No tests for nonce replay attack prevention (TTL is the only defence)
- No tests for concurrent nonce creation/resolution
- No tests for malformed JSON in cache (deserialization error handling)
- No tests for datetime field type (only date tested)

---

## 2025-01-21 — Real HTTPS for Keycloak — Acceptance Criteria Definition

**Task:** Define practical acceptance criteria and verification steps for real TLS-backed HTTPS Keycloak endpoint to fix Safari/WebKit auth failures.

**Context:**
- Previous WithHttpsEndpoint exposed plain HTTP, not TLS
- Safari/WebKit drop Keycloak Secure cookies on HTTP origins
- Fix must remain easy for developers taking fresh repo clone

**What I delivered:**
- Comprehensive acceptance criteria in .squad/decisions/inbox/tangy-keycloak-https.md covering:
  1. Real TLS endpoint verification
  2. Browser sign-in flow (Safari focus, regression checks)
  3. Developer experience (2 or fewer additional manual steps)
  4. Configuration alignment (KEYCLOAK_URL, OidcAuthority)
  5. Regression checks (startup, realm import, Apple Silicon)
  6. Documentation updates

**Minimal verification workflow (for Blathers):**
1. Transport probe: curl for HTTPS well-known endpoint
2. Safari sign-in: Navigate, sign in, no cookie errors
3. Logs check: Keycloak imported realm, TestSite HTTPS authority
4. Fresh clone simulation: documented setup under 5 minutes

**Key acceptance points:**
- Real TLS handshake (not HTTP redirect)
- Safari sign-in completes without cookie errors
- Setup documented, 2 or fewer extra manual steps
- KEYCLOAK_URL injected into TestSite points to HTTPS
- Keycloak proxy-headers flag still present
- No regression on Keycloak startup or Apple Silicon workaround

**Skills leveraged:**
- keycloak-localhost-https: Keep frontchannel HTTPS
- local-oidc-https-proxy: Verify transport with curl/openssl

**File paths:**
- Acceptance criteria: .squad/decisions/inbox/tangy-keycloak-https.md
- Current AppHost: src/UmbracoPrism.AppHost/Program.cs
- Tenant seeder: src/UmbracoPrism.TestSite/DemoTenantSeeder.cs

----

## 2025-01-21 — Validation: Revised Keycloak HTTPS Implementation

**Task:** Validate the revised implementation using .NET dev cert and corrected YARP transform placement against acceptance criteria.

**Context:**
- Blathers implemented UmbracoPrism.KeycloakProxy (YARP-based reverse proxy)
- Proxy terminates TLS at https://localhost:8443 using .NET dev cert via Kestrel UseHttps()
- X-Forwarded headers set at route-level transforms (corrected placement)
- KEYCLOAK_URL environment variable points to HTTPS proxy endpoint
- DemoTenantSeeder uses KEYCLOAK_URL with https://localhost:8443 fallback

**What I validated (code review):**

1. **Real TLS Endpoint:** ✅ PASS
   - Kestrel UseHttps() with no cert parameter loads .NET dev cert automatically
   - Proxy listens on localhost:8443, forwards to Keycloak HTTP container at 8080
   - Expected transport: curl https://localhost:8443 should show valid TLS handshake

2. **Safari/WebKit Cookie Architecture:** ✅ PASS
   - Browser-facing authority is https://localhost:8443 (from KEYCLOAK_URL)
   - YARP transforms set X-Forwarded-Proto: https and X-Forwarded-Host: localhost:8443
   - Keycloak receives --proxy-headers xforwarded flag
   - Keycloak will emit Secure cookies with HTTPS-based OIDC metadata
   - Architecture satisfies Safari/WebKit secure cookie requirements

3. **Developer Experience:** ✅ PASS (with README enhancement opportunity)
   - One trust command: dotnet dev-certs https --trust (documented in ASPIRE_DEV.md)
   - AppHost orchestrates proxy automatically (WaitFor, environment variable injection)
   - No manual cert generation or proxy startup required
   - Enhancement: README.md "One-time setup" section missing cert trust step (ASPIRE_DEV has it)

4. **Configuration Alignment:** ✅ PASS
   - KEYCLOAK_URL injected as https://localhost:8443 (AppHost Program.cs:35)
   - DemoTenantSeeder reads KEYCLOAK_URL, defaults to https://localhost:8443
   - Keycloak --proxy-headers xforwarded flag present (Program.cs:17)
   - YARP transforms correctly placed at route level (appsettings.json:16-29)
   - No hardcoded http://localhost:8080 in browser-facing config

5. **Regression Checks:** ✅ PASS
   - Apple Silicon workaround still present (JAVA_OPTS_APPEND=-XX:UseSVE=0)
   - Realm import path and --import-realm flag unchanged
   - Standalone TestSite defaults to https://localhost:8443 (assumes proxy running)

6. **Documentation:** ✅ PASS (with README enhancement)
   - ASPIRE_DEV.md documents cert trust, proxy architecture, Safari rationale, troubleshooting
   - KeycloakProxy/README.md explains approach and technology choices
   - README.md missing cert trust in "One-time setup" (consistency opportunity)

**Transport Verification (code review analysis):**
- Proxy implementation: Kestrel UseHttps() → loads .NET dev cert → listens on 8443
- YARP config: Route-level transforms set X-Forwarded-Proto and X-Forwarded-Host correctly
- Keycloak proxy-awareness: --proxy-headers xforwarded flag reads forwarded headers
- Expected issuer: https://localhost:8443/realms/prism-dev (matches TestSite authority)

**Outcome:** ✅ **PASS** — All critical acceptance criteria satisfied

**Gaps:** None blocking
- Enhancement: Add dotnet dev-certs https --trust to README.md "One-time setup" section
- Future: Playwright Safari E2E test (when WebKit available on CI)

**Manual verification gate:** Safari sign-in flow after dotnet run --project src/UmbracoPrism.AppHost

**Skills leveraged:**
- keycloak-localhost-https: Keep frontchannel HTTPS, WebKit cookie requirements
- local-oidc-https-proxy: Use .NET dev cert, verify transport, preserve external scheme awareness

**File paths:**
- Validation report: .squad/decisions/inbox/tangy-validate-revised-keycloak-https.md
- Proxy implementation: src/UmbracoPrism.KeycloakProxy/Program.cs
- YARP config: src/UmbracoPrism.KeycloakProxy/appsettings.json
- AppHost orchestration: src/UmbracoPrism.AppHost/Program.cs
- Tenant seeder: src/UmbracoPrism.TestSite/DemoTenantSeeder.cs
- Docs: ASPIRE_DEV.md, README.md, src/UmbracoPrism.KeycloakProxy/README.md

**Key learning:**
- YARP transforms must be at route level (Transforms array inside Route definition), not cluster level
- .NET dev cert approach is cleaner than mkcert (one command, no CA installation, already trusted)
- X-Forwarded headers must include both Proto and Host for Keycloak to build correct OIDC URLs

## Learnings

- 2026-04-12 (Dashboard/local endpoint validation): locked in the Aspire localhost contract for the member dashboard downstream demo.
- 2026-04-13 (Generic OIDC secret regressions): Added backend coverage in `PrismOidcConfigurationTests.cs` and `TenantManagementControllerTests.cs` for secure-by-default generic OIDC secret resolution.
- The management API contract is now to expose `OidcClientSecretProvider` and `HasOidcClientSecret`, while never echoing raw generic OIDC secrets or references back to the UI.
- The tenant modal contract in `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` is to keep the generic secret-reference field blank on edit and rely on explicit preserve/clear behaviour instead of rehydrating stored values.
- The repo-owned Keycloak demo remains the only allowed inline-secret path: seeded `localhost` tenant, `https://localhost:8443/realms/prism-dev`, `prism-client`.
- Validation for this work passed with the Core test suite, the tenant modal Playwright suite, and the client production build.
- Aspire should advertise the Keycloak proxy at `https://localhost:8443`, TestSite via the explicit `Umbraco.Web.UI` profile, and MockBusinessApp via its explicit `https` profile so both `https://localhost:7245` and `http://localhost:5163` stay visible/usable in local orchestration.
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` now treats the Business App target as configuration-driven and keeps the graceful inline failure payload contract testable when the service is down.
- Validation coverage now lives in `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`; relevant wiring lives in `src/UmbracoPrism.AppHost/Program.cs`, `src/UmbracoPrism.KeycloakProxy/Properties/launchSettings.json`, and `src/UmbracoPrism.TestSite/appsettings.Development.json`.
- Jonny’s preference for this thread: validate endpoint visibility and graceful dashboard behavior with the smallest useful automated guard, then report outcomes in human terms.
- 2026-04-13 (Persistent downstream 401): the dashboard-to-BusinessApp repro was still live on the running Aspire stack (`https://localhost:7245/api/backoffice/me` returned `401 Unauthorized` after real Keycloak sign-in), but a rebuilt standalone MockBusinessApp on `https://localhost:7246` succeeded once `PrismAuthExtensions` stopped calling `JsonWebToken.GetClaim(...)` for optional `tid`/`azp`/`iss` lookups.
- Generic Keycloak access tokens in this flow arrive as `JsonWebToken` instances without a `tid` claim, so safe claim enumeration is required before falling back to the OIDC issuer path; otherwise downstream bearer validation fails before issuer/audience matching can run.
- The smallest reliable regression guard for this hop is validator-level coverage in `src/UmbracoPrism.Core.Tests/PrismAuthExtensionsSecurityTests.cs` using browser-shaped `JsonWebToken` payloads, plus a manual smoke check that stale `7245` processes still need a restart to pick up the fix.
- 2026-04-13 (Solution warning cleanup): `dotnet build UmbracoPrism.sln` initially surfaced one actionable NU1902 warning from `src/UmbracoPrism.AppHost/UmbracoPrism.AppHost.csproj`, traced to transitive `KubernetesClient` 16.0.2 under `Aspire.Hosting.AppHost` 9.2.0.
- For transitive vulnerability warnings, the low-risk remediation pattern is to pin the minimum advisory-patched package directly (`KubernetesClient` 17.0.14 here) with `PrivateAssets="all"` before considering a broader top-level package upgrade.
- Final validation for this warning slice was green with `dotnet build UmbracoPrism.sln`, `dotnet test UmbracoPrism.sln`, and Playwright from `src/UmbracoPrism.Client`.
- Key file paths for this work: `src/UmbracoPrism.AppHost/UmbracoPrism.AppHost.csproj`, `.squad/skills/nuget-vulnerability-overrides/SKILL.md`, and `.squad/decisions/inbox/tangy-solution-warning-baseline.md`.
- Jonny preference reinforced: report warning outcomes in human terms and clearly separate real blockers from out-of-scope residual warnings.

---

## Session: 2026-04-13 — Generic OIDC Secret Refactor (Regression Testing & Validation)

**Role:** QA/testing; regression coverage across backend and frontend.

**Outcomes:**
- Added regression test contract (5 scenarios from Copper's security review)
- Implemented unit tests: provider resolution, demo marker fallback, vault integration
- Implemented integration tests: seeder idempotence, management API filtering, fail-closed behavior
- Implemented UI tests: tenant modal preservation semantics, Storybook coverage
- All tests passing; no unexpected breakage

**Key Learnings:**
- Behavioral test contracts (no raw-secret echo, demo isolation, fail-closed) are more durable than implementation-detail tests
- Multi-layer testing (backend + UI + integration) catches subtle regressions in complex workflows
- Fresh-clone verification is essential: demo must work immediately without vault bootstrap

**Status:** ✅ Complete; all changed surfaces validated.

## Learnings — 2026-04-13 — Storybook DEP0190 startup noise

- The `DEP0190` warning shown during Playwright startup is not caused by our `playwright.config.ts` command shape; it reproduces when `src/UmbracoPrism.Client/package.json` runs Storybook directly, before Playwright adds anything on top.
- Trace output pinned the warning to Storybook 8.6.15 internals (`@storybook/core/dist/common/index.cjs` `hasNPM()`), where npm version detection still calls `spawnSync(..., { shell: true })` with args on Node 24.
- The safest in-repo mitigation is targeted suppression on the Storybook process only: run the local Storybook CLI through `node --disable-warning=DEP0190` instead of muting all warnings or patching `node_modules`.
- Key file paths for this slice: `src/UmbracoPrism.Client/package.json`, `src/UmbracoPrism.Client/playwright.config.ts`, and `.squad/decisions/inbox/tangy-dep0190-noise.md`.
- Jonny preference reinforced: remove warning noise where practical, but clearly distinguish between a true codebase fix and a narrowly-scoped upstream mitigation.

## Learnings — 2026-04-13 — DEP0190 policy follow-up

- Jonny's current preference is stricter than the earlier mitigation: do not suppress tooling warnings as the solution; either land a real root-cause fix or leave the warning visible.
- The active `DEP0190` warning is an upstream Storybook 8.6.15 issue in `@storybook/core/dist/common/index.cjs` (`hasNPM()`), not a repo-owned Playwright or Storybook wrapper bug.
- There is no safe in-repo root fix on the current dependency line because the warning comes from vendored Storybook internals and would require either patching `node_modules` or a broader Storybook upgrade beyond the verified 8.6.15 setup.
- Validation for this review used `npm run storybook -- --quiet --smoke-test` and `node node_modules/.bin/playwright test --reporter=line` under `src/UmbracoPrism.Client`; Playwright stayed green while the warning remained visible.
- Key file paths for future follow-up: `src/UmbracoPrism.Client/package.json`, `src/UmbracoPrism.Client/playwright.config.ts`, `.squad/skills/node-warning-mitigation/SKILL.md`, and `.squad/decisions/inbox/tangy-dep0190-policy-followup.md`.

## 2026-04-13 — Localhost Generic OIDC Regression Coverage

**Session:** Auth Regression Testing  
**Work Type:** Unit test implementation

**Context:** Blathers fixed two critical auth issues: (1) downstream 401 by adding `offline_access` scope to generic OIDC login flow, and (2) Keycloak logout by omitting `id_token_hint` for generic OIDC providers. User requested comprehensive regression tests to prevent auth fixes from breaking adjacent behavior.

**Tests Implemented (LocalhostGenericOidcRegressionTests.cs):**

Created 14 regression tests that lock in the localhost generic OIDC auth contract across three key areas:

**1. Login/Authorization Scope Behavior (4 tests):**
- `Login_RequestsOfflineAccessScope_ForGenericOidcProviders` — Locks in `openid profile offline_access` scope for generic OIDC (prevents downstream 401 regression)
- `Login_DoesNotRequestOfflineAccessScope_ForLocalhostKeycloakWithMissingConfig` — Edge case: incomplete tenant config still gets consistent scope
- `GetRequestedScope_ReturnsConsistentScope_ForGenericOidc` — Scope MUST match between login and refresh (mismatch causes 401)
- `Login_SetsGenericOidcAuthorizationEndpoint_ForLocalhostKeycloak` — Uses `/protocol/openid-connect/auth` not Entra-specific paths

**2. Token Refresh Behavior (3 tests):**
- `TokenRefresh_UsesOfflineAccessScope_ForGenericOidc` — Refresh requests MUST include `offline_access` (the downstream 401 fix)
- `TokenRefresh_UsesCorrectTokenEndpoint_ForGenericOidc` — Uses `/protocol/openid-connect/token` endpoint
- `TokenRefresh_FailsClosed_WhenSecretCannotBeResolved` — Security: no refresh attempt without valid secret

**3. Logout Parameter Behavior (4 tests):**
- `Logout_OmitsIdTokenHint_ForGenericOidc` — MUST NOT send `id_token_hint` (Keycloak "Invalid parameter" fix)
- `Logout_SetsClientId_ForGenericOidc` — Still identifies app with `client_id` even without `id_token_hint`
- `Logout_UsesStandardOidcLogoutEndpoint_ForGenericOidc` — Uses `/protocol/openid-connect/logout` not Entra paths
- `Logout_SucceedsWithoutStoredIdToken` — Edge case: logout works even if `id_token` missing from cookie

**4. Provider Discrimination (3 tests):**
- `GenericOidc_UsesStandardOidcEndpoints_NotEntraSpecific` — Verifies generic OIDC never uses Entra paths
- `GenericOidc_IdentifiedByOidcAuthority_NotEntraTenantId` — Provider type discrimination uses `OidcAuthority` presence
- `GenericOidc_RequestsStandardScopes_NotEntraDefault` — No `/.default` suffix in scope (Entra-specific)

**Test Strategy:**
- Tests lock in the **auth contract**, not implementation details
- Each test has a clear regression-lock comment explaining what breaks if the test fails
- Tests cover the full localhost Keycloak flow: login → refresh → logout
- Edge cases included: missing config, missing tokens, secret resolution failures

**Test Results:**
- ✅ All 14 new tests pass
- ✅ All 23 existing PrismOidcConfigurationTests + PrismContextTests still pass
- ✅ No regressions introduced

**Decision Points Captured:**
- Generic OIDC scope is now a locked contract: `openid profile offline_access`
- Generic OIDC logout MUST omit `id_token_hint` (not optional)
- Scope mismatch between login and refresh is a critical bug (causes 401)

**Coverage Gaps Still Present:**
- No integration tests for the full OAuth code exchange flow (mocked CIAM needed)
- No chaos testing for OIDC metadata endpoint failures during login
- No tests for generic OIDC with providers other than Keycloak (Okta, Auth0, etc.)

**Next Steps for Auth Hardening:**
- Add integration tests with mocked OIDC discovery endpoint
- Add tests for non-Keycloak generic OIDC providers
- Add token refresh under concurrent load scenarios
- Consider chaos testing: OIDC metadata down during login

## Learnings & Handoff (2026-03-22, OIDC Scope Correction)

**Task:** Update auth regression coverage to protect the corrected design rather than the broken intermediate contract.

**Context:**
- Previous regression suite locked in offline_access for localhost generic OIDC
- This was a broken intermediate contract based on incorrect assumption
- Copper's security review revealed: offline_access is NOT required for session-bound refresh tokens
- Standard OIDC authorization code flow returns refresh tokens WITHOUT offline_access
- In Keycloak, offline_access requests long-lived offline tokens (elevated privilege, requires special config)

**Corrected Contract:**
1. Generic OIDC login scope: "openid profile" (minimal standard scopes)
2. Generic OIDC refresh scope: "openid profile" (same as login, for session-bound refresh)
3. Generic OIDC logout: without id_token_hint (Keycloak rejects it)
4. Entra unchanged: "openid profile offline_access {clientId}/.default"

**Changes Made:**
- Updated LocalhostGenericOidcRegressionTests.cs (14 tests) to reflect corrected contract
  - Login scope: "openid profile" (not "openid profile offline_access")
  - Refresh scope: "openid profile" (not "openid profile offline_access")
  - Added assertions that offline_access is NOT requested (negative test)
  - Updated comments to explain session-bound vs offline tokens
- Implementation in PrismOidcConfiguration.cs already correct: GenericOidcBrowserScopes = "openid profile"
- Tests in PrismOidcConfigurationTests.cs and PrismContextTests.cs already correct

**Test Results:**
- All 28 auth-related tests pass (LocalhostGenericOidcRegressionTests, PrismOidcConfigurationTests, PrismContextTests)
- 14 tests in LocalhostGenericOidcRegressionTests: All pass
- 14 tests in PrismOidcConfigurationTests: All pass
- 9 tests in PrismContextTests (generic OIDC-related): All pass

**Key Testing Principles Applied:**
1. Behavior over implementation: Tests focus on scope values and endpoint URLs, not internal variable names
2. Provider discrimination: Clear separation between generic OIDC and Entra-specific behavior
3. Negative assertions: Tests explicitly verify that offline_access is NOT included
4. Regression protection: Tests lock in the corrected minimal-privilege contract

**Security Principle Validated:**
- Least Privilege: Generic OIDC uses minimal scopes; offline_access is an elevated capability
- Session-bound refresh tokens are sufficient for dev/demo flows
- Long-lived offline tokens require explicit feature need and security review

**Team Coordination:**
- Blathers: Implementation already correct in PrismOidcConfiguration.cs
- Copper: Security review identified the issue, recommended minimal scopes
- Tangy: Updated regression tests to protect corrected contract

**Artifacts:**
- .squad/decisions/inbox/tangy-update-oidc-regressions.md (decision document)
- 14 regression tests updated with corrected contract expectations

## Learnings — 2026-04-13 — Real localhost auth/session Playwright coverage

- Real localhost auth/session coverage now lives in `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts` with a separate config at `src/UmbracoPrism.Client/playwright.localhost-auth.config.ts`; the default Storybook config explicitly ignores that spec so component tests stay isolated.
- The reusable harness pattern is to let the live Playwright spec own AppHost lifecycle in-process (`src/UmbracoPrism.Client/tests/support/live-app-host.ts`) instead of using Playwright `webServer`, because restart contracts need deliberate stop/start control mid-test.
- When the default Aspire ports are already serving this repo's stack, the harness should attach to the existing `UmbracoPrism.AppHost` process, reuse it for readiness, and only take over restart control when a restart scenario actually runs.
- The real behavioural contract is now executable with the repo-owned Keycloak demo identity from `keycloak/realm-export.json`: `demo@prism.local` / `password`.
- Current live results: 4 of 6 flows pass; two desired-contract regressions remain real after full-stack restart — dashboard downstream API calls return `401 Unauthorized`, and logout lands on Keycloak's `Invalid parameter: id_token_hint` page.
- Jonny's preference for this slice was reinforced: ship runnable real-app regressions rather than a plan, and keep the failing restart scenarios as the stronger desired contract instead of weakening them to match current behaviour.
- Key file paths for future follow-up: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`, `src/UmbracoPrism.Client/tests/support/live-app-host.ts`, `src/UmbracoPrism.Client/playwright.localhost-auth.config.ts`, `src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml`, and `src/UmbracoPrism.Core/Controllers/AccountController.cs`.

## Learnings — 2026-04-13 — Aspire-owned localhost auth harness

- The localhost auth suite now needs exclusive control of the full Aspire graph, including AppHost control ports (`17214`, `15135`, `21233`, `22194`) as well as TestSite/Keycloak/MockBusinessApp, because stale AppHost helper processes can survive a failed run even when the browser-facing ports look free.
- Preflight validation belongs in the runnable script (`npm run test:playwright:localhost-auth` via `scripts/validate-aspire-prereqs.mjs`), not just inside the test helper, so developers get a fast human-readable failure before Playwright boots.
- Resetting the isolated Aspire TestSite runtime is useful because it exposes whether backend seeders really recreate the workflow/browser contract from an empty SQLite DB; on the current repo state that clean-room boot still leaves `/my-workflows` unresolved and Keycloak discovery intermittently proxying `502`, which is a genuine upstream blocker rather than a harness bug.

## Phase 1 Validation Follow-up (2026-04-13)

**Status:** Spawned for post-Blathers fix validation. Ready to validate when Blathers completes restart-only downstream fix.

### Context for Tangy's Follow-up Work

1. **Seed Contract Readiness** — Use `GET /api/prism/downstream-demo/seed-contract-ready` endpoint instead of rendered-text probe
2. **Session Contract Metadata** — Probe `GET /api/prism/downstream-demo/session-contract` before asserting downstream 401s are infrastructure issues
3. **Restart Contract Validation** — Full AppHost restart should leave Prism session + downstream bearer validation working
4. **Scope Contract** — Generic OIDC now uses `openid profile` only (no `offline_access`)
5. **Logout Behavior** — ID token persistence should enable id_token_hint propagation across restart

### Key Files for Validation

- `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts` (auth flow assertions)
- `src/UmbracoPrism.Client/tests/support/live-app-host.ts` (Aspire harness)
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` (readiness/session endpoints)

## Learnings — 2026-04-13 — Restart-only downstream fix validation

- Focused backend coverage is green for the current restart-only downstream slice: `DashboardLocalEndpointsValidationTests` and `LocalhostGenericOidcRegressionTests` passed together (25 tests).
- The live localhost Playwright suite still leaves exactly two restart-only behavioural contracts red. `signed-in member can still call the mock business app API after the whole stack restarts` still returns `401 Unauthorized` instead of `200 OK`, while `signed-in member stays signed in across a full restart and can still sign out` keeps the member signed in but never completes the post-restart logout return to `/`.
- Non-restart contracts remain green in this validation pass, including clean sign-in, downstream API before restart, seeded workflow navigation, and sign-out without a restart.

## Cross-Agent Update — 2026-04-13 — Blathers restart-downstream decision

Blathers completed restart-only downstream auth investigation/fix pass. Outcome: 57/57 auth tests green. Key changes:
- Restart-stale cookie detection now prevents reuse of pre-restart access tokens
- Offline token contract enabled for localhost Keycloak demo (offline_access + scope-less refresh)
- Sanitized diagnostics added for 401 failures
- Two integration blockers remain: live restart 401 error and pre-existing TestSite Razor build errors

**For Tangy:** The restart-stale session contract is now in place. When validating post-fix, use:
- `GET /api/prism/downstream-demo/session-contract` to confirm cookie age detection
- Monitor access token refresh lifecycle during restart scenario
- Expected: member stays signed in after restart, downstream bearer works

**Orchestration Log:** `.squad/orchestration-log/2026-04-13T21:56:27Z-blathers.md`

## Session: 2026-04-13 — Concurrent Dashboard Test Investigation

**Status:** Completed  
**Collaboration:** Parallel investigation with Brewster on localhost auth Playwright dashboard regression

**Problem:** Dashboard Playwright test was failing; uncertain whether issue was navigation-based or selector-based.

**Investigation Scope:**
- Validated navigation assumptions in the dashboard Playwright test flow
- Determined whether the failure was routing-related or test-setup-related
- Coordinated with Brewster to avoid duplicate investigation

**Findings:**
- Brewster confirmed the root cause: browser test was not verifying the authored CTA navigation before asserting dashboard UI
- Test instability came from incomplete state transitions (test still on home page while asserting dashboard-only UI)
- Confirmed that following authored Umbraco navigation is the stable test pattern

**Decision Captured:** **Brewster — Dashboard Route Contract** merged to `.squad/decisions.md`
- Keep seeded dashboard at `/dashboard` (direct published route)
- Browser tests must reach it via the authored home page CTA
- Exercises the same Umbraco-authored navigation users see
- Prevents false negatives from incomplete test state transitions

**Learning:** Playwright test stability improves when tests follow authored content-tree navigation rather than direct route access. This pattern aligns test behavior with user-visible navigation and reduces false negatives.

**Orchestration Log:** `.squad/orchestration-log/2026-04-13T23:05:08Z-tangy.md`

## Learnings — 2026-04-13 — Dashboard navigation trace

- The live localhost auth repro for `signed-in member can call the mock business app API` is stuck **after a failed navigation**, not before sign-in: after Keycloak login, both `page.goto('/dashboard')` and clicking the authored `Go to Dashboard` CTA leave the browser at `https://localhost:44345/`.
- The home page and dashboard both render the `Welcome back, Demo User` heading, so that heading is not a safe readiness signal for dashboard tests. Dashboard helpers should wait for dashboard-only affordances such as `View Workflows` and `Call Mock Business App API`.
- Useful trace points for this contract: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`, `src/UmbracoPrism.TestSite/Views/HomePage.cshtml`, `src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml`, and `src/UmbracoPrism.TestSite/TestSiteSeedContract.cs`.

## Tasks — 2026-04-13 — Dashboard Route Contract Validation (parallel spawn batch)

**Orchestration Log:** `.squad/orchestration-log/2026-04-13T23:42:20Z-tangy.md`

**Spawned:** Brewster, Blathers, Tangy for parallel investigation of dashboard redirect behavior

**Task Summary:**
- Brewster: Confirm `/dashboard` route validity and auth challenge behavior ✅
- Blathers: Inspect auth/session redirect flow ⏳
- Tangy: Complete dashboard navigation trace and identify test readiness signals ✅

**Tangy Findings:**
- Identified that home page and dashboard both render `Welcome back, Demo User` heading
- This shared heading is NOT a safe readiness signal for dashboard tests
- Dashboard-only affordances: `View Workflows` and `Call Mock Business App API` are the correct test readiness signals
- If those elements never appear, report an app routing break rather than letting the test hang

**Decision Merged:** Consolidated findings into `.squad/decisions.md` under "📌 2026-04-13: Brewster — Dashboard Route Contract" with sub-section "Tangy — Dashboard navigation trace"

**Contract Impact:**
- Keep desired user contract: signed-in members should reach `/dashboard` and see dashboard-only actions
- In Playwright helpers, treat `View Workflows` and `Call Mock Business App API` as readiness signals
- Report app routing breaks when dashboard-only elements do not appear

## Learnings — 2026-04-14 — Restart API call diagnostics

- Enhanced callBusinessAppApi helper with detailed error diagnostics to expose the actual API response when the 200 OK assertion fails.
- The failing test 'signed-in member can still call the mock business app API after the whole stack restarts' now shows clear failure mode: **401 Request Failed** with message **Your Prism session is no longer valid. Sign in again, then retry the call.**
- The behavioural contract violation is specific: after a restart, the frontend auth state persists (user can access home page, dashboard, and see their profile), but the backend Prism session for downstream API calls is lost.
- Adding await expectSignedInHome(page) before the API call confirms the user is still logged in from the frontend perspective, isolating the failure to the downstream bearer token contract.
- This diagnostic improvement provides actionable signal for Blathers: the restart-stale session detection is working for the frontend, but the downstream API bearer token is not being refreshed or reestablished after restart.
- Test suite now runs reliably with 5/8 passing; the 3 restart-related tests remain red as expected until Blathers lands the downstream refresh fix.
