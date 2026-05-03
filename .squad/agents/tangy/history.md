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

