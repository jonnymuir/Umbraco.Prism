
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

### 2026-05-03: Codespaces Login Callback Localhost:9250 Regression Diagnosis

**Context:** After restarting Codespaces, Safari opens OIDC callback URL `https://localhost:9250/signin-oidc?...` instead of the public forwarded URL, causing "cannot connect to server localhost" error.

**Root Cause — Port 44345 Discovery Race:**

AppHost Program.cs line 21 calls `TryDiscoverCodespaceUrls()` which queries `gh codespace ports` for the authoritative browseUrl of port 44345. If port 44345 is **not yet forwarded** when AppHost starts (common after Codespaces resume), `gh codespace ports` returns no entry for 44345, and `testSitePublicUrl` is set to **null** (line 203-204 in AppHost).

When `testSitePublicUrl` is null, AppHost line 127-128 **does not call** `.WithEnvironment("TESTSITE_PUBLIC_URL", ...)` on the testsite resource. TestSite then launches **without** the environment variable.

TestSite Program.cs line 44 reads `TESTSITE_PUBLIC_URL`. If absent, the middleware at lines 48-52 is **never registered**, so `context.Request.Host` is never overridden. The OIDC middleware at PrismOidcConfiguration.cs line 320 builds `redirect_uri` using the raw inbound `context.Request.Host`, which Codespaces forwards as `localhost:44345` or `localhost:9250` (depending on which port the browser hit).

**Why `https://localhost:9250` specifically:**

Aspire's launch profile advertises both `https://localhost:44345` and `http://localhost:9250` (launchSettings.json line 24). When TestSite starts before port 44345 is forwarded, the browser may hit the HTTP endpoint first (port 9250), then get redirected or rewritten by Codespaces proxy to HTTPS. The OIDC middleware captures the **mixed state** — HTTPS scheme with the HTTP port's host value — yielding `https://localhost:9250/signin-oidc`.

**Why flaky across restarts:**

GitHub Codespaces port forwarding is **asynchronous**. On fresh start or resume, port 44345 may not appear in `gh codespace ports` output for 5-30 seconds. If AppHost queries `gh codespace ports` **before** port 44345 is registered, `testSitePublicUrl` is null and TestSite launches without the override middleware. If AppHost queries **after** port registration, the middleware is correctly wired and login works.

**Immediate diagnostic:**

Check AppHost startup logs in `artifacts/startup-status/prism-apphost.log` for the line:
```
[PRISM] Discovered Codespaces URLs — Keycloak: ... TestSite: (port 44345 not yet forwarded) ...
```

If present, confirms port 44345 was not forwarded when AppHost started.

**Safe fix (minimal):**

AppHost line 212 already has fallback logic to **derive** the TestSite URL from the discovered Keycloak URL when port 44345 is absent:
```csharp
businessAppUrl ??= DeriveCodespaceUrl(keycloakUrl, 7245);
```

Add the same pattern for TestSite:
```csharp
testSitePublicUrl ??= DeriveCodespaceUrl(keycloakUrl, 44345);
```

This ensures `TESTSITE_PUBLIC_URL` is always set in Codespaces, even if port 44345 is not yet forwarded when AppHost starts.

**Recommended next step:**

Add the one-line fix to AppHost Program.cs line 212 (right after `businessAppUrl ??= ...`), then test a cold Codespace restart to confirm the derived URL is correct.


## 2026-05-03: Codespaces Login Callback Startup Sequence Diagnosis

**Outcome**: Traced runtime/config source of wrong callback and startup sequence dependency after restart.

**Scope**: Investigated why AppHost startup lag causes TESTSITE_PUBLIC_URL propagation failure on Codespaces resume.

**Key Finding**: Codespaces tunnel rewrites inbound `Host` header to `localhost:44345` before forwarding to Kestrel. TestSite has Host override middleware that corrects this—but only when `TESTSITE_PUBLIC_URL` env var is set. On restart, AppHost must set the var before TestSite starts; timing drift on resume causes OIDC fallback to internal HTTP port 9250.

**Decision**: Contributed to decisions.md entry. Three fix options documented: fail-fast (recommended), fallback detection, test coverage.

## 2026-05-03: MockBusinessApp API Demo Timeout — `localhost:5163` in Browser

**Status:** ✅ Diagnosis complete

**Context:** Sign-in now works, but the "Call Mock Business App API" action in the member dashboard times out. The UI shows the browser calling `http://localhost:5163/api/backoffice/me`, timing out after 10 seconds.

**Root Cause:**

The `DownstreamDemoController` is *server-side code* that's supposed to call MockBusinessApp on behalf of the browser using the member's Bearer token. However, line 142 in `AppHost/Program.cs` sets:
```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This is intended for **server-to-server** calls from TestSite to MockBusinessApp's internal HTTP endpoint, bypassing GitHub Codespaces port forwarding.

But the `DownstreamDemoController` at line 301 reads this environment variable and uses it to build the target URL that gets **returned to the browser** in the response JSON (line 103, 130, 147, 165). The browser-side JavaScript at `memberDashboard.cshtml` line 178-369 then displays this URL in the UI (line 159, 272, 299).

The problem is architectural: `BUSINESSAPP_BACKCHANNEL_URL` is being used for *two different purposes*:
1. Server-side internal HTTP calls (intended use, works correctly)
2. Display URL shown to the user in diagnostic output (unintended leak, shows internal localhost port)

**Why localhost:5163 specifically:**

MockBusinessApp's launchSettings.json advertises both `https://localhost:7245` (HTTPS, for browser) and `http://localhost:5163` (HTTP, for internal calls). In Codespaces, AppHost sets `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` so TestSite's server-side code can reach MockBusinessApp's HTTP endpoint without hitting the GitHub port-forwarding proxy.

But the browser **cannot** reach `localhost:5163` — that's a TestSite-internal address. The browser needs the public Codespaces URL like `https://{token}-7245.{region}.app.github.dev`.

**Why this is a bug:**

1. The DownstreamDemoController is *already running server-side* in TestSite. When it calls `SendDownstreamRequestAsync(targetUrl, authHeader)` at line 75, the HTTP request goes from TestSite's process to MockBusinessApp's process using `localhost:5163` — this works fine.

2. BUT the controller then returns `targetUrl` in the JSON response (line 103, 130, 147, 165), which the browser-side JavaScript displays in the UI (line 272). The browser sees `http://localhost:5163/api/backoffice/me` and thinks the *browser* should call that URL — which fails because the browser can't reach TestSite's internal network.

3. The timeout is a red herring: the controller call *already succeeded* server-side, but the response JSON contains the wrong URL for display purposes. The browser-side JavaScript is trying to navigate to or fetch from that URL (which it can't), leading to confusion.

**Key Insight:**

`BUSINESSAPP_BACKCHANNEL_URL` is a *transport layer* config for server-to-server calls, NOT a *display URL* for browser-facing surfaces. The controller response should use `PrismBusinessApp:WorkflowApiBaseUrl` (the public/browser URL) for display purposes, not the backchannel URL.

**Diagnostic validation:**

Check the DownstreamDemoController response JSON in browser DevTools Network tab. If `url` field is `http://localhost:5163/...`, confirms the backchannel URL leaked into the browser-facing response.

**Safest Fix:**

1. Change `ResolveBusinessAppTransportBaseUrl()` at line 299-310 to *only* use `BUSINESSAPP_BACKCHANNEL_URL` for the *actual HTTP call*, not for the URL returned in the response.

2. Add a separate method `ResolveBusinessAppDisplayUrl()` that returns `PrismBusinessApp:WorkflowApiBaseUrl` (the public browser URL) for use in response JSON.

3. Update lines 103, 130, 147, 165 to use the display URL instead of the transport URL.

**Alternative (simpler) Fix:**

If the DownstreamDemoController response JSON `url` field is only for diagnostics (not used by the browser for navigation), the fix is minimal: just document that the `url` field shows the *server-side transport URL*, not the browser-facing URL. The actual API call will succeed server-side regardless of what URL is displayed.

**Next Step:**

Inspect the actual runtime behavior in Codespaces to confirm whether the server-side call is succeeding but the response JSON misleads the user, or if there's a separate runtime issue causing the timeout.

## Orchestration Update (Scribe 2026-05-03)

MockBusinessApp timeout diagnosis complete. Both agents identified architectural leak: internal backchannel URL leaks to browser-facing response.

**Blathers:** Root cause is BUSINESSAPP_BACKCHANNEL_URL at AppHost line 142 being returned in DownstreamDemoController response
**Tangy:** Contract gap identified — existing test accepts internal URL; new test contracts required for public URL validation

Decisions captured in decisions.md. Orchestration logs: orchestration-log/2026-05-03T17:17:19Z-*.md
Session log: log/2026-05-03T17:17:19Z-mockbiz-timeout-diagnosis.md

Next: Implementation of URL transformation in controller.


## 2026-05-03: MockBusinessApp Browser URL Leak Fix

**Status:** ✅ Complete — PR #48 created

**Context:** After fixing the 401 diagnosis, users could sign in but the "Call Mock Business App API" action showed `http://localhost:5163` in the dashboard UI, which was unreachable from the browser.

**Root Cause:**
The DownstreamDemoController was using `BUSINESSAPP_BACKCHANNEL_URL` for two conflicting purposes:
1. Server-to-server HTTP transport (correct, for efficiency)
2. Display URL returned in JSON response to the browser (incorrect, leaks internal address)

The backchannel URL `http://localhost:5163` is only accessible from TestSite's server process. In Codespaces, the browser needs the public forwarded URL on port 7245.

**Solution:**
- Added `ResolveBusinessAppDisplayBaseUrl()` to always return the public URL from `PrismBusinessApp:WorkflowApiBaseUrl`
- Added `TransformToDisplayUrl()` to rewrite backchannel URLs to public URLs before returning in responses
- Updated all four response returns (success, invalid response, timeout, network error) to use `TransformToDisplayUrl()`
- Updated test from `DownstreamDemo_PrefersBusinessAppBackchannelUrl_WhenConfigured` to `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport`

**Key Design Principle:**
**Transport URLs ≠ Display URLs**

When a backchannel URL is configured:
- Use it for the actual HTTP call (server-to-server efficiency)
- Transform it to the public URL before returning in the response (browser accessibility)

This separation is critical in Codespaces where:
- Internal ports (5163) are server-process-only
- Forwarded ports (7245) are browser-accessible

**Test Coverage:**
The updated test validates the contract:
```csharp
// Backend uses backchannel for transport efficiency
capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));

// But response to browser uses public URL
root.GetProperty("url").GetString().Should().Be(
    "https://codespace-7245.app.github.dev/api/backoffice/me",
    because: "browser-facing URLs must be publicly accessible");
```

**Branch:** `squad/fix-browser-url-leak`
**Commit:** `6774c55`
**PR:** #48
**Test Results:** ✅ All 674 tests pass

**Files Changed:**
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — Added URL transformation methods and applied to all response returns
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — Updated test to validate transport-vs-display separation

**Decision Alignment:**
This fix implements the decision documented in `.squad/decisions.md`:
> Browser-facing API responses must return publicly accessible URLs, not internal server-to-server backchannel URLs.


**Update:** Added browser-level Playwright test (commit `2ebec5a`) to validate the URL displayed in the dashboard does not expose the internal backchannel port :5163. This complements the unit test and ensures the full browser experience prevents regression.

**Test Strategy:**
- Unit test: Validates controller logic (transport uses backchannel, response uses public URL)
- Playwright test: Validates browser UI (displayed URL does not contain :5163, does contain https://localhost:7245)

### 2026-05-03: PR #48 Merged — Browser URL Leak Fix

**Status:** ✅ Complete

Successfully monitored and merged PR #48 (`squad/fix-browser-url-leak` → `main`).

**PR Contents:**
- Commit `6774c55`: Transform internal backchannel URLs to public URLs in browser-facing responses
- Commit `2ebec5a`: Add browser-level contract for backchannel URL visibility (Playwright test)

**CI Results:**
- ✅ test (9 seconds)
- ✅ core-tests (53 seconds)
- ✅ storybook-tests (1m53s)
- ✅ localhost-auth-playwright (15m32s)

**Key Learning:**
Long-running Playwright tests with full Aspire stack + Keycloak + browser automation legitimately take 15+ minutes. The localhost-auth-playwright job ran for over 15 minutes before successfully completing. This is normal for integration tests with container orchestration, OIDC flows, and end-to-end browser automation — not a sign of a stuck or failing test.

**Merge Strategy:**
Used `--merge` (not squash) to preserve the two separate commits for clean release notes:
1. Fix commit addresses the core bug (backchannel URL transformation)
2. Test commit adds browser-level contract validation

Each commit addresses a distinct concern, making it easier to track fixes in changelogs and git bisect operations.

**Local State:**
Local `main` branch synced to `origin/main` at commit `0f79c12` (merge commit for PR #48). The `.squad/` history files have local modifications from this session, which were not mixed into the product PR.

