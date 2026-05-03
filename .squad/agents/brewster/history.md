# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## Session: Downstream Demo HTML Validation Fix (2026-05-02)

**Status:** ✅ Complete — Commit `da7ddc9` on `main`

**Scope:** Fix false-positive bug where `DownstreamDemoController` treated HTML/non-JSON responses as success instead of errors. Tangy found that Codespaces port-forwarding pages ("Connecting to the forwarded port...") returned 200 OK with `text/html`, breaking the dashboard UI.

### Problem

The controller checked HTTP status code but not `Content-Type` header. Any 200 response was treated as success, including:
- `text/html` from Codespaces port-forwarding placeholders
- `text/plain` from misconfigured endpoints
- Other non-JSON responses

Dashboard UI expected structured JSON, so HTML responses broke the interface silently.

### Solution

Added `Content-Type` validation before processing response body:

1. **Validate JSON content type** — Only accept `application/json`, `application/problem+json`, `text/json`
2. **Return structured error for non-JSON** — `statusCode: 0`, `statusText: "Invalid Response"`, with clear error message
3. **Preserve Blathers' backchannel fix** — `BUSINESSAPP_BACKCHANNEL_URL` still takes precedence in Codespaces

**Implementation:**
- Added `IsJsonContentType(string)` helper to check for JSON MIME types
- Validate immediately after receiving HTTP response, before parsing
- Include user-friendly hint about Codespaces port-forwarding delays when HTML detected

**Test Coverage:** Tangy's 3 new regression tests:
- `DownstreamDemo_ReturnsError_WhenResponseIsHtml`
- `DownstreamDemo_DetectsCodespacesPortForwardingPage`
- `DownstreamDemo_RejectsNonJsonContentType`

**Test Results:** 653 Core tests pass (including all HTML validation tests)

**Impact:**
- HTML/non-JSON responses now surface as errors with actionable messages
- Dashboard shows clear error instead of breaking on invalid JSON parse
- Preserves all existing functionality (URL allowlisting, token refresh, backchannel URL)

**End-to-End Note:**
The fix ensures clear error messaging when port-forwarding pages appear. The underlying cause (BusinessApp not ready) still requires waiting for Codespaces to forward the port — but users now see an actionable error instead of a broken UI.

---

## 2026-05-03: Codespaces Recovery Scripts

**Status:** ✅ Complete; merged to main.

**Scope:** Developer experience improvement for Codespaces recovery path.

**Delivered:**
- `scripts/codespaces/stop.sh` — Graceful AppHost/status-server shutdown with force-kill fallback
- `scripts/codespaces/refresh.sh` — Standard recovery: stop → pull → conditional npm install → restart (with `--rebuild` and `--no-start` flags)
- `scripts/codespaces/health-check.sh` — Readiness probes on five endpoints (Status server, Aspire Dashboard, TestSite, Keycloak, MockBusinessApp)
- **CODESPACES.md** updated with full recovery section covering decision tree and readiness endpoints

**Integration:** Scripts delegate to `.devcontainer/on-start.sh` (single source of truth); auto-detect `package-lock.json` changes for npm install.

**Impact:** Developers can now recover stack without full Codespace rebuild (~90 seconds for code-only changes).

---

**📚 Older sessions archived to `history-archive.md` to keep active history under 15KB.**
