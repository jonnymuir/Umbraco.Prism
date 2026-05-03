# Tangy — History

## Core Context

QA validation, test coverage analysis, and edge-case identification.

**Key domains:** Playwright testing, E2E validation, Edge case coverage, CI/CD readiness, Performance analysis

## 📋 Recent Sessions

---

## 2026-05-03: Spawn Manifest — Codespaces Dashboard Failure Reproduction

**Timestamp:** 2026-05-03T11:07:19.866Z  
**Status:** ✅ Reproduced

Tangy reproduced the live Codespaces dashboard failure at `https://organic-space-fortnight-77g9wvq6jxhxg97-44345.app.github.dev/dashboard`.

**Evidence Captured:**
- Found hardcoded `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` in code
- This may not resolve correctly in live Codespaces environment
- Hypothesis: backend service discovery issue or network isolation

**Coordination:**
- Blathers deployed enhanced diagnostics (token kid, ASPNETCORE_ENVIRONMENT, JWKS URLs)
- Copper verified trust chain and recommended restart of MockBusinessApp
- Brewster fixed Codespaces URL printing regression

**Next Steps:**
- Monitor operator actions when next 401 occurs in live Codespaces
- Use enhanced /debug/auth endpoint to confirm backchannel state
- Leverage Codespaces recovery scripts if needed


---

## 2026-05-03: Codespaces Port Forwarding Mismatch

**Timestamp:** 2026-05-03T12:28:26.122+01:00  
**Status:** ✅ Reproduced & Root-caused

### Problem

User reported that the startup status page (port 3000) shows "everything is ready" after health-check, but opening the TestSite forwarded URL (port 44345) results in a download/blank-file experience.

### Investigation

Used playwright-cli to reproduce live behavior at both URLs:

**Port 44345 (TestSite):**
- HTTP 404 from `tunnels-prod-rel-uks1-v3-cluster`
- `content-length: 0` (empty body, not an app error page)
- `x-served-by: tunnels-prod-rel-uks1-v3-cluster`
- Playwright error: `net::ERR_HTTP_RESPONSE_CODE_FAILURE`

**Port 3000 (Status Page):**
- HTTP 401 with `www-authenticate: tunnel`
- Redirects to GitHub login for Codespaces auth
- Port is private and requires authentication

### Root Cause

**Port 44345 is not forwarded/visible in the Codespace.**

The health-check script tests `https://localhost:44345` **internally** (inside the Codespace), which succeeds because the app is listening. However, the Codespaces tunnel infrastructure does NOT expose port 44345 publicly, resulting in:

- Internal check: ✅ `curl https://localhost:44345` succeeds
- External URL: ❌ HTTP 404 from tunnel layer

This creates a **false positive** — the status page reports "ready" based on internal checks while the public URL is inaccessible.

### Learnings

1. **Localhost checks ≠ tunnel accessibility.** Health checks must verify the actual forwarded URL, not just localhost.
2. **HTTP 404 with content-length: 0 from `tunnels-prod-*` clusters indicates port not forwarded**, not an application error.
3. **Port visibility must be explicit.** Codespaces requires `.devcontainer/devcontainer.json` port declarations or manual visibility changes via UI/CLI.

### Evidence

Full network capture and analysis: `codespaces-port-mismatch-evidence.md`

### Decision Recorded

`📌 2026-05-03: Tangy — Health Checks Must Verify Tunnel Accessibility` (decisions.md, PROPOSED)

Recommendation: Enhanced health checks should verify both internal (localhost) and external (forwarded URL) surfaces. Uses `gh codespace ports` to discover and test forwarded URLs and visibility. Complements Brewster's pre-forwarding fix.


---

**2026-05-03T11:58:20Z:** Dispatched as Tangy-8 — Reproduce dashboard and debug evidence path (agent: Tangy). Concluded: localhost:5163 is expected internal hop; minimal UX-only improvement made to messaging for operators (indicate localhost is internal, prompt refresh + /debug/auth if 401 persists); targeted dashboard tests and full core tests passed; no regressions.

---

## Learnings

### HTTP 401 + www-authenticate: tunnel = Codespaces Port Visibility Issue

**Observed:** Dashboard URL returns HTTP 401 with `www-authenticate: tunnel` header on forwarded Codespaces URL.

**Analysis:**
- `www-authenticate: tunnel` is Codespaces infrastructure auth protocol (requires GitHub login)
- Indicates port 15135 is not publicly visible to the Codespaces tunnel proxy layer
- Application-level security (`DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`) is correct
- Issue is Codespaces-infrastructure-level port visibility, not app misconfiguration

**Contract:** Dashboard URL must:
1. Not return HTTP 401 with `www-authenticate` on the forwarded public URL
2. Not redirect to GitHub login
3. Serve dashboard immediately (telemetry + logs visible)
4. Respond HTTP 200 or compatible success code

**Fix verification:** Ensure port 15135 is explicitly public in `.devcontainer/devcontainer.json` ports declaration or manually toggled visible in VS Code Ports panel.


## 2026-05-03 — Codespaces Dashboard Port Visibility Validation (Background)

**Task:** Validate HTTP 401 + `www-authenticate: tunnel` diagnosis; define dashboard contract acceptance criteria.

**Findings:**
- Confirmed 401 is GitHub Codespaces tunnel layer authentication, not app configuration issue
- Identified health check false positive: localhost checks pass but tunnel forwarding inaccessible
- Drafted acceptance criteria for dashboard forwarded URL (HTTP 200, no auth header, immediate render)

**Decision Record:** `.squad/decisions.md` entries 2026-05-03 (Tangy)

**Files Created:**
- `.squad/orchestration-log/2026-05-03T13-59-46Z-tangy.md` — orchestration report

---

## 2026-05-03 — Codespaces Dashboard Port 17214 Contract Validation

**Timestamp:** 2026-05-03T15:12:55.439+01:00  
**Status:** ✅ Complete

### Task

Add automated validation for the Codespaces dashboard contract: users must be directed to the forwarded HTTPS Aspire dashboard endpoint on port 17214, not the redirecting HTTP endpoint on port 15135.

### Outcome

- **Blathers had already fixed the code** — both `on-start.sh` and `server.js` now correctly advertise port 17214 for Codespaces users
- Added two new tests to `DashboardLocalEndpointsValidationTests.cs`:
  1. `CodespacesStartupScript_AdvertisesHttpsPort17214_NotHttpPort15135` — validates on-start.sh uses port 17214
  2. `StatusServer_UsesPort17214ForCodespacesPublicUrl` — validates server.js uses port 17214
- Both tests pass, confirming the contract is met
- Ran full test suite for `DashboardLocalEndpointsValidationTests`: all 25 tests pass

### Contract Enforced

✅ **Codespaces users are directed to port 17214 (HTTPS Aspire dashboard)**  
❌ Port 15135 (HTTP redirect endpoint) is no longer advertised

### Learnings

**Test-as-contract pattern:** When a critical operational contract emerges (like port forwarding behavior), codify it as a test immediately. This prevents regression and documents the requirement for future contributors.

## 2026-05-03 — Scribe: Dashboard Port Contract Decision Merged

Scribe merged tangy-dashboard-17214-contract.md decision documenting test coverage and port 17214 contract for Codespaces.

---

## 2026-05-03 — Login Callback Port Mismatch After Codespace Restart

**Timestamp:** 2026-05-03T17:57:10.282+01:00  
**Status:** 🔍 Root-caused (no code changes yet)

### Problem

User reports that after restarting Codespaces, TestSite login redirects Safari to `https://localhost:9250/signin-oidc?...` instead of the public Codespaces URL. Port 9250 is the internal HTTP listener and unreachable from the browser.

### Behavioral Contract Violated

**Contract**: After Codespace restart, the TestSite login flow must redirect to the public Codespaces forwarded URL for `/signin-oidc`, not an internal localhost URL.

**Expected**: `https://v7ldkc4c-44345.uks1.app.github.dev/signin-oidc?...`  
**Actual**: `https://localhost:9250/signin-oidc?...` (unreachable)

### Root Cause

The OIDC middleware generates `redirect_uri` based on the incoming request's `Host` header. The TestSite has a Host override middleware (lines 44-54 in `Program.cs`) that replaces `Request.Host` with the public Codespaces hostname when `TESTSITE_PUBLIC_URL` is set.

**The issue**: If `TESTSITE_PUBLIC_URL` is missing or stale after restart, the override doesn't activate. The OIDC handler then sees the internal host (`localhost:9250` HTTP or `localhost:44345`) instead of the public Codespaces URL, generating an unreachable redirect_uri.

**Why it's flaky**: The behavior is deterministic but *feels* flaky because:

1. **Timing dependency**: AppHost must start before TestSite and set `TESTSITE_PUBLIC_URL` via the environment. On cold starts this works, but on Codespace resume, service startup order may differ or environment propagation may lag.

2. **HTTP vs HTTPS port confusion**: TestSite listens on both `https://localhost:44345` and `http://localhost:9250`. If the Host override doesn't activate, the redirect_uri may use port 9250, which is never forwarded in Codespaces.

3. **Silent failure mode**: There's no error logged when `TESTSITE_PUBLIC_URL` is missing — the middleware just doesn't override the Host, and everything *seems* fine until the user clicks "Sign in" and gets an unreachable localhost URL.

### Regression Gap

**Existing tests**: `LocalhostGenericOidcRegressionTests.cs` validates scope and token behavior, but doesn't test:

1. That the `redirect_uri` uses the public host when `TESTSITE_PUBLIC_URL` is set
2. That the Host override middleware works correctly for HTTPS requests
3. That the behavior gracefully degrades or fails loudly when `TESTSITE_PUBLIC_URL` is missing in a Codespaces environment

The tests are unit tests that mock OIDC configuration but don't exercise the actual Host header override middleware or the restart scenario.

### Minimal Reproduction

To prove the suspected root cause:

1. **Restart Codespace** and wait for AppHost to start
2. **Before clicking "Sign in"**, check TestSite logs or add a probe endpoint to dump:
   - `TESTSITE_PUBLIC_URL` env var
   - `Request.Host` for an inbound HTTPS request
   - The `redirect_uri` generated by the OIDC middleware
3. **If port 9250 appears in the redirect_uri**, the Host override didn't activate

**Better approach**: Add logging to the Host override middleware in `Program.cs` to surface immediately if the env var is missing after restart.

### Proposed Test Coverage

Add a new test class `CodespacesOidcRedirectUriTests.cs` with:

1. Host override middleware sets `Request.Host` to public hostname when `TESTSITE_PUBLIC_URL` is set
2. OIDC redirect_uri uses the public Codespaces URL, not localhost:9250
3. System logs a warning if `CODESPACE_NAME` is set but `TESTSITE_PUBLIC_URL` is missing (fail-fast contract)

### Recommendation

**Option 1 (fail-fast)**: Make `TESTSITE_PUBLIC_URL` required in Codespaces — fail fast with a clear error message if missing  
**Option 2 (fallback)**: Derive the public URL from inbound `X-Forwarded-*` headers if `TESTSITE_PUBLIC_URL` is missing  
**Option 3 (test only)**: Add test coverage without changing runtime behavior

Option 1 (fail-fast) is the most honest and debuggable approach.

### Decision Recorded

`.squad/decisions/inbox/tangy-login-callback-flake.md` — full analysis and recommendations

### Learnings

**Restart-dependent failures feel flaky even when deterministic** because the preconditions change between cold start and resume. The user experience is: "it worked before, now it doesn't, I didn't change anything." From a test-contract perspective, this is a **silent partial boot** — the service starts successfully but is missing critical runtime configuration, leading to delayed failure during user interaction.

**Test gap pattern**: Unit tests validated token/scope logic but missed the **environmental contract** — the dependency on `TESTSITE_PUBLIC_URL` being set by AppHost before TestSite starts. Integration tests or contract tests that validate environment variable propagation would have caught this.

## 2026-05-03: Codespaces Login Callback Flake Diagnosis

**Outcome**: Identified missing/stale `TESTSITE_PUBLIC_URL` as root cause of restart-sensitive contract gap.

**Scope**: Diagnosed why TestSite login redirects to `localhost:9250` instead of public Codespaces URL after restart.

**Key Finding**: Host override middleware in Program.cs (lines 44-54) relies on `TESTSITE_PUBLIC_URL` environment variable. When missing or stale after restart, OIDC handler generates unreachable redirect_uri using internal port 9250.

**Decision**: Merged to decisions.md as "2026-05-03: Tangy — Codespaces Login Callback Port Mismatch". Proposed fail-fast approach: require `TESTSITE_PUBLIC_URL` in Codespaces, log error immediately if missing.

---

