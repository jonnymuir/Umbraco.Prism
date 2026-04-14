# Blathers — History




## 📋 Recent History

Previous history archived to reduce file size. Recent entries below.

---

   - Keycloak browser auth flow now requests `offline_access` scope
   - Enables token refresh across full stack restarts without requiring browser re-auth
   - Keycloak realm export updated with offline_access configuration

3. **Scope-Aware Token Refresh**
   - Localhost refresh token grant omits `scope` parameter
   - Allows Keycloak to reuse offline scopes already carried by the refresh token
   - Aligns with OIDC refresh best practices

4. **Sanitized Auth Failure Diagnostics**
   - 401 responses include diagnostic context without exposing credentials
   - Failure chain visible in logs for debugging restart edge cases
   - Device-friendly error reporting for downstream auth issues

### Test Coverage

- ✅ Focused auth test set: 57/57 passing
- ✅ `PrismContextTests`: restart-stale detection tests passing
- ✅ `LocalhostGenericOidcRegressionTests`: offline refresh tests passing
- ✅ Keycloak realm export validation passing

### Remaining Blockers

1. **Live Restart Regression (401)**
   - Full stack restart still results in 401 from MockBusinessApp
   - Symptoms: pre-restart access token rejected after Keycloak restart
   - Root cause investigation needed: token expiry vs revocation during restart cycle

2. **Pre-existing TestSite Razor Build Errors**
   - Blocks normal Playwright/AppHost test path
   - Unblocks: Fix Razor compilation issues before running full integration suite

### Architecture Decisions

- Added `RestartStaleSessionHandler` in `PrismContext`
- Implemented `OfflineTokenRefreshContract` for localhost demo
- Keycloak realm export: offline_access + minimal scope refresh pattern
- Diagnostic context preserved without security exposure

### Follow-up

- Blathers: Investigate live restart 401 root cause (token lifecycle during Keycloak restart)
- Tangy: Validate live suite behavior after Razor build errors resolved


## Learnings (2026-04-14, Restart 401 Fix — COMPLETE)

**Issue:** Playwright test "signed-in member can still call the mock business app API after the whole stack restarts" failing with 401 after appHost.restart().

**Root Causes Identified:**

1. **Keycloak session loss on restart:** In-memory H2 database lost refresh_tokens when Keycloak container restarted, making token refresh impossible.

2. **Signing key cache cooldown blocking fresh key fetches:** When Keycloak restarted and generated new signing keys, MockBusinessApp's PrismSigningKeyCache had a 30-second forced-refresh cooldown that prevented fetching the latest keys when a token with an unknown keyId arrived, causing 401 validation failures.

**Fixes Applied:**

1. **Keycloak session persistence** (UmbracoPrism.AppHost/Program.cs):
   - Added bind mount: keycloakDataRoot to /opt/keycloak/data/h2
   - H2 database now persists to artifacts/aspire/keycloak-data
   - Refresh tokens survive container restarts

2. **Signing key cache bypass for missing keys** (PrismSigningKeyCache.WarmAsync):
   - Added requiredKeyId parameter to generic OIDC overload
   - Bypass forced-refresh cooldown when requested key is missing from cache
   - Prevents stale-key 401s after OIDC provider (Keycloak) restarts with new keys
   - Updated IPrismSigningKeyCache, PrismAuthExtensions.ResolveSigningKeys, and test mocks

**Test Results:**
- All 8 localhost auth Playwright tests pass, including both restart tests
- All 27 signing key cache & auth extension unit tests pass
- Runtime restart detection (ShouldRefreshForRuntimeRestart) working correctly

**Key Insight:** OIDC provider restarts invalidate tokens in two ways:
1. Refresh tokens become invalid if sessions aren't persisted
2. Signing keys rotate, invalidating cached access tokens

Both must be handled for restart resilience.


## Learnings (2026-04-14, Open Redirect Fix — COMPLETE)

- The auth redirect boundary spans both `AccountController` and `PrismOidcConfiguration`; sanitizing only the authenticated `LocalRedirect(...)` branch is not enough because unauthenticated requests carry `AuthenticationProperties.RedirectUri` through OIDC state and the callback later issues `Response.Redirect(...)`.
- The safest contract is to normalize `returnUrl` twice with the same helper: once before creating the challenge state, and again immediately before the callback redirect sink. This preserves safe local routes while failing closed to `/` for absolute, scheme-relative, or script-style inputs.
- Minimal behavior coverage for this slice is:
  1. login/register challenge state stores `/` for hostile return URLs,
  2. safe local paths survive unchanged,
  3. authenticated users still land on local destinations only,
  4. callback redirect normalization uses the same shared rule.


## 2026-04-14: Redirect Hardening Sprint — COMPLETE

**Session:** Redirect Hardening Work (2026-04-14T12:39:42Z)

**Delivered:**
- Open-redirect mitigation: hardened login/callback returnUrl flow against open-redirect vulnerabilities
- Framework integration: replaced handwritten returnUrl parsing with ASP.NET Core `RedirectHttpResult.IsLocalUrl()` validator
- Restart resilience: Keycloak session persistence + signing key cache bypass for provider restarts
- Validation: Targeted security tests 49/49 passed; Core slice 400/400 passed

**Key Outcomes:**
- Used framework-backed local-only validation for all auth redirect paths
- Normalized returnUrl both at ingress (AccountController) and callback (PrismOidcConfiguration)
- Kept LocalRedirect for controller redirects; used IsLocalUrl() for callback contexts
- Hardened blank/null/external callback targets to default `/`
- OIDC token validation now resilient to provider restarts with key rotation

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T12:39:42Z-blathers.md`
**Session Log:** `.squad/log/2026-04-14T12:39:42Z-redirect-hardening.md`

**Team Consensus:** No compromise on security; prefer ASP.NET Core built-in validators over custom logic when feasible.

---

## Session: Phase1 Security Regression CI Test Fix (2026-04-14T17:52:43Z)

**Topic:** CI-safe loopback OIDC regression harness

**Outcome:** ✅ Fixed Phase1SecurityRegressionTests by switching loopback OIDC harness from `https://localhost` to `http://127.0.0.1` and aligning discovery HTTPS requirements with metadata URL scheme.

**Team Updates:**
- Decision merged to `.squad/decisions.md`: "CI-safe loopback OIDC regression harness"
- Tangy validated regression contract and security posture
- Session log: `.squad/log/2026-04-14T17:52:43Z-ci-test-fix.md`


## Learnings (2026-04-14, CI-safe OIDC loopback fix — COMPLETE)

- `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs` intentionally drives `PrismOidcConfiguration.OnAuthorizationCodeReceived`, so the redirect regression coverage depends on a loopback OIDC server for real token exchange, metadata discovery, nonce validation, cookie sign-in, and the final redirect sink.
- The CI failure was transport-only: GitHub Actions did not trust the Kestrel dev certificate behind `https://localhost`, so the tests never reached the redirect assertions.
- Smallest safe fix: keep the executable OIDC harness, but move the test provider to `http://127.0.0.1` because TLS is not the behavior under test in this slice.
- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` now matches `PrismSigningKeyCache`'s metadata posture by using `HttpDocumentRetriever` with `RequireHttps` derived from the metadata URL scheme, which preserves HTTPS enforcement for real HTTPS authorities while allowing HTTP loopback test doubles.
- User preference reinforced: prefer the smallest CI-safe change that preserves regression coverage, and avoid broader production refactors when a focused harness adjustment plus narrowly coupled support code is enough.
