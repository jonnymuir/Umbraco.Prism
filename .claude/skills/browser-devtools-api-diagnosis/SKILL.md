# Browser DevTools Manual API Diagnosis Playbook

**Purpose:** Diagnose timeout or failure issues on authenticated API calls from the browser side using DevTools. Isolate whether failures are in button flow, auth header, or network reachability.

**Owner:** Tangy (Tester)  
**Date:** 2026-05-03  
**Audience:** QA, testers, developers debugging API timeouts or 401 errors in authenticated flows

---

## Scenario: Dashboard API Call Times Out

**Setup:**
- You are signed into a protected dashboard page (e.g., `https://localhost:44345/dashboard`)
- You click a button labeled "Call Mock Business App API"
- The browser displays a timeout error after ~10 seconds

**Goal:** Determine if the problem is:
1. **Button flow issue** — JavaScript on the page isn't firing the request
2. **Auth problem** — Token is missing, expired, or incorrectly formatted
3. **Network reachability** — Endpoint is unreachable or misconfigured
4. **CORS issue** — Browser is blocking the request due to cross-origin policy

---

## Diagnostic Playbook

### Phase 1: Capture the Network Request

**Step 1.1: Open DevTools Network Tab**

1. Open your browser's Developer Tools (`F12` or `Cmd+Shift+I`)
2. Click the **Network** tab
3. **Check "Preserve log"** checkbox (top-left, looks like a circular arrow with a stop sign)
   - This keeps log entries even if the page reloads
4. **Ensure requests are being captured** — the dot next to "Network" should be red

**Step 1.2: Clear Previous Requests**

1. Click the trash/clear icon in the Network tab
2. Verify the list is empty

**Step 1.3: Trigger the Request**

1. Navigate to the dashboard: `https://localhost:44345/dashboard`
2. Ensure you see "Welcome back, Demo User" (proves authentication worked)
3. Locate the button labeled "Call Mock Business App API"
4. **Click the button**
5. Wait for the request to complete (you'll see it appear in the Network tab, or the error message on the page)

**Step 1.4: Find the Request in the Network Tab**

Look for a request that:
- **Name** contains: `downstream-demo` or `api` or starts with `https://localhost:7245`
- **Status** is red or shows `504`, `403`, `401`, `0` (timeout), or `pending`

If you see **multiple requests**, look for the **last one** in the chronological list.

---

### Phase 2: Inspect the Request Details

**Step 2.1: Click the Request Row**

Click on the request entry in the Network tab. A details panel opens on the right or below.

**Step 2.2: View the Request Headers**

Click the **Headers** subtab (or scroll to the "Request Headers" section).

**Key headers to check:**

| Header | What to Look For | Expected Value |
|--------|------------------|-----------------|
| `Authorization` | Presence & format | `Bearer <token>` (must start with "Bearer ") |
| `Cookie` | Session cookie | Should show an auth session cookie (e.g., `.AspNetCore.Cookies`) |
| `Content-Type` | Request body format | `application/json` |
| `Accept` | What client expects | `application/json` or `*/*` |

**Example correct request:**
```
GET https://localhost:7245/api/backoffice/me HTTP/1.1
Host: localhost:7245
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
Cookie: .AspNetCore.Cookies=abc123def...
```

**If Authorization header is missing:**
→ **Bug in button flow:** The JavaScript that sends the request isn't including the token. Check if the page can access the token.

---

### Phase 3: Check Response Status & Headers

**Step 3.1: View Response Status**

In the same request details panel, find the **Status** line at the top:

| Status | Meaning | Next Step |
|--------|---------|-----------|
| `200 OK` | Success — check response body | Go to Phase 4 |
| `401 Unauthorized` | Token missing or invalid | Check Phase 2 auth headers; token may be expired |
| `403 Forbidden` | Request is valid but denied | Check server logs for authorization policy failure |
| `504 Gateway Timeout` | Server didn't respond in time | Network issue; try accessing endpoint directly |
| `0` or `(failed)` | Browser timeout or blocked | Browser couldn't reach the endpoint; check CORS or network |

**Step 3.2: View Response Headers**

Click the **Response Headers** subtab to see what the server sent back.

**Key response headers:**

| Header | What It Means |
|--------|--------------|
| `Access-Control-Allow-Origin` | If present and not matching origin, CORS is blocking the request |
| `Content-Type` | Should be `application/json` for API responses |
| `Set-Cookie` | If present, server is trying to set a new cookie (may indicate re-auth) |

**If you see `Access-Control-Allow-Origin: *` or no CORS header:**
→ **Possible CORS issue:** Browser may be blocking the request. Check the **Console** tab for `Cross-Origin Request Blocked` message.

---

### Phase 4: Inspect the Response Body

**Step 4.1: View Raw Response**

In the request details panel:
1. Click the **Response** subtab (or **Preview** for a formatted view)
2. You should see the JSON body that the server returned

**Expected success response:**
```json
{
  "statusCode": 200,
  "url": "https://localhost:7245/api/backoffice/me",
  "elapsedMs": 245,
  "contentType": "application/json",
  "body": {
    "tenant": "Prism Demo (Keycloak)",
    "assignedRole": "Admin",
    "userEmail": "demo@prism.local"
  }
}
```

**Expected timeout response:**
```json
{
  "statusCode": 0,
  "statusText": "Timeout",
  "url": "https://codespace-7245.uks1.app.github.dev/api/backoffice/me",
  "elapsedMs": 10000,
  "summary": "Timed out after 10 seconds via internal-backchannel while targeting /api/backoffice/me.",
  "nextCheck": "Check MockBusinessApp health for /api/backoffice/me and confirm AppHost injected BUSINESSAPP_BACKCHANNEL_URL.",
  "timeout": {
    "timedOutByUs": true,
    "timeoutWindowMs": 10000,
    "cancellationSource": "request-timeout-window"
  },
  "transport": {
    "transport": "internal-backchannel",
    "usingBackchannel": true,
    "backchannelPresent": true,
    "transportBaseUrl": "http://localhost:****",
    "targetUrlScheme": "http",
    "targetPath": "/api/backoffice/me"
  }
}
```

**If you see `transport.transport == "internal-backchannel"` with `transportBaseUrl == "http://localhost:****"`**
→ The `****` means the internal localhost port was intentionally masked. Treat that as **server-side backchannel wiring** evidence, not a browser URL.

**If you see `timeout.cancellationSource == "request-timeout-window"`**
→ The 10-second deadline came from TestSite's own downstream timeout window, not from the browser cancelling the fetch.

---

### Phase 5: Copy Request as cURL / Fetch

**This step isolates whether the problem is the button flow or the API endpoint itself.**

**Step 5.1: Copy the Request as cURL**

1. Right-click the request row in the Network tab
2. Select **Copy as cURL** (exact wording varies by browser)
3. Open a Terminal or Command Prompt
4. Paste and run the command

**Example cURL command:**
```bash
curl -X GET "https://localhost:7245/api/backoffice/me" \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Cookie: .AspNetCore.Cookies=abc123def..." \
  -k  # (macOS/Linux: ignore self-signed cert)
```

**What this tests:**
- If the request **succeeds** with the same headers → API endpoint is reachable; button flow issue is likely
- If the request **times out** → Endpoint is unreachable; network/config issue

**Step 5.2: Copy the Request as Fetch (Browser Console)**

Alternatively, in the same **Copy** menu, select **Copy as Fetch** (if available).

This gives you JavaScript you can paste directly into the browser console:

```javascript
fetch("https://localhost:7245/api/backoffice/me", {
  "method": "GET",
  "headers": {
    "Authorization": "Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
    "Cookie": ".AspNetCore.Cookies=abc123def..."
  }
})
.then(r => r.json())
.then(console.log)
.catch(console.error)
```

**To use this:**

1. Open the browser console (`F12` → **Console** tab)
2. Paste the fetch command
3. Press Enter

**What to look for in the response:**
- **`200` with JSON body** → Endpoint works, button flow is the issue
- **`401` or `403`** → Auth token problem (expired or revoked)
- **`0` status (Network error)** → Endpoint unreachable

---

### Phase 6: Test Direct Endpoint Access (No Auth)

**This isolates whether the endpoint exists and is healthy.**

**Step 6.1: Access the Endpoint Directly**

In a browser tab or terminal, access the endpoint without auth:

```bash
curl -X GET "https://localhost:7245/api/backoffice/me" -k
```

(The `-k` flag ignores self-signed certificate warnings on macOS/Linux.)

**Expected responses:**

| Status | Meaning |
|--------|---------|
| `401 Unauthorized` | ✅ Endpoint is healthy and enforcing auth. Good sign. |
| `504 Timeout` | ❌ Endpoint is unreachable or down |
| `HTML page (port forwarding notice)` | ❌ Wrong port or endpoint not found |

**If you get `401` without auth, but `Timeout` with auth:**
→ The auth header itself (or the token processing) is causing a timeout. Check if the token validation service is responsive.

---

### Phase 7: Compare Authenticated vs. Unauthenticated Access

**Step 7.1: Test Without Token**

```bash
curl -X GET "https://localhost:7245/api/backoffice/me" -k
```

**Expected:** `401 Unauthorized` (endpoint exists and requires auth)

**Step 7.2: Test With Token**

```bash
curl -X GET "https://localhost:7245/api/backoffice/me" \
  -H "Authorization: Bearer <your-token>" \
  -k
```

**Expected:** `200 OK` + JSON response

**If 401 with token → 200 without:**
→ Contradictory behavior. Check if:
- Token is expired
- Token validation endpoint is down
- Authorization header format is wrong

---

### Phase 8: Check Browser Console for Errors

**Step 8.1: Open Console Tab**

Click the **Console** tab in DevTools.

**Look for errors like:**

| Error Message | Meaning |
|---------------|---------|
| `Access to fetch at '...' from origin 'localhost:44345' has been blocked by CORS policy` | CORS issue; API endpoint needs `Access-Control-Allow-Origin` header |
| `TypeError: Failed to fetch` | Network failure; endpoint unreachable |
| `ReferenceError: token is not defined` | Button flow bug; JavaScript can't access the token |
| `Uncaught SyntaxError: Unexpected token <` | Response is HTML (not JSON), likely a port-forward error page |

---

### Diagnosis Decision Tree

```
Q1: Do you see a network request in DevTools?
  YES → Q2
  NO → Button flow broken; JavaScript not firing the request

Q2: What is the HTTP status?
  200 → API works; check response body for expected fields
  401/403 → Auth issue; check Authorization header
  0 / Timeout → Q3
  504 → Server timeout; check backend health

Q3: Can you reach the endpoint without auth (curl -X GET https://localhost:7245/api/backoffice/me)?
  401 (expected) → Auth is preventing access (correct)
  Timeout → Endpoint unreachable; check network/firewall
  HTML page → Wrong endpoint or port

Q4: Does the Authorization header include a Bearer token?
  YES → Check if token is valid (try decoded JWT at jwt.io)
  NO → Token not being set on request; button flow issue

Q5: Endpoint works with cURL but times out in browser?
  YES → CORS or browser-specific issue; check console for CORS error
  NO → Consistent network issue across tools
```

---

## Quick Reference: Timeout Investigation

**You are seeing "timeout after 10 seconds" on the button:**

1. **Open DevTools Network tab** and reproduce the click
2. **Find the request** (will show as `0` status or `pending` → timeout)
3. **Check the URL** in the request details
   - If it's `http://localhost:5163/...` → **Port reachability issue** (internal backchannel URL)
   - If it's `https://localhost:7245/...` → **External endpoint issue**
4. **Check Authorization header** — is the Bearer token present?
5. **Try the same request in cURL** with the copied headers
   - If cURL works but browser times out → CORS issue
   - If cURL also times out → Network/endpoint issue (not browser-specific)

---

## Playbook Summary

| Phase | Goal | Tool | Expected Outcome |
|-------|------|------|------------------|
| 1 | Capture request | DevTools Network | See request in tab |
| 2 | Check auth | Request Headers | See `Authorization: Bearer <token>` |
| 3 | Check status | Response Status | `200` or identified error code |
| 4 | Inspect response | Response Body | Valid JSON or error message |
| 5 | Isolate endpoint | cURL / Fetch | Reproduce same result outside browser |
| 6 | Test endpoint health | Direct curl | Confirm endpoint responds to unauthenticated request |
| 7 | Compare auth levels | cURL (with & without) | Confirm token is validated correctly |
| 8 | Catch JS errors | Console Tab | Identify client-side failures |

---

## Examples

### Example 1: Successful Diagnosis — Auth Header Missing

**Symptoms:** Button click shows timeout.

**DevTools findings:**
- Request to `https://localhost:7245/api/backoffice/me`
- Status: `401 Unauthorized`
- Headers: **No `Authorization` header**

**Conclusion:** Button flow is broken; JavaScript isn't attaching the token.

**Next step:** File bug against the button component; token not being retrieved.

---

### Example 2: Successful Diagnosis — Port Unreachable

**Symptoms:** Button click shows timeout after 10 seconds.

**DevTools findings:**
- Request to `http://localhost:5163/api/backoffice/me`
- Status: `0` (timeout)
- Response body shows: `"statusCode": 0, "statusText": "Timeout"`

**Conclusion:** Server tried to call internal backchannel URL that doesn't exist or is on wrong port.

**Next step:** Check AppHost backchannel configuration; confirm port assignment.

---

### Example 3: Successful Diagnosis — CORS Block

**Symptoms:** Button click makes request but browser blocks it.

**DevTools findings:**
- Request to `https://localhost:7245/api/backoffice/me`
- Status: `0` (blocked)
- Console error: `Access to fetch at 'https://localhost:7245/api/backoffice/me' from origin 'https://localhost:44345' has been blocked by CORS policy`

**Conclusion:** API endpoint doesn't allow requests from dashboard origin.

**Next step:** Add `Access-Control-Allow-Origin: https://localhost:44345` to API response headers.

---

## Environment-Specific Notes

### Localhost Development

- Endpoints are `https://localhost:<port>`
- Self-signed certificates are expected; use `-k` in cURL
- Ports should be consistent (launchSettings.json)

### Codespaces

- Public endpoints are `https://<random-hash>-<port>.uks1.app.github.dev`
- Some ports may not be forwarded (e.g., internal backchannel port `5163`)
- URLs displayed in UI should use public Codespaces URL, not internal `localhost:<port>`

### CI/CD Environment

- Full DNS names (e.g., `https://prism-staging.azure-dev.io`)
- May require VPN or allowlist for cross-origin requests
- Certificate validation is stricter; don't use `-k`

---

## Related Skills

- `aspire-dynamic-endpoint-backchannels` — How to use `.GetEndpoint("protocol")` for backchannel URLs
- `inline-api-failure-states` — Designing API responses to surface failure reasons clearly
- `dev-session-contract-probe` — Validating API contracts at app startup
