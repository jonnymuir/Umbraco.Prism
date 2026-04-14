# Blathers — History

## Core Context

This agent manages backend services, authentication infrastructure, and CI/CD workflows. File has grown to reflect extensive project history. Complete work context in git history and Recent Sessions below.

**Key domains:** Auth/OIDC, Aspire local dev, CI infrastructure, Database services, Security hardening

## 📋 Recent Sessions

---

## Session: Aspire localhost auth CI job (2026-04-14T18:06:05Z)

**Topic:** Add separate GitHub Actions job for the Aspire-backed localhost auth/session Playwright lane

**Outcome:** ✅ Added `localhost-auth-playwright` job to `.github/workflows/ci-tests.yml`

### Delivered

- Implemented dedicated `localhost-auth-playwright` job alongside `storybook-tests` and `core-tests`
- Configured Node 22.17.1 and .NET 10 runtime setup
- Integrated Playwright Chromium browser install with system dependencies
- Added HTTPS dev certificate generation and trust sequence
- Implemented Aspire prerequisite validation (`validate-aspire-prereqs.mjs`)
- Widened workflow path filters to include Aspire-backed auth graph

### Validation

- ✅ Local `npm run test:playwright:localhost-auth` suite passed **8/8**
- ✅ AppHost lifecycle working (startup, two restarts, shutdown)
- ✅ Bearer token refresh across restarts validated

### Key Insights

- The localhost auth lane is heavier than Storybook/core because it owns the full AppHost lifecycle, Docker Keycloak, browser automation, and whole-stack restart
- Isolated job design allows the lane to fail independently without affecting core/storybook paths
- Path filters must include the entire auth stack (AppHost, TestSite, MockBusinessApp, KeycloakProxy, Shared, keycloak/, scripts)
- Tangy validated CI-readiness as GREEN with ~5–8 min expected wall-time impact per PR

### Next Steps

- Merge job spec to main
- Monitor first CI run for ubuntu-latest certificate trust
- Document wall-time expectations in team decisions for future auth-path changes

**Decision Merged:** `.squad/decisions.md` — "2026-04-14: Blathers & Tangy — Aspire localhost auth CI job"

---

## 📋 Recent History
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

## Learnings (2026-04-14, Aspire localhost auth CI lane — COMPLETE)

- The real localhost auth/session regression lane belongs beside the existing CI jobs in `.github/workflows/ci-tests.yml` as its own job, not folded into Storybook or core tests, so the heavy Aspire/Docker/browser path can fail independently without disturbing the existing slices.
- For this repo, the workflow path filters must include the whole Aspire-backed auth graph — `src/UmbracoPrism.AppHost/`, `src/UmbracoPrism.TestSite/`, `src/UmbracoPrism.MockBusinessApp/`, `src/UmbracoPrism.KeycloakProxy/`, `src/UmbracoPrism.Shared/`, `keycloak/`, and `scripts/validate-aspire-prereqs.mjs` — or CI will miss real auth-lane changes outside the client/core projects.
- The smallest credible GitHub Actions bootstrap on Ubuntu is: `actions/setup-node` for Node `22.17.1`, `actions/setup-dotnet` for `.NET 10`, `npm ci`, `npx playwright install --with-deps chromium`, `dotnet dev-certs https` plus `dotnet dev-certs https --trust`, then the existing repo guardrails `node ../../scripts/validate-aspire-prereqs.mjs --localhost-auth-suite` and `npm run test:playwright:localhost-auth`.
- `src/UmbracoPrism.Client/package.json` already contains the right executable contract for the lane; the CI job should call that script instead of re-encoding AppHost lifecycle logic in YAML.
- Local validation matters for this slice: on 2026-04-14 the full `npm run test:playwright:localhost-auth` suite passed `8/8`, confirming the real Aspire-backed lane is runnable end-to-end before wiring GitHub Actions to it.
- User preference reinforced: preserve the existing Storybook and core test jobs, add the smallest separate auth job that starts the real lane, and avoid unrelated CI refactors.

## Learnings (2026-04-14, localhost-auth-playwright failure investigation)

- GitHub Actions run `24415783660` failed in workflow setup, not in Aspire startup or Playwright execution: `localhost-auth-playwright` never reached the prereq script or the suite because the `Trust .NET development certificate` step exited `4`.
- On GitHub-hosted Ubuntu, `dotnet dev-certs https --trust` is not self-sufficient for this lane; the runner log explicitly requires `$HOME/.aspnet/dev-certs/trust` to be included in `SSL_CERT_DIR` for OpenSSL-based trust to take effect.
- The smallest next fix is workflow-only: keep the existing Node/.NET/browser/path-filter/working-directory setup, but export `SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"` (persisted for later steps) before running `dotnet dev-certs https --trust`, then rerun the job to see whether Aspire/Docker/app behavior has any remaining issues.
- Evidence against the other suspected buckets in this run: Playwright Chromium + Linux deps installed successfully, the workflow paths already cover the full auth graph, `../../scripts/validate-aspire-prereqs.mjs` resolves correctly from `src/UmbracoPrism.Client`, and no Docker/Aspire logs exist yet because the job stopped before those steps.

## Learnings (2026-04-14, CI workflow manual auth rerun — COMPLETE)

- The smallest safe GitHub Actions fix for the Ubuntu localhost-auth lane is workflow-only: persist `SSL_CERT_DIR` to `$GITHUB_ENV` before `dotnet dev-certs https --trust`, keeping the runner's dev-cert trust directory alongside the system cert directories for later .NET/OpenSSL consumers in the job.
- Adding top-level `workflow_dispatch:` to `.github/workflows/ci-tests.yml` makes the existing `localhost-auth-playwright` job manually runnable from both the GitHub UI and `gh workflow run`, without changing the existing push/pull-request job topology.
- For this repo, manual rerun support belongs at the workflow trigger layer, not by duplicating or renaming the localhost auth job; preserving the existing job name keeps prior diagnostics, history, and references stable.


## Team Update — 2026-04-14T19:12:55Z — Auth Failure Investigation Complete

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T19:12:55Z-blathers.md`

**Session Log:** `.squad/log/2026-04-14T19:12:55Z-auth-failure-investigation.md`

**Outcome:** Scribe merged Tangy and Blathers decisions into `.squad/decisions.md` under **2026-04-14: Tangy & Blathers — GitHub Actions localhost-auth-playwright Bootstrap Failure Classification**.

**Decision Finalized:** GitHub Actions run `24415783660` (localhost-auth-playwright) is a **workflow bootstrap failure**. Workflow and job structure are sound; only the certificate bootstrap for Linux runners needs `SSL_CERT_DIR` wiring before `dotnet dev-certs https --trust`.

**Smallest Correct Fix:** Update `.github/workflows/ci-tests.yml` to export/persist `SSL_CERT_DIR` on Ubuntu, including `$HOME/.aspnet/dev-certs/trust` and system directories, then rerun lane.

**Inbox Files:** Deleted after merge (deduplication confirmed).

---

## Team Update — 2026-04-14T19:52:39Z — CI workflow patch finalized

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T19:52:39Z-blathers.md`

**Session Log:** `.squad/log/2026-04-14T19:52:39Z-auth-workflow-fix.md`

**Outcome:** Scribe finalized workflow patch orchestration, merged Blathers decision into `.squad/decisions.md`, and updated team histories.

**Patch Summary:** `.github/workflows/ci-tests.yml` now includes:
- Top-level `workflow_dispatch:` trigger for manual GitHub UI and `gh` CLI reruns.
- `SSL_CERT_DIR` wired on Ubuntu runners: `$HOME/.aspnet/dev-certs/trust:/etc/ssl/certs:/usr/lib/ssl/certs` before `dotnet dev-certs https --trust`.
- Existing `pull_request` and `push` triggers and job topology unchanged.

**QA Verdict:** Tangy approved patch as production-ready.

**Status:** Ready for merge.
