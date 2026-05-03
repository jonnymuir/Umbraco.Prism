## 2026-05-03: Browser Timeout on MockBusinessApp API Call — Backchannel URL Exposure

**Timestamp:** 2026-05-03T18:12:37.055+01:00  
**Status:** 🔍 Diagnosed (contract violation identified, test gap documented)

### Problem

User reports: After sign-in works, clicking "Call Mock Business App API" on the dashboard tries to call `http://localhost:5163/api/backoffice/me` and times out.

### Investigation

The DownstreamDemoController successfully calls the MockBusinessApp backend using the internal backchannel URL (`http://localhost:5163`), but then **returns that internal URL to the browser** in the JSON response:

```csharp
return Ok(new
{
    statusCode = (int)response.StatusCode,
    url = targetUrl,  // ← "http://localhost:5163/api/backoffice/me"
    ...
});
```

The dashboard JavaScript displays this URL to the user (memberDashboard.cshtml line 272). In Codespaces, only port 7245 (HTTPS) is forwarded — port 5163 is never exposed publicly, making the displayed URL unreachable and confusing.

### Contract Violations

1. **Browser-Facing Content Includes Unreachable Localhost URL**
   - **Contract:** URLs displayed to users must be publicly reachable from their browser
   - **Violation:** The response contains `http://localhost:5163`, an internal server-to-server backchannel endpoint that is never forwarded in Codespaces
   - **Expected:** Return the public-facing URL (e.g., `https://v7ldkc4c-7245.uks1.app.github.dev/api/backoffice/me`) or clearly label internal URLs as "not browser-accessible"

2. **Diagnostic Information Exposes Implementation Details**
   - **Contract:** User-facing error messages should focus on observable behavior and actionable next steps
   - **Violation:** Displaying the internal backchannel hop exposes dual HTTP/HTTPS listener setup and internal port numbers
   - **Expected:** Show only the public URL users would use for direct access

### Regression Gap

**Existing test coverage:** `DashboardLocalEndpointsValidationTests.cs` has extensive coverage but:

✅ Line 97-128: `DownstreamDemo_PrefersBusinessAppBackchannelUrl_WhenConfigured` validates that the backchannel URL is used for transport AND returned in the response  
✅ Line 126-127: Actually **asserts** that `http://localhost:5163` is returned, treating it as correct behavior

❌ **No test validates the browser-facing URL contract:**
- No assertion that URLs returned to the dashboard are publicly accessible
- No check that internal backchannel URLs are transformed or hidden
- No validation of URL accessibility in different environments

**Pattern:** Tests focused on "does the server call the right endpoint" miss "is the response safe/useful for the browser client."

### Minimal Reproduction

**Playwright (Codespaces):**
```typescript
await callBusinessAppApi(page);
const apiUrl = page.locator('#api-url-label');
// Will show "http://localhost:5163/api/backoffice/me" — unreachable
```

The current test only checks for `200 OK` status and response body content, not the displayed URL.

**Manual (Codespaces):**
1. Sign in to TestSite dashboard
2. Click "Call Mock Business App API"
3. Observe displayed URL: `http://localhost:5163/api/backoffice/me`
4. Try accessing that URL in browser → timeout

### Recommended Fix

**Option 1 (Preferred):** Return the public-facing URL when backchannel is used for transport  
**Option 2:** Add separate `displayUrl` field for UI, keep `url` for diagnostics  
**Option 3:** Don't display the URL at all (it's an implementation detail)

### Proposed Test Coverage

**Unit test:** `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport` — assert that `capturedRequestUri` uses backchannel but response `url` field uses public endpoint

**Playwright contract test:** Validate displayed URL doesn't contain `:5163` or `localhost:`, and matches expected public URL pattern for environment

### Decision Recorded

`.squad/decisions/inbox/tangy-mockbiz-timeout-diagnosis.md` — full analysis and fix recommendations

### Learnings

**Test coverage blind spot:** Server-side transport optimization (using HTTP backchannel for efficiency) can leak into client-facing responses. Tests that validate "backend calls the right endpoint" don't necessarily catch "frontend displays the wrong endpoint."

**Browser-reachability as a contract:** In Codespaces (and any environment with port forwarding), URLs must be validated for browser accessibility, not just HTTP correctness. The dual HTTP/HTTPS listener pattern requires careful separation between transport URLs (internal) and display URLs (public-facing).

## Orchestration Update (Scribe 2026-05-03)

MockBusinessApp timeout diagnosis complete. Both agents identified architectural leak: internal backchannel URL leaks to browser-facing response.

**Blathers:** Root cause is BUSINESSAPP_BACKCHANNEL_URL at AppHost line 142 being returned in DownstreamDemoController response
**Tangy:** Contract gap identified — existing test accepts internal URL; new test contracts required for public URL validation

Decisions captured in decisions.md. Orchestration logs: orchestration-log/2026-05-03T17:17:19Z-*.md
Session log: log/2026-05-03T17:17:19Z-mockbiz-timeout-diagnosis.md

Next: Implementation of URL transformation in controller.


---

## 2026-05-03: Browser URL Leak Fix — Test Coverage

**Timestamp:** 2026-05-03T18:29:38.303+01:00  
**Status:** ✅ Complete

### Task

Add regression test coverage for the browser-facing backchannel URL leak issue. Work in parallel with Blathers on `squad/fix-browser-url-leak` branch to ensure browser-facing surfaces fail if internal localhost transport URLs leak to users again.

### Outcome

- **Unit test**: Already updated by Blathers in commit `6774c55` — `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport` now validates that:
  - Backend uses `http://localhost:5163` for transport efficiency
  - Response to browser returns `https://codespace-7245.app.github.dev/api/backoffice/me` (public URL)
- **Playwright test**: Added browser-level contract in `localhost-auth-session.spec.ts`:
  - Updated `callBusinessAppApi()` to validate the displayed URL element `#api-url-label`
  - Asserts URL does NOT contain `:5163` (internal backchannel port)
  - Asserts URL DOES contain `https://localhost:7245` (public endpoint)
  - This behavior-level test would have caught the original bug

### Test Results

- **Unit tests**: All 25 `DashboardLocalEndpointsValidationTests` pass
- **Changes committed**: `2ebec5a` on `squad/fix-browser-url-leak` branch
- **Coordination**: Clean commit history with Blathers' controller fix (`6774c55`) followed by my test addition (`2ebec5a`)

### Contract Enforced

✅ **Browser-facing API responses must not expose internal backchannel URLs**

The Playwright test validates this at the user experience level — if the dashboard displays `localhost:5163` again, the test will fail immediately.

### Learnings

**Test coverage layering**: Unit tests validate controller logic (URL transformation), Playwright tests validate browser experience (what users see). Both layers are needed — the unit test caught the implementation, the Playwright test ensures the full UX contract.

**Behavior-level assertions**: Prefer testing what the user sees over implementation details. The Playwright test doesn't care *how* the URL is transformed, only that the displayed URL is publicly accessible.


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
