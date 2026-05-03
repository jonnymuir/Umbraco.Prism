**DevTools + cURL combo:** Testing both in the browser (to catch browser-specific issues like CORS) and via cURL (to confirm endpoint health) gives two independent data points that narrow down the root cause.

**Response structure matters:** A well-designed failure response (including `statusCode`, `statusText`, attempted URL) is diagnostic gold — it tells you which of the three hops (button → middleware → downstream endpoint) failed.

---

## 2026-05-03: Downstream API Timeout — Hardcoded Backchannel Port Issue

**Timestamp:** 2026-05-03T19:40:50.786+01:00  
**Status:** 🔍 Diagnosed (architectural gap identified, handed to Blathers)

### Problem

User reports: After the URL transformation fix (showing public 7245 URL correctly), the browser call to "Call Mock Business App API" still times out after 10 seconds, even though the MockBusinessApp admin page is reachable.

### Investigation

The URL transformation fix (commits `6774c55`, `2ebec5a`) **is working correctly** - it transforms the internal `http://localhost:5163` backchannel URL to the public Codespaces URL in browser-facing responses.

**But:** The actual server-to-server call from TestSite to MockBusinessApp is timing out because AppHost line 142 hardcodes:

```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This assumes port 5163 is always correct, but Aspire may assign ephemeral ports in Codespaces. The correct pattern (already used for Keycloak at line 134) is:

```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

This gets the **actual runtime HTTP endpoint** that Aspire assigned.

### Behavioral Contract Violation

**Contract:** Server-to-server API calls must complete within the configured timeout (10 seconds)

**Current behavior:**
- TestSite attempts to call hardcoded `http://localhost:5163/api/backoffice/me`
- Port is unreachable or wrong
- Request times out after 10 seconds
- Controller returns "Timeout" response (statusCode 0)
- Browser displays: "We could not reach the Mock Business App..."

**Expected behavior:**
- TestSite calls MockBusinessApp's actual runtime HTTP endpoint
- Request completes successfully (200 OK)
- Browser displays: "Mock Business App responded successfully."

### Test Coverage Analysis

**Existing tests:**
- ✅ `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport` validates URL transformation with stub handler
- ✅ Playwright `callBusinessAppApi()` validates the end-to-end flow including button click, status badge, response body
- ❌ **Test gap:** The Playwright test runs against a live Aspire stack, so it SHOULD catch this bug
- ❌ **BUT:** The test may be passing because it's running in a local dev environment where port 5163 IS correct, not in Codespaces where Aspire assigns ephemeral ports

**Smallest regression test surface:**

The existing Playwright test `callBusinessAppApi()` (localhost-auth-session.spec.ts, line 150-186) **should fail** if the backchannel is wrong:

```typescript
await expect(statusBadge).toHaveText(/200 OK/, { timeout: 120_000 });
```

If the timeout happens, this assertion should fail with:
```
Expected API call to succeed with 200 OK, but got:
Status: Timeout
```

### Recommended Fix (Handed to Blathers)

**Change AppHost line 142:**

FROM:
```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

TO:
```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

This ensures TestSite uses the actual runtime HTTP endpoint, matching the Keycloak pattern.

**Also fix failing unit test `AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls` at line 302:**

The test looks for `.WithEnvironment(...)` but the actual code has `testsite.WithEnvironment(...)`. Update the assertion to:

```csharp
program.Should().Contain("BUSINESSAPP_BACKCHANNEL_URL");
program.Should().Contain("businessApp.GetEndpoint(\"http\")");
```

### Why I Stayed Read-Only

This is an **AppHost configuration issue**, not a test-only fix. The architectural pattern (hardcoded port vs runtime endpoint) belongs to Blathers' domain. The test would pass if the configuration were correct.

The fix is obvious (use `.GetEndpoint("http")`), but implementing it requires:
1. Understanding Aspire endpoint semantics
2. Verifying MockBusinessApp exposes HTTP endpoint correctly
3. Validating behavior in Codespaces environment
4. Potentially adjusting other backchannel-related config

This is infrastructure work, not test surface work.

### Learnings

**Hardcoded ports vs runtime endpoints:** When using Aspire, always prefer `.GetEndpoint("protocol")` over hardcoded `localhost:port` strings. Aspire may assign ephemeral ports, especially in containerized/Codespaces environments.

**Test coverage vs environment coverage:** A Playwright test running against localhost may pass even when Codespaces fails, if the localhost environment happens to match the hardcoded assumptions. Environment-specific failures require environment-specific test runs.

**URL transformation ≠ endpoint reachability:** The URL transformation fix solved the **display** problem (showing public URLs to browsers), but didn't solve the **transport** problem (server reaching the backchannel). These are separate concerns that both need fixing.

### Decision Recorded

`.squad/decisions/inbox/tangy-downstream-timeout.md` — full diagnosis and fix recommendation


---
date: 2026-05-03T19:40:50Z
status: complete
area: testing, orchestration
---

# Session Coordination: Downstream Timeout Root Cause Diagnosis

## Team Outcome

Two-agent parallel investigation identified root cause of downstream API timeout:

**Tangy (Diagnosis):**
- ✅ Confirmed PR #48 URL transformation fix was correct
- ✅ Isolated root cause: AppHost hardcoded backchannel port instead of using Aspire dynamic discovery
- ✅ Test gap identified: Playwright test doesn't run in full Aspire + Codespaces environment

**Blathers (Implementation):**
- ✅ Implemented fix: Changed to `businessApp.GetEndpoint("http")` pattern
- ✅ Updated regression test: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- ✅ PR #49 ready for merge and Aspire restart

## Key Finding

Aspire port assignment may differ from launchSettings.json in Codespaces. Dynamic endpoint discovery is the only reliable pattern for backchannel URLs.

## Coordination

- Decisions archived to `.squad/decisions.md`
- Orchestration logs written to `.squad/orchestration-log/`
- Session log: `.squad/log/2026-05-03T18:40:50Z-downstream-timeout-diagnosis.md`

---
## 2026-05-03: Codespaces Downstream Diagnostics Script — Operator Guardrails

**Timestamp:** 2026-05-03T21:12:36.429+01:00  
**Status:** ✅ Complete

### Task

Turn the manual downstream diagnosis into a runnable Codespaces helper script that distinguishes transport failures, tunnel/auth HTML responses, token-validation failures, and stale Keycloak backchannel wiring.

### Outcome

- Added `scripts/codespaces/diagnose-downstream.sh`
- Script performs a safe unauthed pass first (`/debug/auth`, public API probe, Keycloak discovery)
- Optional `PRISM_BEARER_TOKEN` input enables direct authenticated checks without printing the token
- Failure output now includes concrete next-step commands (`refresh.sh`, `gh codespace ports`, `/debug/auth`, AppHost log tail)
- Updated `CODESPACES.md` and `MANUAL_DIAGNOSIS_FLOW.md` so operators start with the script before falling back to the longer manual playbook

### Reviewer Notes

Blathers had produced a quick-reference text file, but it still left too much operator interpretation around stale runtime/backchannel state. The new script closes that gap by reading `/debug/auth` and comparing runtime Keycloak wiring with the repo's current Codespaces expectations.

### Validation

- `bash -n scripts/codespaces/diagnose-downstream.sh`
- `bash scripts/codespaces/diagnose-downstream.sh`
- `dotnet test src/UmbracoPrism.Core.Tests/UmbracoPrism.Core.Tests.csproj --filter DashboardLocalEndpointsValidationTests --nologo`

---
## 2026-05-03: Codespaces Downstream Diagnostics Script

**Spawn manifest outcome recorded.**
- Reviewed and strengthened diagnostics flow with Blathers
- Enhanced browser devtools diagnostic with runtime probe integration
- Updated operator guidance for Codespaces troubleshooting
- Validated targeted regression tests: DashboardLocalEndpointsValidationTests
- Recorded decision: "Browser-Facing API Responses Must Not Expose Internal Backchannel URLs"

**Learnings:**
- Browser devtools integration provides superior visibility over static endpoint lists
- Public URL transformation critical for user-facing API responses
- Regression test coverage ensures reliability across environment configurations
