# Blathers — History (Summary)

**Agent:** Backend specialist shipping Codespaces URL derivation fixes, backchannel rewrites for JWKS/token-refresh, and security analysis for auth isolation.

**Recent focus:** Aspire dashboard Codespaces access, authentication diagnostics, runtime stale-code diagnosis, backchannel OIDC validation, dynamic endpoint discovery.

---

## 2026-05-03: BusinessApp Backchannel Timeout Fix — Dynamic Endpoint Discovery

**Status:** ✅ Implemented (PR #49)

**Problem:**
Browser-facing downstream API demo button shows correct public URL (fixed in PR #48), but server-side API call times out after 10 seconds in Codespaces. MockBusinessApp admin page loads successfully, proving the app is running.

**Root Cause:**
AppHost hardcoded `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` (line 142), assuming port 5163 is always correct. However, Aspire may assign ephemeral ports or not bind the HTTP endpoint at the expected address in Codespaces, causing the hardcoded URL to become unreachable.

**Solution:**
Changed to `businessApp.GetEndpoint("http")` for dynamic endpoint discovery, matching the pattern already used successfully for Keycloak (line 134). This ensures the backchannel URL points to the actual runtime HTTP endpoint regardless of port assignment.

**Implementation:**
- AppHost/Program.cs: Use `businessApp.GetEndpoint("http")` instead of hardcoded URL
- DashboardLocalEndpointsValidationTests.cs: Updated test contract to validate dynamic discovery pattern
- Added explanatory comment about Aspire ephemeral port assignment

**Test Results:**
- All 674 Core tests pass
- Test validates the new dynamic discovery pattern with explanatory "because" clause

**Operational Recovery:**
After merging PR #49, restart the Aspire AppHost in Codespaces. The backchannel will automatically resolve to the correct runtime endpoint, fixing the timeout.

**Key Insight:**
Containers (like Keycloak) and projects (like MockBusinessApp) both work with `GetEndpoint("http")` for dynamic discovery. The previous failure with `GetEndpoint("https")` was specific to service discovery URLs on HTTPS endpoints — HTTP endpoints return plain `http://localhost:{port}` URLs that work from plain HttpClient.

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

---
date: 2026-05-03T19:40:50Z
status: complete
area: implementation, orchestration, aspire-endpoints
---

# Session Coordination: Downstream API Timeout — Dynamic Endpoint Discovery Fix

## Team Outcome

Parallel investigation with Tangy (Test) identified and fixed downstream API timeout root cause.

**Root Cause:** AppHost hardcoded `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` instead of using Aspire's dynamic endpoint discovery.

**Implementation:** Changed to `businessApp.GetEndpoint("http")` pattern (commit `2a46494`), matching the proven Keycloak backchannel approach.

## Implementation Details

1. **AppHost change:** Line 142 now uses `businessApp.GetEndpoint("http")` for runtime discovery
2. **Test contract:** Added regression coverage in `DashboardLocalEndpointsValidationTests`
3. **Why HTTP works:** Returns plain `http://localhost:{port}` URL compatible with bare HttpClient
4. **Why HTTPS doesn't:** Returns service discovery URL requiring Aspire SDK extensions

## PR Status

PR #49 ready for merge. After merge, restart Aspire AppHost in Codespaces — backchannel will resolve to correct runtime endpoint.

## Coordination

- Decisions archived to `.squad/decisions.md`
- Orchestration log: `.squad/orchestration-log/2026-05-03T18:40:50Z-blathers.md`
- Session log: `.squad/log/2026-05-03T18:40:50Z-downstream-timeout-diagnosis.md`

---

## 2026-05-03: Manual Diagnostic Flow — Read-Only

**Status:** ✅ Complete (read-only task)

**Goal:** Provide operator-friendly diagnostic steps to manually prove:
1. API reachability (internal backchannel vs public)
2. Bearer token validity
3. Keycloak backchannel accessibility
4. Separation of browser-facing vs server-side failures

**Deliverables:**
- `MANUAL_DIAGNOSIS_FLOW.md` — Comprehensive step-by-step guide with expected outcomes
- `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt` — Cheat sheet for quick triage

**Key Insight:**
The 10-second timeout in DownstreamDemoController can fail in 5 distinct ways:
1. **Aspire port reassignment** — Port 5163 isn't actually listening (connection refused)
2. **Service hung** — Port listens but no response (timeout persists)
3. **Bearer token expired** — API responds with 401 but no retry
4. **Keycloak backchannel blocked** — Signing keys unreachable (401 from validation)
5. **GitHub port forwarding tunnel** — HTML page returned instead of JSON

**Diagnostic Strategy:**
Use curl to isolate each layer: HTTP internal → HTTPS public → Bearer token → Keycloak backchannel. Each layer is testable independently without code changes or complex tooling.

**Files Changed:** None (read-only task)

---

## Learnings

- **2026-05-03T21:12:36.429+01:00:** For Codespaces terminal diagnostics, prefer live runtime probes (`gh codespace ports`, `MockBusinessApp /debug/auth`, `TestSite /session-contract`) over guessed localhost ports. Public `app.github.dev` probes should report redirects or HTML tunnel pages as proxy/auth evidence, not false application success.
- **2026-05-03T21:32:41.296+01:00:** Codespaces helper scripts that embed Python should self-check a working stdlib runtime and launch it with `-I` plus `PYTHONHOME`/`PYTHONPATH` scrubbed; activated toolchains can otherwise break even basic imports like `json`.
- **2026-05-03T21:49:23.079+01:00:** For operator-facing Codespaces diagnostics, prefer shell-native `curl`/`gh` probes over embedded runtimes. A helper that only needs Bash plus the stock network tools is more reliable than trying to harden around a missing or broken Python install.
- **2026-05-03T22:27:45.244+01:00:** When downstream API timeout isn't preceded by 401, check whether TestSite is using the backchannel URL or the public app.github.dev URL — if BUSINESSAPP_BACKCHANNEL_URL is actually set at runtime, the timeout is internal; if not, it's hitting the GitHub forwarding tunnel. Named HttpClients have default timeouts (100s); the custom timeout only applies when the named client is registered.

---
## 2026-05-03: Codespaces Downstream Diagnostics Script

**Spawn manifest outcome recorded.** 
- Added `scripts/codespaces/diagnose-downstream.sh` for live runtime diagnostics
- Updated `CODESPACES.md` with diagnostic workflow
- Recorded decision: "Codespaces Downstream Diagnostics Should Prefer Live Runtime Probes"
- Validated DashboardLocalEndpointsValidationTests passed
- Collaborated with Tangy on enhanced browser diagnostics integration

**Learnings:**
- Live runtime probes more reliable than static config validation in Codespaces environment
- Internal backchannel URLs must not be exposed in browser-facing responses
- Manual diagnosis flow critical for operator troubleshooting

---

## 2026-05-03: Diagnostics Script Python Runtime Hardening — Team Orchestration

**Status:** ✅ Complete (decision merged to .squad/decisions.md)

**Team Context:** Orchestrated with Tangy (test contract validation) and Mabel (product commit to main)

**Decision:** Codespaces Diagnostics Scripts Should Verify a Clean Python Runtime
- Added runtime detection and `-I` isolation to diagnose-downstream.sh
- Scrub PYTHONHOME, PYTHONPATH environment overrides
- Fallback handling for broken system Python

**Collaboration:**
- Tangy hardened the test contract: `CodespacesDiagnosticsScript_IgnoresAmbientPythonShellOverrides()`
- Mabel landed product-scoped fix to main (commit fb1b324) with supporting documentation
- Decisions merged by Scribe (this session)

**Outcome:** Product deliverable live on main. Scope discipline established for future product vs bookkeeping separation.

## 2026-05-03: Diagnostics Script No-Python Rewrite (SESSION COMPLETION)

**Orchestration log:** `.squad/orchestration-log/2026-05-03T21:00:48Z-blathers.md`

### Work Summary
- Rewrote `scripts/codespaces/diagnose-downstream.sh` to be shell-native (curl-based probes)
- Eliminated Python runtime dependency for Codespaces operator troubleshooting
- Implemented `gh codespace ports` integration for forwarded browse URLs
- Updated documentation: CODESPACES.md, MANUAL_DIAGNOSIS_FLOW.md
- Validated no-Python code path end-to-end

### Decision Established
- **Codespaces Downstream Diagnostics Must Not Depend on Python** (PROPOSED)
  - Rationale: Reliability for broken environments; operator ergonomics; security on secrets handling
  - Preference: Bash, curl, gh CLI first; fallback only when no shell-native alternative exists

### Cross-Agent Context
- **Tangy (Tester):** Reviewed and strengthened regression coverage for no-Python contract
- **Mabel (Technical Writer):** Committed product changes to main as 22843a2; established product/bookkeeping separation workflow

### Next Steps
- Production validation by users and future Codespaces diagnostics maintenance follow established shell-first preference

---

## 2026-05-03T22:13:58.511+01:00: Session-contract downstream path analysis

**Status:** ✅ Complete (read-only analysis)

**Evidence reviewed:**
- `session-contract` reported authenticated cookie state, access/refresh/ID token presence, `authorizationHeaderReady=true`, `scheme=Bearer`, resolved generic OIDC tenant, and ready seed data.
- Current AppHost wiring uses dynamic `BUSINESSAPP_BACKCHANNEL_URL=businessApp.GetEndpoint("http")` and `KEYCLOAK_BACKCHANNEL_URL=keycloak.GetEndpoint("http")` in Codespaces.
- Current MockBusinessApp auth wiring trusts the public OIDC authority, supports `aud` or `azp` binding for generic OIDC tokens, and exposes `/debug/auth` to confirm live JWKS/backchannel state.

**What this rules out:**
- Missing TestSite sign-in cookie or missing stored tokens
- PrismContext being unable to mint a bearer header at probe time
- Unresolved tenant/hostname mapping on the TestSite side
- Browser-to-BusinessApp CORS/public-tunnel transport as the primary hop for the downstream demo request (the server-side call uses the internal backchannel)

**Most likely remaining failure point:**
- Live MockBusinessApp bearer-token validation/runtime wiring, especially stale AppHost/BusinessApp state where the running process does not actually have the expected Keycloak backchannel/JWKS configuration or is still serving older JWT-validation code.

**Highest-signal follow-up checks:**
1. Check live `https://localhost:7245/debug/auth` (or Codespaces equivalent) for `backchannelJwksEnabled=true`, correct `backchannelUrl`, and a successful `backchannelProbe`.
2. Inspect the actual `/api/prism/downstream-demo` response body/diagnostic body for `401 invalid_token`, timeout, or non-JSON tunnel HTML to separate auth rejection from transport/tunnel failure.
3. If it is a 401, compare the live BusinessApp process against a fresh restart (`bash scripts/codespaces/refresh.sh`) because current repo tests already cover the generic OIDC issuer + `azp` path and dynamic backchannel wiring.

---
## 2026-05-03T22:27:45Z: Spawn Manifest — Timeout Path Analysis

**Status:** ✅ Complete (analysis)

**Execution:** Analyzed downstream timeout path for live TestSite process.

**Conclusion:** Highest-probability remaining issue is that live TestSite process is using the public forwarded BusinessApp URL instead of the internal backchannel URL. This explains why the browser request reaches the dashboard (via public 7245 URL) but the server-side API call times out.

**Handoff to Tangy:** Operator flow reduced to three diagnostic checks:
1. Run diagnostics script with real bearer token
2. Compare public-vs-localhost cURL for copied request
3. Only probe Keycloak JWKS if both still hang

**Artifact:** `.squad/orchestration-log/2026-05-03T21-27-45Z-blathers.md`

