# Blathers — History (Summary)

**Agent:** Backend specialist shipping Codespaces URL derivation fixes, backchannel rewrites for JWKS/token-refresh, and security analysis for auth isolation.

**Recent focus:** Aspire dashboard Codespaces access, authentication diagnostics, runtime stale-code diagnosis, backchannel OIDC validation, dynamic endpoint discovery, transport diagnostics.

---

## Learnings

- **2026-05-03T21:12:36.429+01:00:** For Codespaces terminal diagnostics, prefer live runtime probes (`gh codespace ports`, `MockBusinessApp /debug/auth`, `TestSite /session-contract`) over guessed localhost ports. Public `app.github.dev` probes should report redirects or HTML tunnel pages as proxy/auth evidence, not false application success.
- **2026-05-03T21:32:41.296+01:00:** Codespaces helper scripts that embed Python should self-check a working stdlib runtime and launch it with `-I` plus `PYTHONHOME`/`PYTHONPATH` scrubbed; activated toolchains can otherwise break even basic imports like `json`.
- **2026-05-03T21:49:23.079+01:00:** For operator-facing Codespaces diagnostics, prefer shell-native `curl`/`gh` probes over embedded runtimes. A helper that only needs Bash plus the stock network tools is more reliable than trying to harden around a missing or broken Python install.
- **2026-05-03T22:27:45.244+01:00:** When downstream API timeout isn't preceded by 401, check whether TestSite is using the backchannel URL or the public app.github.dev URL — if BUSINESSAPP_BACKCHANNEL_URL is actually set at runtime, the timeout is internal; if not, it's hitting the GitHub forwarding tunnel. Named HttpClients have default timeouts (100s); the custom timeout only applies when the named client is registered.
- **2026-05-03T22:49:38.255+01:00:** Named HttpClients used in controllers must be registered via AddHttpClient() even when timeout is managed via CancellationToken, because unregistered clients lack proper handler configuration for connection pooling, localhost resolution, and certificate validation in containerized environments. The "prism-downstream-demo" client was unregistered, causing reliable timeouts despite the backchannel URL being correct.
- **2026-05-03T23:00:12.742+01:00:** Downstream demo diagnostics should surface transport path metadata (internal backchannel vs public tunnel, whether BUSINESSAPP_BACKCHANNEL_URL was present) and timeout cause (timeout CancellationToken vs external cancellation) directly in the response JSON and logs without exposing actual backchannel port numbers or raw tokens. Mask localhost ports as `http://localhost:****` in diagnostics; show full public URLs since they're browser-visible anyway.

---

## 2026-05-03T23:00:12.742+01:00: Downstream Demo Transport Diagnostics Implementation

**Status:** ✅ Complete

**Problem:**
When downstream API calls fail (timeout, network error, non-JSON response), operators need immediate visibility into which transport path was used (internal backchannel vs public Codespaces tunnel), whether BUSINESSAPP_BACKCHANNEL_URL was configured, and whether timeouts were triggered by the 10s timeout vs external cancellation. Without this context, diagnosing stale AppHost wiring vs downstream auth failures requires manual inspection of environment variables and logs.

**Solution:**
Added `BuildTransportDiagnostics()` method to DownstreamDemoController that returns structured transport metadata:
- `transport`: "internal-backchannel", "public-tunnel", or "public-url"
- `backchannelPresent`: boolean flag for BUSINESSAPP_BACKCHANNEL_URL
- `transportBaseUrl`: masked for internal URLs (`http://localhost:****`), full for public URLs
- `targetUrlScheme`: http/https indicator

This metadata is:
1. Included in all response payloads (success, timeout, network error, non-JSON)
2. Logged via structured logging for searchable diagnostics
3. Used to generate contextual hints (e.g., "Try `refresh.sh`" for backchannel timeouts)

**Security:**
- Internal backchannel ports are masked as `http://localhost:****` to avoid exposing ephemeral port assignments
- Public URLs shown in full since they're browser-visible anyway
- No tokens, cookies, or client secrets exposed
- Follows existing dev-only guard (IsDevelopment or Prism:EnableDownstreamDemo)

**Implementation:**
- Updated `/api/prism/downstream-demo` endpoint to include `transport` field in all responses
- Enhanced structured logging with transport metadata
- Contextual hints based on transport type (backchannel vs tunnel)
- Timeout diagnostics distinguish CancellationToken timeout from external cancellation

**Test Results:**
- All 680 Core tests pass
- Build succeeds with no new warnings

**Files Changed:**
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — Added transport diagnostics

**Key Insight:**
Response-visible diagnostics beat verbose logs for operator troubleshooting. When the next Codespaces failure happens, the JSON response will immediately show whether the request used the internal backchannel or hit the GitHub forwarding proxy, eliminating the first round of "what path did it actually use?" investigation.

---

## Earlier Sessions

Full history archived to `history-archive.md` (prior to 2026-05-03 learnings section).

---

---

## Cross-Agent Update: 2026-05-03T23:08:07Z Scribe Coordination

**Spawn manifest consolidated:** Blathers implemented transport diagnostics; Tangy added 5 contract tests. All tests passing.

**Orchestration records logged:**
- `.squad/orchestration-log/2026-05-03T22:08:07Z-blathers.md`
- `.squad/orchestration-log/2026-05-03T22:08:07Z-tangy.md`

**Decisions merged to main registry:**
- Downstream Demo Transport Diagnostics Should Be Response-Visible
- Downstream API Timeout Diagnosis: Unregistered HttpClient Root Cause
- Safe Transport Diagnostics Must Not Expose Internal Ports or Secrets

**Team coordination complete.** Ready for PR review and merge.


## 2026-05-03 · Transport Diagnostics Validation Spawn

**Spawn outcome:** Diagnosed internal backchannel timeout as root cause (not instrumentation defect). Recommended refresh.sh as operational fix.

**Session:** transport-diagnostics-landing | Coordinator spawned to validate transport diagnostics feature post-landing (commit 17edf9c).

**Coordination:** Tangy (Tester) in parallel spawn identified next proof step: fresh token authentication test.
