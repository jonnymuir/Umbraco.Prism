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
