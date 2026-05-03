# Blathers — History (Summary)

**Agent:** Backend specialist shipping Codespaces URL derivation fixes, backchannel rewrites for JWKS/token-refresh, and security analysis for auth isolation.

**Recent focus:** Aspire dashboard Codespaces access, authentication diagnostics, runtime stale-code diagnosis, backchannel OIDC validation.

---

## 2026-05-03: Codespaces Dashboard Port 17214 Fix Implementation

**Status:** ✅ Complete

**Changes Made:**
Updated all Codespaces-facing startup output, status surfaces, and helper scripts to use the HTTPS forwarded endpoint on port 17214 instead of the HTTP endpoint on 15135.

**Files Updated:**
- `.devcontainer/on-start.sh` — Changed DASHBOARD_URL from `http://localhost:15135` to `https://localhost:17214` in Codespaces
- `.devcontainer/devcontainer.json` — Swapped port 15135 to 17214 in forwardPorts array, updated port labels
- `scripts/startup-status/server.js` — Changed ASPIRE_CODESPACES_PORT from 15135 to 17214
- `scripts/codespaces/health-check.sh` — Unified dashboard URL to `https://localhost:17214` for both environments
- `CODESPACES.md` — Updated documentation to reflect port 17214 as primary dashboard port
- `scripts/codespaces/stop.sh` — Updated freed ports message
- Test files — Updated port references in server.test.js, live-app-host.ts, validate-aspire-prereqs.mjs

**Test Results:**
- JavaScript tests: 24/24 passed
- C# DashboardLocalEndpointsValidationTests: 23/23 passed

**Key Insight:**
The HTTP endpoint on 15135 redirects to an ephemeral HTTPS port (not the advertised 17214), making it unsuitable for browser access in Codespaces. The HTTPS forwarded endpoint on 17214 is the correct entry point for both Codespaces and local development.

---

## 2026-05-03: Aspire Dashboard HTTP→HTTPS Redirect to Ephemeral Port

**Status:** ✅ Diagnosis complete (no code changes required)

**Root Cause Identified:**
Aspire dashboard redirects HTTP port 15135 to internal ephemeral HTTPS port 41981, not the forwarded port 17214.

**Recommendation:**
Use HTTPS port 17214 directly in Codespaces. Update `.devcontainer/devcontainer.json`, `on-start.sh`, and `CODESPACES.md` to reference port 17214 as primary dashboard URL.

**Key Learning:**
- Aspire dashboard HTTP→HTTPS redirect uses Kestrel's internal HTTPS bind address, not advertised forwarded port
- When both HTTP and HTTPS ports are forwarded, HTTP becomes a redirect trap
- For Codespaces scenarios, prefer HTTPS forwarded port as primary dashboard URL

---

## 2026-05-03: Team Spawn — Aspire Dashboard Codespaces 401 Redirect

**Status:** ✅ Investigation complete; operator action pending

**Finding:** Interpreted Codespaces runtime evidence and concluded dashboard HTTP endpoint on 15135 redirects to ephemeral HTTPS port 41981.

**Recommendation:** Try dashboard on forwarded HTTPS port 17214 first, since AppHost advertises that endpoint and 15135 is only HTTP-redirect side.

---

## Earlier Sessions

Full history archived to `history-archive.md` (prior to 2026-05-03).

## Learnings

### 2026-05-03: MockBusinessApp 401 — Codespaces Port Mismatch

Diagnosed Codespaces downstream API 401 where MockBusinessApp couldn't validate bearer tokens from the browser-facing TestSite session. Root cause: `KEYCLOAK_BACKCHANNEL_URL` environment variable was set to hardcoded `http://localhost:8080` (expected static port), but Aspire generates **ephemeral localhost ports** that change between runs (e.g., `http://localhost:57123`). The backchannel OIDC metadata fetch tried to reach `localhost:8080` but Keycloak was listening on a different port, causing "connection refused" and falling back to the public Codespaces URL (which is blocked by GitHub's port-forwarding proxy for unauthenticated server requests).

**Key insights:**
- Aspire's `.GetEndpoint("http")` returns the **full runtime URL** including ephemeral ports — not a static port like :8080
- AppHost already sets `KEYCLOAK_BACKCHANNEL_URL` correctly at line 145 using `keycloak.GetEndpoint("http")`
- The issue arises when shell profiles, `.env` files, or launch configs override Aspire's environment with a hardcoded value
- The malformed error URL (`http://codespace-url:8080/...`) is a red herring from exception formatting, not the actual fetch attempt

**Action:** Remove any hardcoded `KEYCLOAK_BACKCHANNEL_URL=http://localhost:8080` from environment configs. Let Aspire set it dynamically.

**Skill match:** This aligns with "keycloak-localhost-https" and "backchannel-rewrite-testing" patterns — backchannel rewrites must not weaken issuer validation, and transport-layer URL derivation must respect runtime discovery (Aspire endpoints, not hardcoded ports).

**Decision artifact:** `.squad/decisions/inbox/blathers-mockbiz-401-diagnosis.md` — full technical analysis and recommended fix strategy.

### 2026-05-03: Codespaces Dashboard and Auth Fixes — Commit Separation

**Branch:** `squad/codespaces-dashboard-and-auth-fixes`

Separated two distinct Codespaces fixes into clean, release-note-friendly commits:

**Commit 1: Dashboard port 17214 fix** (`fa7881c`)
- Changed all Codespaces dashboard references from HTTP port 15135 to HTTPS port 17214
- Updated `.devcontainer`, scripts, tests, and documentation
- Root cause: HTTP endpoint redirects to ephemeral HTTPS port, not the advertised 17214
- Test results: 24/24 JS tests + 23/23 C# DashboardLocalEndpointsValidationTests passed

**Commit 2: MockBusinessApp JWKS fetch via backchannel** (`455e0d5`)
- Set `KEYCLOAK_BACKCHANNEL_URL` env var for MockBusinessApp in AppHost (line 148)
- Uses ephemeral port allocation for Keycloak (`port: null` on line 65)
- Enables MockBusinessApp to fetch signing keys via internal HTTP endpoint, bypassing GitHub Codespaces proxy
- Backchannel rewrite is dual-gated (env var + Development) — issuer validation unchanged

**Key learning:**
When implementing multi-concern fixes, separate commits by user-facing issue for clean release notes. The dashboard fix addresses "port 17214 doesn't work" and the auth fix addresses "MockBusinessApp returns 401 in Codespaces". Mixing them would obscure which commit fixed which symptom.

**Build/test status:** Solution builds clean (5 pre-existing warnings), all affected tests pass.


## 2026-05-03 — Scribe: Codespaces Dashboard & Auth Fix Decisions Merged

Scribe merged 5 decision inbox files from Blathers session:
- Dashboard port 17214 HTTPS decision
- Commit separation practice
- MockBusinessApp backchannel diagnosis
Also added decisions from Mabel, Tangy, and Copper re: this work.

### 2026-05-03: PR #47 Merged — Codespaces Dashboard and Auth Fixes

**Status:** ✅ Complete

Successfully created and merged PR #47 (`squad/codespaces-dashboard-and-auth-fixes` → `main`).

**PR Contents:**
- Commit `fa7881c`: Dashboard port 17214 fix (10 files changed)
- Commit `455e0d5`: MockBusinessApp backchannel auth fix (1 file changed)
- Commit `c2b5a2b`: Squad decisions merge (5 `.squad/` files)

**CI Results:**
- ✅ test (9 seconds)
- ✅ core-tests (55 seconds)
- ✅ storybook-tests (111 seconds)
- ✅ localhost-auth-playwright (959 seconds / ~16 minutes)

**Key Learning:**
Playwright integration tests with full Aspire + Keycloak stack legitimately take 15-16 minutes to complete. Don't assume a long-running check is stuck — integration tests with container orchestration, OIDC flows, and browser automation require patience.

**Merge Strategy:**
Used `--merge` (not squash) to preserve the two separate product commits for clean release notes. Each commit addresses a distinct user-facing issue, making it easier to track fixes in changelogs and git bisect operations.

**Local State:**
Local `main` branch synced to `origin/main` at commit `cfe90fc` (merge commit for PR #47).


---

## 2026-05-03: PR #47 Merge Completion

**Status:** ✅ Merged to main

**Merge Details:**
- Used `--merge` strategy (preserved separate commits)
- PR created from squad/codespaces-dashboard-and-auth-fixes
- All CI checks passed within expected timeframe
- Merge commit: cfe90fc
- Local main synced to origin/main

**Decision Rationale Recorded:**
Separate commits preserved because:
1. Dashboard port 17214 fix (fa7881c) is independent user-facing issue
2. MockBusinessApp backchannel auth fix (455e0d5) is separate concern
3. Release notes need to track fixes independently
4. Git bisect operations benefit from granular history

This strategy standardized for future PRs with multiple concerns.

**Team Impact:**
Two significant product fixes shipped:
- Codespaces users get correct HTTPS dashboard URL
- MockBusinessApp auth backchannel works correctly
- Foundation set for multi-concern PR merge strategy
