# Tangy — History

## Core Context

This agent leads QA validation, test coverage analysis, and edge-case identification. File reflects extensive testing and validation work. Complete history in git; Recent Sessions below.

**Key domains:** Playwright testing, E2E validation, Edge case coverage, CI/CD readiness, Performance/load testing

## Session: Aspire localhost auth CI job QA (2026-04-14T18:06:05Z)

**Topic:** Add separate GitHub Actions job for the Aspire-backed localhost auth/session Playwright lane

**Outcome:** ✅ CI-readiness QA verdict: GREEN

### Review Scope

Evaluated Blathers' `localhost-auth-playwright` job spec for:
- CI environment prerequisites (Node, .NET, Docker, Playwright)
- HTTPS certificate trust strategy
- Aspire lifecycle automation
- Port collision and resource constraints
- Regression contract alignment with existing tests

### Key QA Findings

**✅ Passed**
- Certificate trust strategy sound
- Aspire prerequisites properly validated
- Browser automation dependencies correctly sequenced
- Existing test contracts executable end-to-end (8/8 passing locally)
- Port usage isolated to localhost auth lane

**⚠️ Notes**
- Expected wall-time impact: +3–5 min per PR
- Monitor ubuntu-latest runner certificate trust
- Consider conditional triggers on auth-path changes for future gating

### Performance Expectations

- AppHost startup: ~30–60 seconds
- Keycloak container init: ~20–30 seconds
- Playwright browser install: ~40–60 seconds (cached)
- Test execution: ~2–3 minutes
- **Total wall time: 5–8 minutes per PR**

### Recommendation

**APPROVE for merge.** Monitor first few runs for certificate trust issues on ubuntu-latest; if present, add explicit certificate PIN or test-only certificate bundle strategy.

**Decision Merged:** `.squad/decisions.md` — "2026-04-14: Blathers & Tangy — Aspire localhost auth CI job"

---

## 📋 Recent Sessions

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



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

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

## Learnings — 2026-04-14 — CI loopback OIDC certificate trust

- The `CI Tests` workflow run `24413418473` failed only in `core-tests`; `storybook-tests` stayed green.
- All 10 failures came from `Phase1SecurityRegressionTests` redirect-round-trip cases that start `LoopbackOidcProvider` on `https://localhost:{port}` and then hit it through `new HttpClient()`.
- GitHub Actions failed during token exchange in `PrismOidcConfiguration` with `HttpRequestException` → `AuthenticationException` → `UntrustedRoot`, which points to certificate trust rather than redirect logic.
- Local reproduction with `dotnet test src/UmbracoPrism.Core.Tests/UmbracoPrism.Core.Tests.csproj -c Release --filter FullyQualifiedName~Phase1SecurityRegressionTests` passed 23/23 on a machine that already has a trusted .NET HTTPS dev certificate.
- For this repo, any test harness that spins up loopback HTTPS with Kestrel must either establish certificate trust in CI or use an explicit test-only certificate/handler strategy; otherwise security-contract tests can fail before the behavior under test runs.

## Learnings — 2026-04-14 — Phase1 redirect contract review

- The failing `Phase1SecurityRegressionTests` redirect-round-trip cases are asserting the right user-facing contract at the callback boundary: hostile `returnUrl` values fall back to `/`, safe local paths survive sign-in, and missing or blank values canonicalize to `/`.
- The unnecessary part of the current harness is not the callback execution itself; it is the dependency on a Kestrel `https://localhost` dev certificate being trusted by CI while `PrismOidcConfiguration` uses bare `HttpClient` instances for token exchange and discovery.
- Any Blathers fix should preserve execution of `PrismOidcConfiguration.OnAuthorizationCodeReceived` and the final `Response.Redirect(...)` assertion; replacing the transport trust strategy with an explicit test-only handler/certificate is fine, but collapsing coverage back to controller-only or `PrismReturnUrl.Normalize(...)` tests would lose the callback-sink regression.
- Concrete QA gap: `RecordingAuthenticationService` only records the sign-in scheme today, so the suite does not yet assert that `AuthenticationProperties.RedirectUri` is cleared before the `PrismMemberCookie` session is persisted.
- Key review paths: `.github/workflows/ci-tests.yml`, `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs`, `src/UmbracoPrism.Core/Auth/PrismReturnUrl.cs`, and `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`.

## 2026-04-14: Redirect Hardening Sprint — COMPLETE

**Session:** Redirect Hardening Work (2026-04-14T12:39:42Z)

**Delivered:**
- Rewrote stale Phase1 regression tests into behavior-based security contracts
- Converted legacy test patterns to modern contract-driven testing
- Comprehensive Phase1 regression test audit and remediation guidance
- Validation: Phase1 tests passed; full Core suite passed; Playwright end-to-end green

**Key Outcomes:**
- Security tests now assert runtime behavior with executable harnesses
- External destinations blocked; safe local destinations round-trip verified
- Missing state falls back safely; production debug output renders nothing
- Avoid source inspection helpers and inert expressions for security regressions

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T12:39:42Z-tangy.md`
**Session Log:** `.squad/log/2026-04-14T12:39:42Z-redirect-hardening.md`

**Team Consensus:** Security contracts must be behavior-driven with executable test harnesses.


## 2026-04-14: Release v1.8.0 — Pre-Deployment Validation

**Session:** Release orchestration (v1.7.1 → v1.8.0)

### Work Performed

1. **Solution Build Verification** — `dotnet build UmbracoPrism.sln` passed; no errors, no new warnings
2. **File Consistency Check** — Verified version sync across all 5 release files (CHANGELOG, .csproj, package.json×2, marketplace.json)
3. **Integration Validation** — Frontend artifacts generated, backend NuGet metadata correct, marketplace synchronized
4. **Readiness Assessment** — All pre-deployment checks passed; release ready for git tag creation

### Key Verifications

- ✅ Solution builds cleanly (dotnet build UmbracoPrism.sln)
- ✅ Version consistency: 1.8.0 across CHANGELOG, .csproj, package.json (root + client), marketplace.json
- ✅ No orphaned version references
- ✅ package-lock.json regenerated cleanly
- ✅ No build artifacts require regeneration before deployment
- ✅ Release date correctly set (2026-04-14)

### Outputs

- Orchestration log: `.squad/orchestration-log/2026-04-14T16:55:12Z-tangy.md`

### Pattern for Future Validation

Pre-deployment validation should:
1. Build the entire solution to catch any compile errors
2. Verify version strings match across all deployment surfaces
3. Confirm no orphaned references to old versions
4. Validate marketplace/CDN metadata in sync with package versions
5. Generate clean build artifacts with no warnings introduced

---

## Session: Phase1 Security Regression CI Test Fix (2026-04-14T17:52:43Z)

**Topic:** Validation of loopback OIDC regression harness change

**Outcome:** ✅ Regression contract validated; confirmed loopback dependency necessary for real OIDC callback execution; validated self-signed TLS removal does not weaken security assertions.

**Team Updates:**
- Decision merged to `.squad/decisions.md`: "CI-safe loopback OIDC regression harness"
- Blathers completed fix: loopback moved to HTTP/127.0.0.1, HTTPS requirement derived from metadata scheme
- Session log: `.squad/log/2026-04-14T17:52:43Z-ci-test-fix.md`

## Learnings — 2026-04-14 — Aspire localhost auth CI lane review

- The live localhost auth/session suite is already behaviorally green on a real machine: `npm run test:playwright:localhost-auth` passed 8/8 in about 168 seconds, after owning AppHost startup, two full-stack restarts, and shutdown.
- Current CI baseline is much lighter: GitHub Actions `CI Tests` run `24414635647` finished `core-tests` in about 47 seconds and `storybook-tests` in about 1 minute 54 seconds, so a new Aspire-backed lane would materially increase PR wall time once Node install, Playwright browser install, Docker cold start, and image pulls are included.
- The suite depends on fixed localhost HTTPS ports and the Aspire-owned lifecycle in `src/UmbracoPrism.Client/tests/support/live-app-host.ts` (`17214`, `15135`, `21233`, `22194`, `44345`, `8443`, `7245`); that is appropriate for an isolated CI runner but too broad to piggyback on every generic client-only path trigger without more selective gating.
- The biggest CI readiness risk is certificate trust, not browser automation: AppHost intentionally serves TestSite, Keycloak proxy, and MockBusinessApp on HTTPS localhost, while `PrismOidcConfiguration` still performs token exchange and OIDC discovery with bare `HttpClient`/`HttpDocumentRetriever` against `https://localhost:8443`, which can fail on runners that do not trust the .NET dev certificate.
- Key review paths for this lane are `.github/workflows/ci-tests.yml`, `src/UmbracoPrism.Client/package.json`, `src/UmbracoPrism.Client/playwright.localhost-auth.config.ts`, `src/UmbracoPrism.Client/tests/support/live-app-host.ts`, `scripts/validate-aspire-prereqs.mjs`, `src/UmbracoPrism.AppHost/Program.cs`, and `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`.

## Learnings — 2026-04-14 — Localhost auth Playwright CI bootstrap failure

- GitHub Actions run `24415783660` failed in job `localhost-auth-playwright` after about 70 seconds, but the first meaningful failure happened before any Aspire startup or Playwright test execution.
- The failing step was `.github/workflows/ci-tests.yml` step **Trust .NET development certificate**, not `Validate Aspire prerequisites` or `npm run test:playwright:localhost-auth`.
- Runner log excerpt: `dotnet dev-certs https --trust` reported `[110] For OpenSSL trust to take effect, '$HOME/.aspnet/dev-certs/trust' must be listed in the SSL_CERT_DIR environment variable` and then exited with code `4`.
- That makes this a **workflow/bootstrap certificate-trust failure on Ubuntu GitHub Actions**, not a test-behavior regression, not Docker/Aspire startup, and not a browser automation problem.
- The concrete next action is to teach the workflow's certificate-trust step the Linux trust-store wiring (for example by exporting `SSL_CERT_DIR` to include `$HOME/.aspnet/dev-certs/trust` before `dotnet dev-certs https --trust`) or use an equivalent explicit test-only trust strategy, then rerun the lane to reach real AppHost/test behavior.


## Team Update — 2026-04-14T19:12:55Z — Auth Failure Investigation Complete

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T19:12:55Z-tangy.md`

**Session Log:** `.squad/log/2026-04-14T19:12:55Z-auth-failure-investigation.md`

**Outcome:** Scribe merged Tangy and Blathers decisions into `.squad/decisions.md` under **2026-04-14: Tangy & Blathers — GitHub Actions localhost-auth-playwright Bootstrap Failure Classification**.

**Decision Finalized:** GitHub Actions run `24415783660` is a **workflow bootstrap / Linux certificate trust setup failure**. The next fix is in `.github/workflows/ci-tests.yml`: export/persist `SSL_CERT_DIR` on Ubuntu runners to include `$HOME/.aspnet/dev-certs/trust` before running `dotnet dev-certs https --trust`.

**Inbox Files:** Deleted after merge (deduplication confirmed).

---

## Learnings — 2026-04-14 — Reviewed localhost auth workflow fix

- The current workflow fix wires Linux trust in the right place: it persists `SSL_CERT_DIR` before the trust command, so the following `dotnet dev-certs https --trust` step can see `$HOME/.aspnet/dev-certs/trust` and should avoid the exact Ubuntu exit-4 failure we previously classified.
- Keeping `/etc/ssl/certs` and `/usr/lib/ssl/certs` in that persisted value preserves normal system CA lookup while adding the ASP.NET dev-certs trust directory for the localhost auth lane.
- Adding top-level `workflow_dispatch:` is a safe manual repro hook here because it leaves the existing path-gated `pull_request` trigger and `push`-to-`main` trigger untouched; only human-invoked runs gain an extra entry point.

## Team Update — 2026-04-14T19:52:39Z — CI workflow patch review finalized

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T19:52:39Z-tangy.md`

**Session Log:** `.squad/log/2026-04-14T19:52:39Z-auth-workflow-fix.md`

**Outcome:** Scribe finalized workflow patch orchestration, merged Tangy and Blathers decisions into `.squad/decisions.md`, and updated team histories.

**QA Verdict:** ✅ **APPROVED** — Patch is safe and production-ready.
- SSL_CERT_DIR wiring correctly places trust directory before system paths.
- System CA paths preserved for normal lookup.
- workflow_dispatch safe because it doesn't affect existing pull_request/push triggers.
- Environment variable persistence correct via GITHUB_ENV.

**Risk Assessment:** No regressions detected. Expected wall-time impact: +3–5 min per PR.

**Status:** Ready for merge.
