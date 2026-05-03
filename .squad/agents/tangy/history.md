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

