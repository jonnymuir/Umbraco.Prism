## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---

# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## 2026-05-03: Startup Health-Check vs Forwarded URL Mismatch

**Status:** ✅ Complete.

**Change:** Added `forwardPorts` array to `.devcontainer/devcontainer.json` to pre-forward critical ports (3000, 15135, 44345, 7245, 8443) before health checks run.

**Root cause:** Health checks probe localhost endpoints (e.g., `https://localhost:44345/api/prism/...`), which validate that the local service is running and responding correctly. However, they do **not** validate that the forwarded Codespaces URL exists or is serving content correctly. Port 44345 was declared in `portsAttributes` but **not** in `forwardPorts`, meaning it only forwarded after auto-detection. This created a timing gap where:
- Health checks passed (localhost works)
- Forwarded URL didn't exist yet or returned GitHub's "connecting..." tunnel page
- Users clicking the forwarded URL got a download prompt or blank page

**Fix:** Pre-forward all critical ports by adding them to the `forwardPorts` array. This ensures forwarded URLs exist from the start, eliminating the timing gap.

**Learning:** Health checks validate the **localhost surface**, not the **forwarded surface**. When ports are declared in `portsAttributes` but not `forwardPorts`, Codespaces forwards them lazily after auto-detection, which can happen **after** health checks pass. Pre-forwarding critical ports ensures the user-facing URLs are ready when the status page says "ready".

**Decision:** `📌 2026-05-03: Brewster — Pre-Forward Critical Ports in Codespaces` (decisions.md, IMPLEMENTED)

---

## 2026-05-03: Status Server Missing After Codespace Resume

**Status:** ✅ Complete.

**Change:** Fixed early-exit path in `.devcontainer/on-start.sh` to restart the Node status server if it died during Codespace suspension.

**Root cause:** When a Codespace is resumed and `UmbracoPrism.AppHost` is still alive, `on-start.sh` exits early via `pgrep`. The Node.js process on port 3000 does not survive suspension, so port 3000 is left empty. The GitHub Codespaces tunnel returns `HTTP 404, content-length: 0, no Content-Type, x-content-type-options: nosniff` — which Chrome interprets as an unknown blob and triggers a "Save As" download prompt.

**Fix:** Added a `curl -s --max-time 1 http://localhost:3000/api/status` probe inside the early-exit block. If the probe fails, the Node server is restarted before printing URLs and exiting.

**Learning:** Node processes do not survive Codespace suspension. Any port forwarded via `devcontainer.json` that depends on a Node/shell process must be probed and restarted on every `postStartCommand` run, including the resumed fast-path.

---

## 2026-05-03: Codespaces URL Regression Fix

**Status:** ✅ Complete.

**Change:** Fixed a one-line bug in `get_codespace_url()` in `.devcontainer/on-start.sh` introduced by the full-URL output change.

**Root cause:** The `jq` branch used `| tr -d '/'` to strip trailing slashes from `browseUrl`. `tr -d '/'` strips **all** forward slashes — including `//` in `https://` — producing invalid URLs like `https:CODESPACE-3000.app.github.dev`. This caused browser download prompts and 404 errors on every link printed after `refresh.sh`.

**Fix:** Changed `| tr -d '/'` → `| sed 's|/*$||'` which strips only trailing slashes. The Python fallback already used `.rstrip('/')` correctly and was not touched.

**Learning:** `tr -d CHAR` is a global delete — it removes every occurrence in the stream, not just trailing. Use `sed 's|CHAR*$||'` when the intent is "strip trailing occurrences only". When in doubt, test with a real URL string before shipping.

---

## 2026-05-03: Status Page — Full URL on Startup

**Status:** ✅ Complete.

**Change:** Updated `.devcontainer/on-start.sh` so that when the startup status server comes up, it prints the full clickable URL rather than "open port 3000 in your browser".

- In Codespaces: calls the existing `get_codespace_url 3000` helper (resolves via `gh codespace ports`, falls back to legacy pattern). Port 3000 is pre-declared in `devcontainer.json` so the URL is available before the server starts.
- Locally: prints `http://localhost:3000`.
- CODESPACES.md "Useful tips" updated.
- Decision written to `.squad/decisions/inbox/brewster-startup-url-output.md`.

**Learning:** `get_codespace_url()` can safely be called for any port declared in `devcontainer.json forwardPorts` — Codespaces registers those before any process starts, so the forwarded URL is in `CODESPACE_PORTS_JSON` from the first `gh codespace ports` call.

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

## 2026-05-03: Spawn Manifest — Codespaces URL Fix & Recovery Scripts

**Timestamp:** 2026-05-03T11:07:19.866Z  
**Status:** ✅ Implemented

### Fix 1: Malformed Codespaces URL (tr -d '/' Regression)

**Problem:** After "full-URL output on startup" change, users reported:
- Browser download prompt on printed links
- 404 errors when following links

**Root Cause:** `get_codespace_url()` used `tr -d '/'` which deleted ALL slashes, including `://` in `https://`.
- Input: `https://codespace-name-3000.app.github.dev/`
- Output: `https:codespace-name-3000.app.github.dev` ← invalid

Since `jq` was available, this branch always ran. Python fallback (correct `.rstrip('/')`) was never used.

**Fix:** Replaced `tr -d '/'` with `sed 's|/*$||'` (strips only trailing slashes)

**Impact:** Printed Codespaces URLs now valid and clickable

### Fix 2: Codespaces Recovery Scripts

Added three operator scripts under `scripts/codespaces/`:

1. **`stop.sh`** — Graceful AppHost shutdown (force-kill fallback)
2. **`refresh.sh`** — Fast cycle: stop → git pull → conditional npm install → restart
   - Flags: `--rebuild` (opt-in), `--no-start`
   - Time: ~90s for code-only changes
3. **`health-check.sh`** — Probes 5 readiness endpoints, exits 0/1

**Readiness Endpoints:**
- Port 3000: Status server
- Port 15135: Aspire Dashboard
- Port 44345: TestSite
- Port 8443: Keycloak
- Port 7245: MockBusinessApp

**Rationale:** Canonical operator path for Codespaces failures now documented and scripted.

### Coordination

- Tangy: Reproduced dashboard failure in live Codespaces
- Blathers: Enhanced diagnostics + stale runtime pattern
- Copper: Verified trust chain; recommended restart
- User: Diagnostics over speculation; focus on actual failure runtime


## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-04 | Walkthrough Discoverability Implementation

**Status:** IMPLEMENTED

Completed walkthrough discoverability hardening:
- Removed TestSite stub views blocking Core embedded views
- Restructured member dashboard into Overview and Workflow Demos sections
- Exposed workflow admin URL from MemberDashboardController via IConfiguration
- Added Developer Tools dash-section (conditional rendering)

Verification: `dotnet build` (0 errors, 2 pre-existing warnings); `dotnet test` (690 passed)

Decision recorded: "Walkthrough Discoverability — All Workflow Types Reachable from Dashboard"

## Learnings

### 2026-05-16T13:20:33.659+01:00 | Workflow Editor V1 — Umbraco Integration design

- **Editor hosting:** Chose hybrid option (c): a v17 backoffice section (`prism-workflow-editor`) wrapping a Lit/Web Component that embeds the standalone editor/projection app. This keeps the projection tooling host-agnostic for CLI/agent use while making it discoverable through the Umbraco backoffice. No AngularJS; no Surface Controllers; manifest declared per v17 package API.
- **Surface mapping:** Three surfaces confirmed — (1) Public: unauthenticated Umbraco content shells (`workflowLanding`); (2) Member: `PrismMemberCookie`-protected `workflowPage` + `workflowHub` (both existing, unchanged); (3) Back-stage: MockBusinessApp reviewer surface consuming the same projected `WorkflowDefinitionFile`.
- **DocType strategy:** `workflowPage` and `workflowHub` are stable Core-owned seeded types. V1 adds `workflowLanding` (public explainer shell) and optionally `workflowRegistry` (singleton node for workflow-key picker). No new DocTypes represent the editor itself.
- **Test contract:** `TestSiteViewModelBindingTests` guards against TestSite stub views for `workflowPage` and `workflowHub`. Those stub files currently exist and violate the contract — removing them is the priority-1 acceptance hook before any other TestSite editor work ships.
- **Auth boundary:** Umbraco backoffice auth gates the editor surface; `PrismMemberCookie` gates the member surface; MockBusinessApp role gates the reviewer surface. None cross-contaminate. The `workflow-publisher` capability check must live in the projection API layer, not only in the Lit component.
- **Key file authored:** `docs/design/workflow-editor-v1/03-umbraco-integration.md`

### 2026-05-16T10:59:37.438+01:00 | Workflow editor topology for Prism + Umbraco

- Treat the public site, member portal, and business-app operator surface as three distinct authored/owned experiences: public and member journeys stay in the Umbraco content tree, while business-user workflow operations stay owned by the Business App.
- Keep `workflowPage` and `workflowHub` as the canonical member-facing shells. They surface workflow instances by `workflowKey` and `instanceId`, but the Business App remains authoritative for workflow state, roles, and progression.
- Do not use `workflowDemoPage` as the reference architecture for authored citizen/member journeys; it is a placeholder shell and should not define the package story.
- The planning-application example fits best as a content-authored workflow family: a public explainer/landing page, a protected member `workflowPage` entry, the existing `workflowHub` for resume/history, and the MockBusinessApp admin as the reviewer/business-user surface.
- For future editor work, prefer a mixed model: authored workflow-entry and status pages in `src/UmbracoPrism.TestSite/`, workflow definitions and operator tooling in `src/UmbracoPrism.MockBusinessApp/`, and an Umbraco backoffice extension only for editorial convenience/embedding, not as the primary workflow engine UI.
- Key paths reviewed for this decision: `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs`, `src/UmbracoPrism.Core/Controllers/PrismWorkflowPageController.cs`, `src/UmbracoPrism.Core/Controllers/WorkflowHubController.cs`, `src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs`, `src/UmbracoPrism.TestSite/WorkflowPageSeeder.cs`, `src/UmbracoPrism.TestSite/TestSiteSeedContract.cs`, `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml`, `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification.json`.
