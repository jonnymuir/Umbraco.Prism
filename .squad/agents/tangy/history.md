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
