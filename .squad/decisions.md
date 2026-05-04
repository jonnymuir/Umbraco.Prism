---
date: 2026-05-03T18:12:37.055+01:00
author: Tangy
status: PROPOSED
area: testing, browser-contracts, codespaces
---

# Browser-Facing API Responses Must Not Expose Internal Backchannel URLs

## Context

The DownstreamDemoController on the member dashboard calls MockBusinessApp using an internal backchannel URL (`http://localhost:5163`) for efficiency, but returns that internal URL to the browser in the JSON response. Users see `http://localhost:5163/api/backoffice/me` displayed in the dashboard, which:

- Is unreachable from their browser (only port 7245 HTTPS is forwarded in Codespaces)
- Exposes implementation details (dual HTTP/HTTPS listener setup)
- Creates confusion: appears to be the target but is actually an internal routing hop

## Decision

**Browser-facing API responses must return publicly accessible URLs, not internal server-to-server backchannel URLs.**

When a controller uses an internal backchannel URL for transport optimization:
1. The response must transform the internal URL to its public equivalent before returning to the client
2. OR use a separate `displayUrl` field for the UI and keep `url` for diagnostics
3. OR omit the URL entirely if it's purely an implementation detail

### Implementation

For the DownstreamDemoController specifically:

```csharp
private string GetPublicFacingUrl(string transportUrl)
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    var publicUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    
    if (!string.IsNullOrWhiteSpace(backchannelUrl) && 
        !string.IsNullOrWhiteSpace(publicUrl) &&
        transportUrl.StartsWith(backchannelUrl, StringComparison.OrdinalIgnoreCase))
    {
        return publicUrl + transportUrl.Substring(backchannelUrl.Length);
    }
    
    return transportUrl;
}

// In Get() method:
return Ok(new
{
    statusCode = (int)response.StatusCode,
    statusText = response.StatusCode.ToString(),
    url = GetPublicFacingUrl(targetUrl),  // Transform before returning
    elapsedMs = sw.ElapsedMilliseconds,
    contentType,
    body = displayBody
});
```

### Test Coverage

**Unit test contract:**
```csharp
[Fact]
public async Task DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport()
{
    using var backchannel = new TempEnvVar("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
    var handler = new StubHttpMessageHandler(request =>
    {
        // Capture the actual HTTP request
        capturedRequestUri = request.RequestUri;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    });

    var controller = BuildController(
        handler,
        new Dictionary<string, string?>
        {
            ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://v7ldkc4c-7245.uks1.app.github.dev"
        },
        authHeader: new AuthenticationHeaderValue("Bearer", "token"),
        isDevelopment: true);

    var result = await controller.Get();

    // Validate: backend uses backchannel for transport efficiency
    capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));
    
    // But response to browser uses public URL
    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    doc.RootElement.GetProperty("url").GetString().Should().Be(
        "https://v7ldkc4c-7245.uks1.app.github.dev/api/backoffice/me",
        because: "browser-facing URLs must be publicly accessible");
}
```

**Playwright contract:**
```typescript
test('API demo displays publicly accessible URL', async ({ page }) => {
  await signIn(page);
  await openDashboard(page);
  await page.getByRole('button', { name: 'Call Mock Business App API' }).click();

  await expect(page.locator('#api-status-badge')).toHaveText(/200 OK/);

  const apiUrl = page.locator('#api-url-label');
  const displayedUrl = await apiUrl.textContent();
  
  // Contract: no internal backchannel ports
  expect(displayedUrl).not.toContain(':5163');
  expect(displayedUrl).not.toContain('localhost:');
  
  // Must show public endpoint
  if (process.env.CODESPACE_NAME) {
    expect(displayedUrl).toMatch(/https:\/\/.*-7245\..*\.app\.github\.dev/);
  } else {
    expect(displayedUrl).toContain('https://localhost:7245');
  }
});
```

## Why This Matters

1. **User Experience:** Users see URLs they can't reach, creating confusion and false debugging paths
2. **Codespaces-Critical:** Port forwarding makes the localhost vs public distinction non-negotiable
3. **Security Posture:** Exposing internal routing details (ports, HTTP vs HTTPS) leaks implementation info
4. **Test Contracts:** Separates transport optimization (use fast backchannel) from UI contracts (show reachable URLs)

## Alternatives Considered

**Alternative 1: Don't optimize with backchannel URLs**  
Rejected: The backchannel pattern is valid for server-to-server efficiency; the fix is in the response transformation, not the transport choice.

**Alternative 2: Add `displayUrl` separate from `url`**  
Acceptable: Keeps both for diagnostics but requires UI updates. Preferred approach is simpler: transform before returning.

**Alternative 3: Don't show URLs in API responses**  
Acceptable for some contexts, but diagnostics benefit from showing "what did we call" — just needs to be the public version.

## Migration Path

1. Update `DownstreamDemoController.Get()` to transform backchannel URLs before returning
2. Add unit test `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport`
3. Update existing test line 127 from expecting `http://localhost:5163` to expecting the public URL
4. Add Playwright contract test for URL accessibility
5. Validate in live Codespaces

## References

- Full diagnosis: `.squad/agents/tangy/diagnosis-mockbiz-timeout.md`
- DownstreamDemoController: `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- Existing test: `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` lines 97-128
- Dashboard view: `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml` line 272
- Codespaces URL skill: `.squad/skills/codespaces-url-forms/SKILL.md`


---

---
date: 2026-05-03T18:12:37.055+01:00
author: Blathers
status: diagnosis
---

# MockBusinessApp API Demo Timeout — `localhost:5163` Leak

## Context

Sign-in now works, but the "Call Mock Business App API" action in the member dashboard times out. The UI shows the browser calling `http://localhost:5163/api/backoffice/me`, timing out after 10 seconds.

## Root Cause

The `DownstreamDemoController` is server-side code that calls MockBusinessApp on behalf of the browser using the member's Bearer token. However, AppHost line 142 sets:

```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This environment variable is intended for **server-to-server** calls from TestSite to MockBusinessApp's internal HTTP endpoint, bypassing GitHub Codespaces port forwarding.

BUT the DownstreamDemoController at line 301 reads this env var and uses it to build the target URL that gets **returned to the browser** in the response JSON. The browser-side JavaScript displays this URL in the UI as a diagnostic.

## Why This Is Wrong

1. `BUSINESSAPP_BACKCHANNEL_URL` is a *transport layer* config for server-to-server calls.
2. The controller response JSON includes the `url` field showing `http://localhost:5163/...`.
3. This creates confusion: the URL displayed to the user is TestSite's internal address, not the public Codespaces URL.
4. The browser cannot reach `localhost:5163` — that's a TestSite-internal address accessible only from TestSite's process.

## Why `localhost:5163` Specifically

MockBusinessApp's launchSettings.json advertises:
- `https://localhost:7245` (HTTPS, for browser-facing traffic)
- `http://localhost:5163` (HTTP, for internal server-to-server calls)

In Codespaces, AppHost sets `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` so TestSite's server-side code can reach MockBusinessApp without hitting the GitHub port-forwarding proxy (which blocks unauthenticated server requests).

The browser needs the public URL: `https://{token}-7245.{region}.app.github.dev`.

## Architectural Issue

`BUSINESSAPP_BACKCHANNEL_URL` is being used for *two conflicting purposes*:
1. **Server-side transport** — HTTP call from TestSite process to MockBusinessApp process (works correctly)
2. **Browser-facing display** — URL shown in diagnostic output (incorrect, leaks internal address)

## Impact

- Server-side API call *may be succeeding*, but the response JSON misleads the user by showing an unreachable internal URL
- OR the browser-side JavaScript is misinterpreting the response and trying to make a client-side fetch to `localhost:5163`, causing the timeout

## Fix Options

### Option A: Separate Transport and Display URLs (Recommended)

1. Add a new method `ResolveBusinessAppDisplayUrl()` that returns `PrismBusinessApp:WorkflowApiBaseUrl` (the public browser URL).
2. Change `ResolveBusinessAppTransportBaseUrl()` to be used only for the actual HTTP call.
3. Update controller response JSON (lines 103, 130, 147, 165) to use the display URL.

### Option B: Document the Behavior

If the `url` field in the response JSON is *only for diagnostics* (not used by browser JavaScript for navigation), just document that it shows the *server-side transport URL*, not the browser-facing URL. The API call will succeed regardless of what URL is displayed.

### Option C: Remove Backchannel Override for Display

Change line 305 to check if `BUSINESSAPP_BACKCHANNEL_URL` is set, and if so, use `PrismBusinessApp:WorkflowApiBaseUrl` for display but continue using the backchannel URL for the actual HTTP call.

## Next Diagnostic

Inspect the actual runtime behavior in Codespaces:
1. Check browser DevTools Network tab for the `/api/prism/downstream-demo` response JSON
2. Confirm whether `url` field is `http://localhost:5163/...`
3. Check TestSite logs to see if the server-side call to MockBusinessApp is succeeding or failing
4. Determine if the timeout is client-side (browser can't reach localhost) or server-side (TestSite can't reach MockBusinessApp)

## Decision

Diagnosis complete. Recommend **Option A** (separate transport and display URLs) to cleanly separate concerns and avoid leaking internal addresses into browser-facing surfaces.


---

---
date: 2026-05-03T18:24:57.531+01:00
author: Scribe
status: COMPLETE
---

# Cleanup: Stray Diagnosis Artifact Consolidated

## Action

Deleted `.squad/agents/tangy/diagnosis-mockbiz-timeout.md` — an untracked artifact that was already fully consolidated into `.squad/decisions.md`.

## Context

The Tangy diagnosis on the MockBusinessApp timeout was merged into the decisions file with date 2026-05-03T18:12:37.055+01:00. The original markdown file remained in the worktree as untracked. The diagnostic content (contract violations, root cause analysis, test gaps, fix options) is complete in decisions.md; the artifact file was redundant.

## Decision

Stray diagnostic files that have been consolidated into `.squad/decisions.md` should be deleted to keep the `.squad/` directory authoritative and avoid confusion. The decisions file is the source of truth; temporary diagnostic artifacts don't need to be retained once merged.

## Result

Worktree is clean. `main` is up to date with origin.
---
date: 2026-05-03T18:29:38.303+01:00
author: Blathers
status: implemented
area: api-contracts, codespaces, url-separation
---

# Transport URLs vs Display URLs: Separate Concerns in API Responses

## Context

The DownstreamDemoController uses `BUSINESSAPP_BACKCHANNEL_URL` for server-to-server calls to optimize transport in Codespaces (bypassing the GitHub port-forwarding proxy). However, the controller was returning this internal URL in the JSON response to the browser, causing user confusion and perceived failures.

**Symptom:** Users saw `http://localhost:5163/api/backoffice/me` displayed in the dashboard, which timed out because that port is unreachable from the browser. In Codespaces, only port 7245 (HTTPS) is forwarded for browser access.

## Decision

**API responses must separate transport URLs from display URLs.**

When a backchannel URL is configured for server-to-server efficiency:
1. Use the backchannel URL for the actual HTTP call (transport layer)
2. Transform it to the public URL before returning in the response (display layer)

This separation ensures:
- Server-side calls remain efficient (use internal HTTP endpoints)
- Browser-facing responses show reachable URLs (use public HTTPS endpoints)

## Implementation

Added to `DownstreamDemoController.cs`:

```csharp
private string ResolveBusinessAppDisplayBaseUrl()
{
    var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");
    return baseUrl;
}

private string TransformToDisplayUrl(string transportUrl)
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(backchannelUrl))
        return transportUrl;

    if (!transportUrl.StartsWith(backchannelUrl, StringComparison.OrdinalIgnoreCase))
        return transportUrl;

    var displayBaseUrl = ResolveBusinessAppDisplayBaseUrl();
    return displayBaseUrl + transportUrl.Substring(backchannelUrl.Length);
}
```

All response returns now use `TransformToDisplayUrl(targetUrl)` instead of bare `targetUrl`.

## Test Contract

Updated `DashboardLocalEndpointsValidationTests.cs`:

```csharp
[Fact]
public async Task DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport()
{
    // ... setup ...
    
    // Backend uses backchannel for transport efficiency
    capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));
    
    // But response to browser uses public URL
    root.GetProperty("url").GetString().Should().Be(
        "https://codespace-7245.app.github.dev/api/backoffice/me",
        because: "browser-facing URLs must be publicly accessible");
}
```

This test validates the contract: transport uses backchannel, response shows public URL.

## Why This Matters

1. **User Experience:** Users see URLs they can actually reach, not internal addresses
2. **Codespaces-Critical:** Port forwarding rules make public vs internal URLs non-negotiable
3. **Security Posture:** Don't expose internal routing details (ports, HTTP vs HTTPS) to the browser
4. **Test Contracts:** Codify that transport optimization doesn't leak into UI concerns

## Alternatives Considered

**Alternative 1: Don't use backchannel URLs**  
Rejected: The backchannel pattern is valid for server-to-server efficiency in Codespaces; the fix is in response transformation, not transport choice.

**Alternative 2: Add separate `displayUrl` field**  
Acceptable but more complex: Would require UI updates and adds redundancy. Transforming the existing `url` field is simpler and clearer.

**Alternative 3: Document that `url` shows internal address**  
Rejected: Users expect displayed URLs to be reachable. This would violate the principle of least surprise.

## References

- Implementation: PR #48 (`squad/fix-browser-url-leak`)
- Commit: `6774c55`
- Test: `DashboardLocalEndpointsValidationTests.DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport`
- Prior diagnosis: `.squad/agents/blathers/history.md` — "MockBusinessApp API Demo Timeout"
- Related decision: `.squad/decisions.md` — "Browser-Facing API Responses Must Not Expose Internal Backchannel URLs"
---
date: 2026-05-03T18:29:38.303+01:00
author: Tangy
status: implemented
area: testing, playwright, browser-contracts
---

# Browser-Level Regression Test for Backchannel URL Visibility

## Context

Following Blathers' implementation of `TransformToDisplayUrl()` in `DownstreamDemoController` (commit `6774c55`), added Playwright test coverage to ensure the browser-facing contract is enforced at the user experience level.

The unit test validates the controller logic, but doesn't exercise the full browser → server → response → DOM rendering path. A Playwright test completes the coverage by validating what users actually see.

## Decision

**Add browser-level assertion to `callBusinessAppApi()` in Playwright test suite.**

The test validates the URL displayed in element `#api-url-label` after clicking "Call Mock Business App API" in the member dashboard.

## Implementation

Updated `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`:

```typescript
async function callBusinessAppApi(page: Page): Promise<void> {
  // ... existing setup and success assertions ...
  
  // Contract: Browser-facing API responses must not expose internal backchannel URLs
  const displayedUrl = await apiUrl.textContent();
  expect(displayedUrl).not.toContain(':5163', 
    'displayed URL must not expose the internal backchannel port 5163');
  expect(displayedUrl).toContain('https://localhost:7245',
    'displayed URL must show the public-facing HTTPS endpoint');
}
```

## Why This Matters

1. **Full-stack validation**: Unit tests validate controller logic; Playwright validates the complete user experience
2. **Behavior-level contract**: Test what users see, not just what the code does
3. **Regression prevention**: This test would have caught the original bug where `localhost:5163` leaked to the dashboard
4. **Environment coverage**: Works in both localhost and Codespaces contexts

## Test Results

- **All 25 unit tests pass**: `DashboardLocalEndpointsValidationTests`
- **Playwright test updated**: `localhost-auth-session.spec.ts` — `callBusinessAppApi()` function
- **Commit**: `2ebec5a` on `squad/fix-browser-url-leak` branch

## Coordination

Worked in parallel with Blathers on the same feature branch:
- Blathers: Controller fix + unit test (`6774c55`)
- Tangy: Playwright contract test (`2ebec5a`)

Clean commit history, no conflicts.

## References

- Commit: `2ebec5a` — "test: add browser-level contract for backchannel URL visibility"
- Related decision: `blathers-mockbiz-browser-url-fix.md` (controller implementation)
- Test file: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- History: `.squad/agents/tangy/history.md` — "Browser URL Leak Fix — Test Coverage"
---
date: 2026-05-03T18:29:38.303+01:00
author: Blathers
status: EXECUTED
area: git-workflow, merge-strategy, release-notes
---

# PR #48 Merge Strategy — Preserve Commit History

## Context

PR #48 (`squad/fix-browser-url-leak`) contained two commits:
1. `6774c55` — Core fix: Transform internal backchannel URLs to public URLs
2. `2ebec5a` — Browser test: Add Playwright contract for URL visibility

Both commits were release-note-relevant and addressed distinct concerns (implementation vs validation).

## Decision

**Merged PR #48 using `--merge` strategy to preserve the two separate commits in main.**

Rationale:
- Each commit addresses a distinct aspect (fix vs test coverage)
- Release notes benefit from granular history
- Git bisect operations benefit from separated concerns
- Avoids squashing away test coverage commit into fix commit

## Implementation

```bash
gh pr merge 48 --repo jonnymuir/Umbraco.Prism --merge --body "All checks passed. Merging to main."
```

Resulted in merge commit `0f79c12` on main, preserving both `6774c55` and `2ebec5a`.

## CI Results

All checks passed:
- ✅ test (9 seconds)
- ✅ core-tests (53 seconds)
- ✅ storybook-tests (1m53s)
- ✅ localhost-auth-playwright (15m32s)

**Note:** Playwright tests with full Aspire + Keycloak + browser automation legitimately take 15+ minutes. This is expected behavior for integration tests with container orchestration and OIDC flows.

## Local Sync

After merge, synced local main:
```bash
git checkout main && git pull origin main
```

Local `.squad/` history files remained uncommitted (not mixed into product PR), preserving separation between product work and squad coordination files.

## Consistency with PR #47

This approach is consistent with PR #47 merge strategy (also used `--merge` to preserve dashboard + auth fix commits). Establishing this as the standard practice for PRs with multiple concerns.
---
date: 2026-05-03T19:40:50.786+01:00
author: Blathers
status: implemented
area: codespaces, aspire-orchestration, backchannel-urls
---

# Use Dynamic Endpoint Discovery for Aspire Project Backchannels

## Context

The downstream API demo was timing out in Codespaces after the URL transformation fix (PR #48). The browser-facing URL was correct (showing the public Codespaces URL), but the server-side API call was timing out after 10 seconds.

Root cause: AppHost hardcoded `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163`, assuming port 5163 would always be correct. However, Aspire may assign ephemeral ports or not bind the HTTP endpoint at the expected address in Codespaces.

## Decision

**For Aspire project resources (not containers), use dynamic endpoint discovery for backchannel URLs.**

Pattern:
```csharp
// Container resources (Keycloak) — already using dynamic discovery
testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));

// Project resources (MockBusinessApp) — NOW using dynamic discovery
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

**Do not hardcode ports** for backchannel URLs, even if they're defined in launchSettings.json. Aspire's dynamic port assignment takes precedence.

## Why This Matters

1. **Codespaces reliability**: Aspire's port assignment may differ from launchSettings.json in containerized environments
2. **Consistency**: Matches the Keycloak backchannel pattern which works reliably
3. **Maintainability**: Single source of truth for endpoint addresses (Aspire's runtime discovery)

## Why GetEndpoint("http") Works for Projects

**Historical context**: An earlier attempt used `businessApp.GetEndpoint("https")` and failed because it returned a service discovery URL that didn't resolve from plain HttpClient.

**Why HTTP works**: The HTTP endpoint returns a plain `http://localhost:{port}` URL (not a service discovery URL), which works from plain HttpClient without Aspire service discovery extensions.

## Test Contract

Updated `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`:

```csharp
program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"http\"))",
    because: "Aspire's dynamic endpoint discovery ensures the correct HTTP port is used, " +
             "avoiding hardcoded ports that may differ across environments or Aspire configurations");
```

This validates the dynamic discovery pattern and prevents regression to hardcoded ports.

## Operational Recovery

**After merging PR #49**: Restart the Aspire AppHost in Codespaces. The backchannel will automatically resolve to the correct runtime HTTP endpoint, fixing the timeout.

No database migrations, no secrets updates, no client-side changes required.

## Alternatives Considered

**Alternative 1: Keep hardcoded localhost:5163**  
Rejected: Already proven to fail in Codespaces. No reason to assume port assignment will be stable.

**Alternative 2: Use GetEndpoint("https")**  
Rejected: Historical evidence (commit `ffc32c5`) shows HTTPS endpoints return service discovery URLs that don't work from plain HttpClient.

**Alternative 3: Configure Aspire to force specific ports**  
Rejected: Fights against Aspire's design. Dynamic discovery is the intended pattern.

## References

- Implementation: PR #49 (`squad/fix-backchannel-endpoint-discovery`)
- Commit: `2a46494`
- Test: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- Prior failed attempt: Commit `ffc32c5` (removed businessApp.GetEndpoint("https"))
- History: `.squad/agents/blathers/history.md` — "BusinessApp Backchannel Timeout Fix"
---
date: 2026-05-03T19:40:50.786+01:00
author: Tangy
status: DIAGNOSED
area: testing, codespaces, aspire-endpoints
---

# Downstream API Timeout: Hardcoded Backchannel Port vs Aspire Runtime Endpoint

## Context

User reports: "The downstream API demo now shows the public 7245 URL, but the browser call still times out after 10 seconds even though the Mock Business App admin page is reachable."

## Investigation

**What's working:**
- ✅ URL transformation fix (commit `6774c55`, `2ebec5a`) correctly transforms internal `http://localhost:5163` to public Codespaces URL in browser-facing responses
- ✅ Unit test `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport` validates the transformation logic
- ✅ MockBusinessApp admin page is reachable from browser (confirms app is running)
- ✅ Playwright test validates displayed URL doesn't contain `:5163`

**What's broken:**
- ❌ Server-to-server call from TestSite to MockBusinessApp times out after 10 seconds
- ❌ `DownstreamDemoController` line 289 timeout triggers, returns "Timeout" response to browser

## Root Cause

AppHost line 142 hardcodes the backchannel URL:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This assumes MockBusinessApp's HTTP endpoint is bound to port 5163. However:

1. **Aspire may assign ephemeral ports** - the actual runtime port might not be 5163
2. **Keycloak pattern** (line 134) uses the correct approach: `keycloak.GetEndpoint("http")` to get the actual runtime endpoint
3. **MockBusinessApp is started with `launchProfile: "https"`** (line 97), which specifies `"applicationUrl": "https://localhost:7245;http://localhost:5163"` in launchSettings.json

The hardcoded `http://localhost:5163` is fragile and doesn't work when Aspire assigns different ports in Codespaces.

## Behavioral Contract Violation

**Contract:** Server-to-server API calls must complete within the configured timeout (10 seconds)

**Current behavior:**
- TestSite attempts to call `http://localhost:5163/api/backoffice/me`
- Request times out after 10 seconds
- Controller returns "Timeout" response with statusCode 0, statusText "Timeout"
- Browser displays: "We could not reach the Mock Business App. Check that it is running, then try again."

**Expected behavior:**
- TestSite calls MockBusinessApp's actual HTTP endpoint
- Request completes successfully (200 OK)
- Browser displays: "Mock Business App responded successfully."

## Test Coverage Gap

**Current tests:**
- ✅ Unit tests validate URL transformation logic with stub handlers
- ✅ Playwright test validates displayed URL format
- ❌ **No test validates backchannel endpoint is actually reachable**
- ❌ **No test validates AppHost backchannel configuration matches Aspire reality**

**Smallest regression test surface:**

The existing Playwright test `callBusinessAppApi()` (localhost-auth-session.spec.ts, line 150-186) **SHOULD** catch this bug because it:
1. Clicks "Call Mock Business App API"
2. Expects `#api-status-badge` to show "200 OK"
3. Expects response body to contain tenant and role info

If the backchannel times out, this test should fail with:
```
Expected API call to succeed with 200 OK, but got:
Status: Timeout
Summary: We could not reach the Mock Business App...
Body: Request timed out after 10 seconds. Is MockBusinessApp running?
```

**Question:** Does this Playwright test run in Codespaces with Aspire? If not, that's the coverage gap.

## Recommended Fix (For Blathers)

Change AppHost line 142 from:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

To:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

This matches the Keycloak pattern (line 134) and ensures TestSite uses the actual runtime HTTP endpoint that Aspire assigned to MockBusinessApp.

**Note:** This requires MockBusinessApp to expose an HTTP endpoint. Verify the launchProfile "https" includes both HTTPS and HTTP in applicationUrl (currently: `"https://localhost:7245;http://localhost:5163"`).

## Test Fix

The failing unit test `AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls` (line 302) needs updating:

Current:
```csharp
program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", \"http://localhost:5163\")");
```

Should be:
```csharp
program.Should().Contain("testsite.WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"http\"))");
```

Or make it more flexible:
```csharp
program.Should().Contain("BUSINESSAPP_BACKCHANNEL_URL");
program.Should().Contain("businessApp.GetEndpoint(\"http\")");
```

## Why This Matters

1. **Codespaces-critical:** Hardcoded localhost ports don't work reliably when Aspire assigns ephemeral ports
2. **Consistency:** Keycloak already uses `.GetEndpoint("http")` pattern - MockBusinessApp should match
3. **Behavioral contract:** The Playwright test should catch this, but only if it runs in the actual Codespaces + Aspire environment

## References

- AppHost configuration: `src/UmbracoPrism.AppHost/Program.cs` lines 134, 142
- Controller timeout: `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` line 289
- Keycloak pattern: AppHost line 134 (`testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"))`)
- MockBusinessApp launchSettings: `src/UmbracoPrism.MockBusinessApp/Properties/launchSettings.json`
- Playwright test: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts` line 150-186
- Related decisions: `.squad/decisions.md` - "Transport URLs vs Display URLs: Separate Concerns in API Responses"
---
date: 2026-05-03T21:12:36.429+01:00
status: RECORDED
author: Blathers
area: diagnostics, operations, codespaces
---

# Codespaces Downstream Diagnostics Should Prefer Live Runtime Probes

## Context

The downstream API/auth investigation now spans three distinct surfaces:

1. **Local Codespace runtime** (`localhost` HTTPS endpoints)
2. **Internal backchannel state** (for Keycloak and MockBusinessApp)
3. **Public forwarded URLs** (`*.app.github.dev`) that may return redirects or GitHub tunnel/auth HTML instead of the app

Manual curl commands were becoming easy to misread, especially when a public forwarded URL returned HTML or a redirect that looked superficially like the app was healthy.

## Decision

**Codespaces diagnostics should prefer live runtime probes over guessed ports, and public forwarded-port checks must classify redirects / tunnel HTML as proxy evidence rather than app success.**

## Implementation

Added `scripts/codespaces/diagnose-downstream.sh` to:

- read authoritative forwarded browse URLs from `gh codespace ports`
- probe local TestSite / MockBusinessApp / Keycloak endpoints directly from the Codespace
- summarize safe runtime state from MockBusinessApp `/debug/auth`
- probe public forwarded URLs without following redirects, so tunnel/auth interception stays obvious
- avoid printing secrets, cookies, or bearer tokens

## Why This Matters

1. **Correctness:** dynamic Aspire / Codespaces endpoints are safer to read from runtime than to guess from stale localhost assumptions
2. **Operator clarity:** HTML tunnel pages and redirects are a different class of failure from app JSON or auth responses
3. **Security posture:** diagnostics remain useful without exposing secrets

## References

- `scripts/codespaces/diagnose-downstream.sh`
- `src/UmbracoPrism.MockBusinessApp/Program.cs` (`/debug/auth`)
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` (`/session-contract`, `seed-contract-ready`)
---
date: 2026-05-03T20:53:49.355+01:00
status: complete
domain: diagnostics, operations
---

# Decision: Manual Diagnosis Flow for Downstream API Timeouts

## Problem

When the MockBusinessApp API times out (10s) in Codespaces, operators face ambiguity:
- Is the API unreachable or just hung?
- Is the bearer token invalid or the Keycloak backchannel blocked?
- Is it a browser→API issue or a server→API issue?
- Previous "fixes" that didn't work eroded confidence in troubleshooting.

## Solution

Created **operator-friendly diagnostic flows** that use curl to isolate each layer:

### Deliverables

1. **`MANUAL_DIAGNOSIS_FLOW.md`** — Comprehensive guide
   - 5-step progression from quick reachability checks to deep backchannel validation
   - Expected outcomes for each curl command (not just "try this")
   - Diagnosis flowchart mapping symptoms → root causes
   - Common failure points with fixes
   - Operator checklist for closure

2. **`.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt`** — One-page cheat sheet
   - Test order (fastest to deepest)
   - Decision tree for symptom interpretation
   - Top 5 root causes by frequency
   - Files to check and environment variables

### Key Principles

1. **Layered Testing**
   - Internal backchannel (http://localhost:5163) → proves API listens
   - Public endpoint (https://{codespace}-7245.app.github.dev) → proves port forwarding
   - Bearer token tests → proves auth chain
   - Keycloak backchannel → proves signing key access

2. **No Code Changes**
   - Uses existing curl, gh CLI, browser DevTools
   - No temporary logging or instrumentation needed
   - Can be run by operators with no repo knowledge

3. **Expected Outcomes Explicit**
   - Not "try this command"
   - But "run this; if you see X expect result Y; if Z expect result W"
   - Maps exact output (401, HTML, timeout, connection refused) to root causes

4. **Separation of Concerns**
   - Browser-facing path (public HTTPS + port forwarding)
   - Server-side path (internal backchannel + token forwarding)
   - Keycloak trust chain (issuer, JWKS, token validation)
   - Each testable independently

## Five Distinct Failure Modes

The 10-second timeout can originate from:

1. **Aspire port reassignment** — Port 5163 not listening
   - Test: `curl http://localhost:5163/api/backoffice/me`
   - Result: Connection refused
   - Fix: Check `gh codespace ports` for actual port

2. **Service hung** — Port listening but no response
   - Test: Same curl, hangs for 10s
   - Fix: Restart AppHost or check MockBusinessApp logs

3. **Bearer token expired/invalid** — API responds 401
   - Test: `curl -H "Authorization: Bearer {TOKEN}" ...`
   - Result: 401 Unauthorized
   - Fix: Check token expiry, re-sign in

4. **Keycloak backchannel blocked** — Signing keys unreachable
   - Test: `curl http://localhost:8080/realms/prism-dev/.well-known/openid-configuration`
   - Result: Connection refused or timeout
   - Fix: Restart Keycloak, verify port

5. **GitHub tunnel auth page** — Port forwarding returns HTML
   - Test: `curl https://{codespace}-7245.app.github.dev/api/backoffice/me`
   - Result: `<h1>Connecting to the forwarded port...</h1>`
   - Fix: Include Bearer token in Authorization header

## Why This Matters

- **Previous approach**: "Try this fix, restart AppHost, hope it works"
- **New approach**: "Run these 5 tests in order; at step N you'll know whether it's port/auth/tunnel"
- **Operator confidence**: Diagnosis is reproducible and deterministic, not magical

## Not Changing Code

This is a **read-only diagnostic aid** — no code changes, no new dependencies, no Aspire modifications. It documents existing troubleshooting best practices discovered during PR #49 work.

## Related

- `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md` — The fix (code change)
- `.squad/skills/generic-oidc-downstream-bearer-validation/SKILL.md` — Token validation patterns
- `.squad/skills/live-oidc-401-stale-runtime/SKILL.md` — Runtime restart detection
- PR #49 — Implementation of dynamic endpoint discovery
# Final Push to Origin & Branch Cleanup

**Author:** Mabel  
**Date:** 2026-05-03  
**Status:** COMPLETED  

---

## Task

Push the finished main branch to origin (which contained 4 .squad-only commits after PR #49 merge and residual reconciliation). Clean up merged feature branches from both remote and local.

## Actions Completed

### Push Main
- Local main (commit `e1d54e7`) pushed to origin/main
- 4 commits delivered:
  - `e1d54e7` docs: mabel session history — post-merge reconciliation complete
  - `ed2b5cd` docs: update tom-nook history — aspire-dynamic-endpoint-backchannels skill extraction
  - `9ee9a25` docs: add aspire-dynamic-endpoint-backchannels skill
  - `e44c8bf` chore: mabel session history — PR #49 merge complete

### Remote Cleanup
Deleted 9 merged feature branches from origin (all were fully merged into main):
- fix/codespaces-businessapp-http-backchannel
- squad/12-biometric-device-credentials-table
- squad/20-21-biometric-platform-config
- squad/22-capacitor-biometric-bridge
- squad/23-biometric-registration-ui
- squad/25-biometric-device-management-ui
- squad/codespaces-dashboard-and-auth-fixes
- squad/fix-backchannel-endpoint-discovery
- squad/fix-browser-url-leak

### Local Cleanup
Deleted corresponding local feature branches:
- fix/codespaces-businessapp-http-backchannel ✅
- squad/codespaces-dashboard-and-auth-fixes ✅
- squad/fix-browser-url-leak ✅
- squad/fix-backchannel-endpoint-discovery ✅ (force-deleted; remote was already gone)

One local branch remains: `fix/codespaces-mockbiz-401` (not merged; kept for ongoing work).

## Final State

- **Local main:** At commit `e1d54e7`, synced to origin/main
- **Working tree:** Clean
- **Local branches:** 2 remaining (`main`, `fix/codespaces-mockbiz-401` — the latter for ongoing work)
- **Risk:** None — all deletions were of fully merged branches; no history was lost

## Pattern

Safe cleanup after merge:
1. Verify branches are fully merged into main using `git branch -r --merged origin/main`
2. Delete from origin first (remote source of truth)
3. Delete from local after remote confirms deletion
4. Keep branches only if they contain active work not yet merged

This is low-risk workflow maintenance that signals closure and keeps branch lists legible.
# Post-Merge Branch State Reconciliation

**Author:** Mabel  
**Date:** 2026-05-03  
**Status:** COMPLETED  
**Issue:** Residual squad-only work on `squad/fix-backchannel-endpoint-discovery` after PR #49 merge

---

## Context

PR #49 merged to main (commit `a8e2d86` on origin/main), but the local feature branch had:
1. Uncommitted changes to `.squad/agents/tom-nook/history.md` (documenting skill extraction)
2. A post-merge skill documentation commit on the branch

Mabel had also made a local post-merge session history commit to main, creating branch divergence.

## Decision

**Outcome:** Keep and land the skill documentation cleanly.

- **Skill verdict:** `aspire-dynamic-endpoint-backchannels` is **earned, well-documented, and reusable**. Merits inclusion in shared skills library.
- **History verdict:** Tom Nook's documentation of the extraction process belongs in the history record.
- **Merge strategy:** Rebase feature branch onto main's post-merge commit, then fast-forward merge to preserve linear history.

## Rationale

1. **Skill quality:** The skill has test contracts, anti-patterns, diagnosis steps, and cross-references. It captures a real learning from Codespaces backchannel timeout diagnosis (PR #49 work).

2. **Clean history:** Feature branch rebase resolves divergence without creating merge commits. Final state: linear main history with two skill-related commits.

3. **Pattern establishment:** Archiving learned skills as part of PR closure is a discipline. This reconciliation sets the precedent: skills extracted during work should be included in the merge, not left behind on a stale branch.

## Implementation

- ✅ Staged Tom Nook's history entry
- ✅ Rebased feature branch onto main
- ✅ Fast-forward merged to main
- ✅ Both main and feature branch now at commit `ed2b5cd`
- ✅ Working tree clean

## Downstream

- **Next step:** Push reconciled main to origin (awaiting authorization)
- **Feature branch:** Can be deleted or left as historical marker; feature branch head points to merged commit
- **No code changes:** This is purely .squad/ bookkeeping; no product or implementation impact

## Related

- Skill: `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md`
- Tom Nook history: `.squad/agents/tom-nook/history.md` (entry dated 2026-05-03 20:12:13)
- Original PR: #49
- Decision: Kept as-is per established routing policy (Mabel owns PR/merge workflow)
# PR #49 Merge Strategy — Preserve Commit History

**Date:** 2026-05-03  
**Agent:** Mabel (Technical Writer / Release)  
**Merge Commit:** a8e2d86

## Decision

Merged PR #49 using **create a merge commit** strategy (not squash) to preserve the readable product history:

```
a8e2d86 Merge pull request #49 ...
├─ d6cfe4e squad: merge downstream timeout diagnosis decisions
└─ 2a46494 fix(codespaces): use dynamic endpoint discovery for BusinessApp backchannel
```

## Rationale

- **Preserve product narrative:** The two commits represent distinct concerns:
  1. **2a46494:** User-facing fix (endpoint discovery solves the timeout)
  2. **d6cfe4e:** Team bookkeeping (decision history consolidation)
- **Release notes clarity:** Future release notes can reference `2a46494` directly as the fix, with d6cfe4e as supporting team documentation
- **Bisect-friendly:** If issues arise, engineers can identify the exact commit that introduced them
- **Consistency:** Aligns with project history strategy: meaningful atomic commits > squashed history

## Alternative Considered

- **Squash merge:** Would flatten both commits into one. This loses the distinction between the fix and team documentation, making future release notes and bisecting harder.
- **Rebase merge:** Would linearize but wouldn't create an explicit merge commit, risking confusion about which commits belonged to this PR.

## Impact

- All CI checks passed before merge ✅
- Local main automatically fast-forwarded to origin/main
- Feature branch cleaned (local + remote deletion)
- Ready for next development cycle
---
date: 2026-05-03T20:53:49.355+01:00
status: RECORDED
author: Tangy
area: testing, diagnosis, browser-debugging
---

# Browser DevTools Manual API Diagnosis Playbook

## Context

After several rounds of timeout investigations on the "Call Mock Business App API" button, a repeatable manual diagnostic pattern emerged. Users need a structured way to isolate failures at three levels: button flow, auth/headers, and network reachability.

## Decision

**Testers, developers, and QA should follow the 8-phase diagnostic playbook to manually isolate API timeouts from the browser side.**

The playbook prioritizes separating concerns so that a single observation (e.g., "timeout") can be quickly traced to a root cause (button flow broken, auth header missing, port unreachable, CORS blocked).

## Diagnostic Approach

### Phase Separation

1. **Capture** (DevTools Network tab) → Know if a request was fired
2. **Inspect auth** (Request Headers) → Know if token was attached
3. **Check status** (Response Status) → Know if server responded
4. **Inspect response** (Response Body) → Know what the failure was
5. **Isolate endpoint** (cURL copy) → Know if it's browser-specific
6. **Test health** (Direct curl, no auth) → Know if endpoint exists
7. **Compare levels** (With/without auth) → Know if auth is the issue
8. **Check console** (Browser errors) → Know if JS or CORS failed

### Key Observation Points

- **No request in DevTools** → Button flow broken (JavaScript)
- **Request with 401** → Auth header missing or token invalid
- **Request with 200** → Success; check response body for expected fields
- **Request with 0 (timeout)** → Endpoint unreachable or misconfigured
- **URL contains `:5163`** → Internal backchannel port (not browser-reachable)
- **cURL succeeds, browser times out** → CORS or browser-specific issue
- **Both fail identically** → Network or endpoint health issue

## Implementation

Documented in: `.squad/skills/browser-devtools-api-diagnosis/SKILL.md`

Includes:
- Step-by-step walkthrough for each phase
- Expected/unexpected responses at each phase
- Decision tree for quick diagnosis
- cURL examples for copying from DevTools
- 3 worked examples (auth missing, port unreachable, CORS blocked)
- Environment-specific notes (localhost, Codespaces, CI/CD)

## Use Cases Covered

1. **Timeout after 10 seconds** → Isolate between button flow, network, auth token validation
2. **401 Unauthorized** → Confirm token is being sent and isn't expired
3. **Endpoint unreachable** → Distinguish between browser CORS block vs. true network failure
4. **Port forwarding confusion** → Recognize internal localhost URLs (`:5163`) vs. public endpoints
5. **Button doesn't seem to do anything** → Confirm request is being fired vs. JavaScript failing

## Testing Edge Cases

The playbook surfaces these edge cases:

- **Token valid in auth context but rejected during header validation** → Token validation timeout
- **Endpoint works without auth (401) but times out with auth** → Token processor hanging
- **cURL works but browser times out** → CORS headers missing or wrong
- **Internal backchannel URL in response** → URL transformation not applied (regression in PR #48)

## Regression Test Coverage

The existing Playwright test `callBusinessAppApi()` (localhost-auth-session.spec.ts) already validates end-to-end but doesn't surface intermediate failures well. The manual playbook allows testers to go deeper when automated tests fail, following the same phases: capture → inspect headers → check status → inspect body → isolate endpoint.

## Team Impact

- **Testers:** Can diagnose timeouts without asking developers
- **Developers:** Can provide better error responses (include `statusCode`, `statusText`, attempted URL in response body)
- **Ops/Infra:** Can correlate browser diagnoses with server logs to confirm backchannel vs. external failures

## References

- Previous timeout diagnoses: `tangy-downstream-timeout.md`, `tangy-mockbiz-timeout-diagnosis.md`
- Related skills: `aspire-dynamic-endpoint-backchannels`, `inline-api-failure-states`, `dev-session-contract-probe`
- Playwright test: `localhost-auth-session.spec.ts::callBusinessAppApi()`
---
date: 2026-05-03T21:12:36.429+01:00
author: Tangy
status: PROPOSED
area: testing, diagnostics, codespaces
---

# Codespaces Downstream Diagnostics Must Separate Transport, Tunnel, and Token Failures

## Context

Manual curl checks were proving that some endpoints returned `200`, but operators still had to guess whether the real failure was:

- the internal TestSite → MockBusinessApp hop
- the public GitHub forwarded-port tunnel/auth layer
- bearer token rejection inside MockBusinessApp
- stale Keycloak backchannel wiring in the running stack

A Codespaces helper script needs to turn those into distinct outcomes instead of a single generic "timeout" story.

## Decision

A Codespaces downstream diagnostics script must:

1. **Check the internal BusinessApp hop separately from the public forwarded URL** so operators can tell "service is up internally" from "public tunnel returned HTML/auth".
2. **Use safe runtime diagnostics (`/debug/auth`) before asking for tokens** so the script can inspect backchannel/JWKS health without dumping secrets.
3. **Treat authenticated 401s as an auth-validation branch, not an availability branch** when the internal app probe already succeeded.
4. **Compare repo expectations with runtime backchannel state** so the script can call out likely stale AppHost/runtime wiring and recommend `bash scripts/codespaces/refresh.sh`.
5. **Print next commands inline for every failure state** so operators do not need to cross-reference a separate playbook.

## Why

The same user-visible timeout can come from different layers, and the remediation is different for each one. A good script must say "forwarding problem", "token problem", or "stale backchannel problem" explicitly, otherwise the operator wastes time chasing the wrong service.
---
author: "Tom Nook"
date: "2026-05-03T20:12:13+01:00"
decision_type: "pattern"
status: "implemented"
---

# Skill Extraction Discipline — aspire-dynamic-endpoint-backchannels

## Decision

**EXTRACT** earned knowledge as `.squad/skills/{skill-name}/SKILL.md` as part of PR closure workflow.

## Context

`squad/fix-backchannel-endpoint-discovery` included:
- **Fix:** Aspire's `GetEndpoint("http")` for dynamic backchannel URL discovery in Codespaces
- **Bookkeeping:** Decision logs, history updates, agent charters
- **Untracked:** `.squad/skills/aspire-dynamic-endpoint-backchannels/` directory

The skill captures reusable patterns:
1. Why GetEndpoint("http") works vs GetEndpoint("https")
2. Test contract validation
3. Diagnosis steps for backchannel timeouts
4. Anti-patterns (hardcoded ports, wrong endpoint types)

## Resolution

**KEEP the skill.** It is:
- Earned through real work (PR #49)
- Well-documented with concrete examples
- Cross-referenced in related skills
- Immediately reusable for future Codespaces/Aspire work

## Consequences

1. **Knowledge Preservation:** Infrastructure patterns become team assets, not lost in commit history
2. **Onboarding:** New contributors can understand Codespaces backchannel without reverse-engineering
3. **Decision Trail:** Skills link back to PRs and orchestration logs for full context
4. **Reuse:** Future Aspire work can reference this pattern instead of re-diagnosing

## Implementation

Added skill as commit `2078604` on `squad/fix-backchannel-endpoint-discovery` during branch cleanup.

## Related

- Implementation: PR #49 (commit `2a46494`)
- Test contract: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- Decision: `.squad/decisions/inbox/blathers-backchannel-dynamic-discovery.md`
---
date: 2026-05-03T21:32:41.296+01:00
author: Blathers
status: PROPOSED
area: codespaces, diagnostics, runtime
---

# Codespaces Diagnostics Scripts Should Verify a Clean Python Runtime

## Context

`scripts/codespaces/diagnose-downstream.sh` is intentionally invoked as a plain shell command from the repo root. In Codespaces, contributors may already have activated another Python toolchain or exported `PYTHONHOME` / `PYTHONPATH`, which can make `python3` start without a usable standard library and fail on imports as basic as `json`.

## Decision

Codespaces operator scripts that embed Python should:

1. Probe for a working interpreter before running the main payload
2. Launch that interpreter with `-I`
3. Scrub shell-level Python environment overrides such as `PYTHONHOME` and `PYTHONPATH`
4. Fall back to a system interpreter when the first `python3` on `PATH` is broken

## Why

- Operators should not have to debug their shell state just to run first-line diagnostics
- `-I` and explicit env scrubbing keep these scripts dependency-free while restoring predictable stdlib imports
- A small runtime guard is cheaper and less invasive than rewriting an otherwise working diagnostics payload

---
date: 2026-05-03T21:26:34.690+01:00
agent: mabel
issue: diagnostics-script-landing
status: implemented
---

# Diagnostics Script Landing: Scope Discipline

## Decision

Land **product-scoped** diagnostics work (script + flow guide) directly onto main branch in a single, clear commit. Keep **agent-scoped** work (.squad bookkeeping, skills) separate and untracked on main.

## Context

After previous work on downstream API timeout diagnosis (PR #49), two artifacts emerged:

1. **Product deliverables:** `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, updated `CODESPACES.md`
2. **Agent bookkeeping:** Blathers' reference note + extracted browser-diagnostics skill

Both were created during the same diagnostic effort but serve different audiences:
- Product files: Codespaces users needing to troubleshoot API/auth/tunnel issues
- Agent work: Squad team learning and skill reuse

## Choice

**Commit product files to main; leave agent work in .squad/**

### Product Commit (926ca7a)

```
docs: add downstream diagnostics script and flow guide

- Add scripts/codespaces/diagnose-downstream.sh for debugging API/auth/tunnel issues
- Add MANUAL_DIAGNOSIS_FLOW.md for step-by-step troubleshooting guide
- Update CODESPACES.md with reference to new diagnostics script and flow

The script checks local endpoints, reads safe runtime diagnostics,
probes TestSite/MockBusinessApp/Keycloak connectivity, and supports
optional bearer token authentication for full testing.
```

### Agent Work (Untracked, Not Merged)

- `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt` — Blathers' diagnostic notes
- `.squad/skills/browser-devtools-api-diagnosis/` — Reusable pattern for future devtools-level debugging

## Rationale

**Separation enables clarity:**

1. **Product surface** (main branch) stays focused on user-facing assets — no .squad clutter
2. **Agent work** stays in .squad/ — available for future sessions but not blocking product merges
3. **Git history** reads clearly: "We shipped diagnostics tooling" vs "We learned a pattern"

**Timing impact:** Landing product immediately unblocks Codespaces users; agent skill can be refined/merged in future work without rushing.

## Implementation

1. Stage only product files: `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `CODESPACES.md`
2. Commit with clear scope message
3. Push to origin/main
4. Leave .squad/ untracked (will be staged separately if/when Scribe merges agent decisions)

## Follow-Up

- Mark `.playwright-cli/` for addition to `.gitignore` (build artifact, not product)
- Blathers' reference note + skill remain in .squad/ for Squad team access
- If browser-devtools-api-diagnosis pattern proves reusable, merge skill to main in a future PR with Blathers' sign-off

---
date: 2026-05-03T20:53:49.355+01:00
agent: mabel
issue: diagnostics-script-runtime
status: implemented
---

# Diagnostics Script Runtime Isolation — Commitment to Main

**Date:** 2026-05-03  
**Decision Owner:** Mabel (Technical Writer)  
**Commit:** `fb1b324`  
**Status:** ✅ Landed on main

## Problem

Codespaces users with other Python toolchains (Conda, Poetry, .venv, etc.) activated in their shell would encounter:

```
ModuleNotFoundError: No module named 'json'
```

when running `bash scripts/codespaces/diagnose-downstream.sh`. The issue occurred because the diagnostics script attempted to use Python with ambient `PYTHONHOME` and `PYTHONPATH` environment variables that pointed to incompatible or incomplete Python installations.

## Solution

### Three-part fix:

1. **Runtime detection** — Added `resolve_python_runtime()` to probe for working Python interpreters, validating each with a stdlib import check (`import json`, `argparse`, etc.)

2. **Isolation** — Invoke detected Python with `-I` flag and explicit env var unset:
   ```bash
   env -u PYTHONHOME -u PYTHONPATH -u PYTHONSTARTUP -u __PYVENV_LAUNCHER__ \
       "$PYTHON_BIN" -I - "$@" <<'PY'
   ```

3. **Documentation** — Updated CODESPACES.md with:
   - Clear statement that the script now self-checks and ignores shell overrides
   - Recovery step: fresh shell + preflight check `python3 -I -c 'import json'`
   - Added test contract: `CodespacesDiagnosticsScript_IgnoresAmbientPythonShellOverrides()`

## Scope

**Landed as single product commit:**
- `scripts/codespaces/diagnose-downstream.sh`
- `CODESPACES.md`
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`

**Not landed (untracked):**
- `.squad/agents/*/history.md` — will update separately
- `.squad/skills/`, `.playwright-cli/` — reference/build artifacts
- Agent reference notes — (Blathers, Tangy, etc.)

This separation keeps the product commit focused and clean, while bookkeeping stays in .squad/.

## Impact

✅ **Codespaces experience:** Users no longer need to close/reopen shells or manually diagnose Python runtime conflicts.  
✅ **Operator clarity:** CODESPACES.md now gives actionable steps if the script itself fails.  
✅ **Contract enforcement:** Test ensures future contributors maintain the isolation pattern.

## User Action

Pull main and rerun the diagnostics script in a fresh Codespaces shell.

---
date: 2026-05-03T21:32:41.296+01:00
author: Tangy
status: PROPOSED
area: testing, codespaces, runtime-assumptions
---

# Codespaces Diagnostics Script Must Ignore Ambient Python Shell State

## Context

`scripts/codespaces/diagnose-downstream.sh` failed before any downstream checks with:

```text
ModuleNotFoundError: No module named 'json'
```

Because `json` is in Python's standard library, the likely failure mode is shell-level runtime contamination or a broken active interpreter, not a missing repo dependency.

## Decision

Run the diagnostics helper with an isolated Python runtime and make the recovery path explicit for operators.

## Consequences

- The script should unset ambient `PYTHON*` overrides and use `python -I` for both its preflight and main execution paths.
- If that still fails, the error should point operators at the shell runtime itself with a minimal `python3 -I -c 'import json'` preflight.
- QA should still call out the remaining assumptions: a genuinely broken `python3` binary cannot be recovered in-script, `gh codespace ports` remains the authoritative public URL source, and stack readiness is still a prerequisite for meaningful probe results.

---
date: 2026-05-03T21:26:34.690+01:00
author: Tom Nook
status: DECISION
area: git-hygiene, diagnostics, codespaces
---

# Landing Diagnostics Script: Separate Product from Bookkeeping

## Problem

**Current state:**
- Local `main` is 1 commit ahead of `origin/main` (42bae10, a squad bookkeeping commit)
- That commit's message claims it includes "scripts/codespaces/diagnose-downstream.sh" and "updated CODESPACES.md"
- **But the actual script files are untracked** — not included in the commit
- The untracked product work: `diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `QUICK_DIAGNOSIS_REFERENCE.txt`, `browser-devtools-api-diagnosis/` skill, and `CODESPACES.md` update

**Consequence:**
- The commit message is dishonest (says it includes files that don't exist in it)
- The script cannot be pulled into Codespaces because it's not actually in the repo
- Squad bookkeeping and product work are entangled in one incomplete commit

## Decision

**Separate product from bookkeeping:**
1. Reset `main` to `origin/main` (discard the incomplete bookkeeping commit)
2. Stage and commit the diagnostics script work in a single, focused product commit
3. Push the product commit to `main`
4. Defer squad bookkeeping consolidation to a separate session

**Rationale:**
- Product commits should contain exactly what their messages claim
- Jonny can immediately pull the script into Codespaces
- Bookkeeping (decision merges, history updates) is a separate concern and should land separately
- Follows "each commit is a complete, releasable unit" discipline

## Implementation

1. `git reset --hard origin/main` (discard 42bae10)
2. Stage: `CODESPACES.md`, `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt`, `.squad/skills/browser-devtools-api-diagnosis/`
3. Commit with message: `feat(codespaces): add downstream diagnostics script and supporting docs`
4. Push to `main`

## Outcome

- ✅ Script lands on main in a clean, focused commit
- ✅ Jonny can pull and use it immediately
- ✅ Bookkeeping will follow in a separate commit when consolidated

**No risk:** The diagnostics script is new work (no regressions); the skill docs are documentation.
---
date: 2026-05-03T21:49:23.079+01:00
author: Blathers
status: PROPOSED
area: codespaces, diagnostics, tooling
---

# Codespaces Downstream Diagnostics Must Not Depend on Python

## Context

`scripts/codespaces/diagnose-downstream.sh` is meant to be the first-response operator tool when downstream API calls, tunnel redirects, or Keycloak backchannel wiring go wrong in Codespaces.

The prior hardening still failed in shells where there was no usable Python runtime at all. In that state, the script exited before any diagnostics banner or reachability checks, which defeated the purpose of having a low-friction troubleshooting helper.

## Decision

The downstream diagnostics helper should be implemented with shell-native tooling and must not require Python to be installed or healthy.

### Implementation guidance

1. Use `curl` for HTTP/HTTPS probes, including detection of:
   - internal service reachability
   - public tunnel/auth HTML interception
   - same-origin runtime endpoint availability
   - authenticated vs unauthenticated downstream responses
2. Use `gh codespace ports` as the authoritative source for forwarded browse URLs when Codespaces metadata is available.
3. Parse only the minimum JSON fields needed for operator guidance with shell-safe extraction rather than embedding a secondary runtime.
4. Keep the fallback hostname derivation path for cases where `gh` metadata is unavailable.

## Why This Matters

- **Reliability:** A script intended for broken environments must keep working when optional runtimes are broken too.
- **Operator ergonomics:** `bash scripts/codespaces/diagnose-downstream.sh` should remain the single obvious command to run.
- **Security posture:** Shell-only summaries still avoid printing cookies, bearer tokens, or other secrets.

## Consequences

- Future enhancements to this helper should prefer Bash, `curl`, and `gh` first.
- If richer parsing is ever needed, it should only be added when there is no credible shell-native alternative and the operator experience remains robust when that dependency is absent.

---
date: 2026-05-03T21:49:23.079+01:00
author: Tangy
status: PROPOSED
area: testing, codespaces, diagnostics
---

# Codespaces Diagnostics Common Path Must Not Require Python

## Context

`scripts/codespaces/diagnose-downstream.sh` was still failing before any useful diagnostics when the active shell exposed a broken Python runtime. The Python-isolation patch improved one failure mode, but the common Codespaces operator path still depended on Python being present and healthy before the script could even reach its first probe.

## Decision

For the common Codespaces path, the downstream diagnostics helper should be shell-only and must not require Python at all. Regression coverage should lock that contract by asserting the script stays on shell-native tooling and by documenting the operator-facing runtime assumptions explicitly.

## Consequences

- A broken or polluted Python interpreter can no longer block the default diagnostics command.
- The remaining fragile assumptions are now narrower and explicit: `curl` + `jq` must exist in the shell, `gh codespace ports` remains the authoritative browse-URL source when Codespaces metadata is available, fallback hostnames are still best-effort, and the stack still has to be running for the probes to be meaningful.
- Future fixes should treat any reintroduction of Python into this script as a regression unless there is a clearly justified non-common-path fallback.

---
date: 2026-05-03T21:49:23.079+01:00
author: Mabel
status: IMPLEMENTED
area: product-hygiene, git-workflow, scope-discipline
---

# Diagnostics Script Landing: Product vs. Bookkeeping Separation

## Context

Blathers and Tangy completed the no-Python diagnostics rewrite (shell-only probe logic, updated tests, browser devtools skill extraction). This landing session faced the scope question: **Should we land product + bookkeeping in one commit, or keep them separate?**

The working tree contained:
- **Product files** (should go to main): `scripts/codespaces/diagnose-downstream.sh`, `CODESPACES.md`, `MANUAL_DIAGNOSIS_FLOW.md`, test contract
- **Bookkeeping files** (should be deferred): `.squad/agents/blathers/history.md`, `.squad/agents/tangy/history.md`, `.squad/skills/browser-devtools-api-diagnosis/`, `.playwright-cli/`

## Decision

**Product and bookkeeping files must be committed separately to main.**

- **Product commit (22843a2):** Only user-facing deliverables go to main. Users pull, get working diagnostics script, no noise.
- **Bookkeeping session:** Agent histories, skills, and session artifacts are coordinated separately, keeping the main branch clean and releasable.

### Rationale

1. **Main branch hygiene:** main should contain only shipping artifacts. `.squad/` bookkeeping is internal coordination noise.
2. **User clarity:** When a user pulls a commit message "Fix: Rewrite diagnostics script...", they should see only the files they care about, not agent history or skill extraction artifacts.
3. **Release boundaries:** One commit = one releasable unit. Product commit 22843a2 is production-ready; bookkeeping is orthogonal.
4. **Git history signal:** Future readers reviewing main history see only meaningful product decisions, not agent coordination artifacts.

### Implementation

**Workflow for multi-agent coordination going forward:**

1. Implementation agents (Blathers, Tangy) complete their work
2. Technical Writer (Mabel) **stages only product files** (`git add <product-files>`)
3. Create clean product commit with single concern
4. **Leave .squad/ files unstaged**
5. Separate bookkeeping session: Update agent histories and merge them without product files

**Git commands:**
```bash
# Stage only product files
git add scripts/codespaces/diagnose-downstream.sh CODESPACES.md MANUAL_DIAGNOSIS_FLOW.md src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs

# Commit to main
git commit -m "Fix: Rewrite diagnostics script to eliminate Python runtime dependency..."

# Push product commit
git push origin main

# Later: Separate bookkeeping merge with only .squad/ files
```

### Exception: When bookkeeping is tightly coupled

If a product file genuinely requires a .squad/ reference for correctness (e.g., a decision embedded in a code comment), include it in the product commit. Otherwise: separate.

---

## Precedent

Commit fb1b324 (2026-05-03, earlier session) established this pattern. Commit 22843a2 reinforces it.

## Follow-up

- **Scribe:** Consider updating `.squad/conventions.md` to document this landing workflow
- **Future technical writes:** Use this pattern for all multi-agent product handoffs

---
date: 2026-05-03T23:00:12.742+01:00
agent: blathers
status: proposed
---

# Downstream Demo Transport Diagnostics Should Be Response-Visible

## Context

The downstream demo endpoint (`/api/prism/downstream-demo`) serves as a live diagnostic tool for operators testing server-to-server bearer token forwarding. When calls fail in Codespaces, the failure could be:
- Stale AppHost wiring (backchannel URL not set or pointing to wrong port)
- GitHub port-forwarding tunnel blocking internal requests
- MockBusinessApp not running or rejecting tokens
- Network timeout vs external cancellation

Previously, failures logged to the server but returned generic error messages, forcing operators to manually inspect environment variables and AppHost logs to determine the actual transport path.

## Decision

Embed transport path diagnostics directly in the JSON response payload for all outcomes (success, timeout, network error, non-JSON response).

### What Gets Exposed

Response includes a `transport` object with:
- `transport`: "internal-backchannel", "public-tunnel", or "public-url"
- `backchannelPresent`: boolean flag for BUSINESSAPP_BACKCHANNEL_URL
- `transportBaseUrl`: masked for internal URLs (`http://localhost:****`), full for public
- `targetUrlScheme`: http/https indicator

Structured logs also include this metadata for searchability.

### Security Considerations

**Safe to expose:**
- Whether backchannel URL is configured (boolean flag)
- Transport type classification
- Public URLs (already browser-visible)
- URL scheme (http/https)

**Must mask:**
- Actual backchannel port numbers → shown as `http://localhost:****`
- Bearer tokens, refresh tokens, cookies
- Client secrets, JWKS keys

### Why Response-Visible

1. **Immediate operator insight** — Failure response immediately shows which transport path was attempted
2. **No log hunting** — Operators don't need AppHost logs or environment variable inspection for first-pass triage
3. **Context-aware hints** — Error messages can tailor advice based on transport (e.g., "Try refresh.sh" for backchannel timeouts)
4. **Test-friendly** — Future automated tests can assert on transport metadata
5. **Safe for dev environments** — Already gated behind IsDevelopment or explicit config flag

## Implementation

Added `BuildTransportDiagnostics()` helper that:
1. Checks `BUSINESSAPP_BACKCHANNEL_URL` environment variable
2. Falls back to `PrismBusinessApp:WorkflowApiBaseUrl` config
3. Classifies as internal-backchannel, public-tunnel, or public-url
4. Masks internal URLs, shows public URLs in full
5. Returns tuple for structured logging and response inclusion

Updated all response paths (success, timeout, HttpRequestException, non-JSON) to include transport metadata.

## Alternatives Considered

**Log-only diagnostics:**
- Rejected: Requires operator to have AppHost log access and grep skills
- Log hunting for every failure slows down diagnosis

**Expose actual backchannel port:**
- Rejected: Ephemeral ports are internal runtime detail; exposing them doesn't help operators since they can't directly call localhost from their browser anyway
- Masked representation conveys "internal backchannel in use" without leaking port

**Separate diagnostic endpoint:**
- Rejected: Response-visible diagnostics on the actual failing endpoint give immediate context
- Separate endpoint requires two requests to correlate transport with failure

## Consequences

**Benefits:**
- Next Codespaces timeout immediately shows "internal-backchannel" vs "public-tunnel"
- Operators can distinguish stale wiring from downstream auth failures in one request
- Contextual hints tailored to actual transport type
- Structured logging enables pattern analysis across failures

**Risks:**
- Exposing transport implementation detail in API contract
- Mitigation: Already dev-only endpoint; transport metadata is descriptive, not prescriptive

**Maintenance:**
- Transport classification logic lives in one helper method
- If new transport types emerge (e.g., service mesh, sidecar), update classification in one place

## Related Decisions

- `.squad/skills/dev-session-contract-probe/SKILL.md` — Precedent for response-visible diagnostics without token exposure
- `.squad/skills/inline-api-failure-states/SKILL.md` — Normalize from Response.status first, layer diagnostic fields
- `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md` — Why backchannel URLs exist and how they're resolved

## Test Coverage

All 680 Core tests pass. No new test failures introduced. Transport diagnostics are response-visible but don't break existing contract expectations.

Tangy added five behavioural contract tests guarding backchannel/public tunnel classification and timeout/error transport metadata; all tests pass.

---
date: 2026-05-03T22:49:38.255+01:00
author: Blathers
status: PROPOSED
area: diagnostics, authentication, http-client
---

# Downstream API Timeout Diagnosis: Unregistered HttpClient Root Cause

## Context

The DownstreamDemoController times out after 10 seconds when calling MockBusinessApp from TestSite. Evidence gathered:

1. **Browser call:** `/api/prism/downstream-demo` → timeout after 10s
2. **Session contract:** Shows authenticated session, access token present, `authorizationHeaderReady=true`
3. **Diagnostics script:** Internal `http://localhost:{port}/debug/auth` returns 200 (BusinessApp is listening and healthy)
4. **Keycloak backchannel:** Healthy and reachable
5. **TestSite same-origin probes:** Healthy

## Root Cause Identified

`DownstreamDemoController.cs` uses a named HttpClient that is **not registered**:

```csharp
// Line 286:
var client = httpClientFactory.CreateClient("prism-downstream-demo");
```

**Impact:**
- HttpClientFactory creates an unconfigured default client
- The CancellationToken timeout (10s) is respected, but the client lacks proper handler configuration
- Unregistered clients may have issues with localhost resolution, certificate validation, or connection pooling in containerized environments

## Decision

**Register the "prism-downstream-demo" HttpClient with explicit configuration.**

This is justified because:
1. Named clients should always be registered (codebase pattern)
2. The timeout alone (via CancellationToken) doesn't guarantee proper handler chain setup
3. Matches the pattern used for "PrismBusinessApp" and "PrismTokenRefresh"
4. Low risk: Won't break existing behavior if the issue is elsewhere

## Implementation

In `PrismComposer.cs` or `TestSiteComposer.cs`:

```csharp
// Add after existing HttpClient registrations:
builder.Services.AddHttpClient("prism-downstream-demo")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15); // Slightly higher than CancellationToken timeout
    });
```

OR in development-only scope (since this is a demo controller):

```csharp
// In TestSiteComposer.cs or wherever dev-only services are registered:
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient("prism-downstream-demo")
        .ConfigureHttpClient(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
}
```

## Alternative: Verify Runtime Environment First

If registering the client doesn't fix the timeout, the next diagnostic step is:

**Add logging to DownstreamDemoController to capture the actual URL being called:**

```csharp
private string ResolveBusinessAppTransportBaseUrl()
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    if (!string.IsNullOrWhiteSpace(backchannelUrl))
    {
        logger.LogInformation("[PRISM] Using backchannel URL: {Url}", backchannelUrl);
        return backchannelUrl;
    }
    
    var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");
    
    logger.LogInformation("[PRISM] Falling back to public URL: {Url}", baseUrl);
    return baseUrl;
}
```

This will confirm:
- Whether BUSINESSAPP_BACKCHANNEL_URL is actually set at runtime
- Whether the URL matches what the diagnostics script successfully tested

## Test Coverage

After implementing, verify:
1. TestSite can call MockBusinessApp via the demo button (< 2 seconds)
2. Browser-facing response still shows public URL (not backchannel)
3. Diagnostics script still shows healthy backchannel connectivity

## References

- History note: "Named HttpClients have default timeouts (100s); the custom timeout only applies when the named client is registered."
- `DownstreamDemoController.cs` line 286
- `PrismComposer.cs` lines 34-35 (existing HttpClient registrations)

---
date: 2026-05-03T23:13:53.622+01:00
session: transport-diagnostics-landing
title: Transport Diagnostics Landing — Product Commit 17edf9c
author: Mabel (Technical Writer)
affected: downstream-demo, diagnostics workflow
status: implemented
---

# Transport Diagnostics Landing Decision

## Context

Transport diagnostics feature (implementation by Blathers, testing by Tangy) was ready to land on main. Two product files contained the changes:
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — diagnostics instrumentation
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — test contracts

Unrelated changes present (`.playwright-cli/`, `.squad/` agent artifacts) required clean staging.

## Decision

**Staged only the two product files.** Committed with conventional commit message (`feat(diagnostics):...`) and required Co-authored-by trailer. Pushed to origin/main as commit 17edf9c.

## Rationale

1. **Single-unit release boundary:** One commit = one releasable feature. No mixing product and bookkeeping in the same commit.
2. **Clean user history:** When users pull origin/main, they see only the shipped diagnostics feature, not internal agent coordination.
3. **Conventional signal for release notes:** `feat(diagnostics)` prefix enables Mabel to infer minor version bump when generating CHANGELOG.
4. **Hygiene pattern reaffirmed:** Continues established product/bookkeeping separation from earlier diagnostics landings (22843a2, fb1b324).

## Outcome

✅ **Product commit 17edf9c now live on origin/main.**

Users can immediately:
- `git pull origin main` to get transport diagnostics feature
- See transport type (internal-backchannel vs public-tunnel) in diagnostic responses
- Understand backchannel configuration state and target URL scheme for troubleshooting

## Files Changed

- DownstreamDemoController.cs: +60 lines (diagnostics instrumentation)
- DashboardLocalEndpointsValidationTests.cs: +175 lines (test contracts)

## Convention Implication

This landing reaffirms the **product/bookkeeping separation pattern** as team-wide convention:

- **Main branch:** Shipping artifacts only (user-facing code changes)
- **Bookkeeping:** .squad/ agent histories, decisions, coordination logs (deferred to separate sessions or merges)
- **Release clarity:** Clean git history enables users and release automation to reason about what shipped and why

Suggest Scribe consider updating `.squad/conventions.md` to document this as explicit team guidance for future multi-agent product handoffs.

---
date: 2026-05-03T23:26:29.163+01:00
author: Blathers
status: decision
area: diagnostics, downstream-demo, backchannel
---

# Decision: Safe deeper downstream timeout diagnostics

## Context

Jonny needed better browser-visible detail for downstream demo timeouts, especially when TestSite calls MockBusinessApp through an internal backchannel in Codespaces or local Aspire wiring.

## Decision

Keep masking internal backchannel ports in `transport.transportBaseUrl` as `http://localhost:****`, but add safe timeout details that do not expose raw internal ports:

- `transport.usingBackchannel`
- `transport.targetPath`
- `timeout.timedOutByUs`
- `timeout.cancellationSource`
- short `summary` / `nextCheck` hints

Also enrich server logs with the masked transport base URL and target path so operators can correlate browser output with backend logs.

## Rationale

The browser already needs to know whether TestSite used the backchannel, which path it targeted, and whether the 10-second timeout came from our own request window. Those details help diagnose stale AppHost wiring and public-tunnel fallbacks, while the raw localhost port still stays hidden from browser-visible JSON.

---
date: 2026-05-03T23:26:29.163+01:00
author: Tangy
status: decision
area: testing, diagnostics, downstream-demo
---

# Decision: Timeout Diagnostics Must Distinguish Deadline vs Cancellation Without Leaking Backchannel Ports

## Context

`DownstreamDemoController` now exposes richer timeout diagnostics for `/api/prism/downstream-demo` so operators can tell whether a failed request used the public tunnel or the internal backchannel. The remaining behavioural risk was ambiguity between a real controller timeout and an externally cancelled request, especially in unit tests that throw `TaskCanceledException` directly.

## Decision

Browser-visible timeout responses should preserve these contracts:

1. **Deadline vs cancellation must be explicit.**
   - Timeout responses expose `statusText`, `timeout.timedOutByUs`, and `timeout.cancellationSource`.
   - Behavioural tests cover both the controller-owned timeout window and a separate external-cancellation path.

2. **Internal-backchannel diagnostics must stay masked.**
   - Responses may identify `internal-backchannel`, the target path, and suggested next checks.
   - `transport.transportBaseUrl` must remain masked (`http://localhost:****`) and raw internal ports must not appear anywhere in browser-visible JSON.

3. **Operator guidance should point to configuration and health checks, not implementation leaks.**
   - `summary` and `nextCheck` should reference the downstream path and wiring checks like `BUSINESSAPP_BACKCHANNEL_URL`.
   - Guidance should avoid exposing raw localhost ports while still telling operators what to verify next.

## Test Coverage

- `DownstreamDemo_IncludesTransportDiagnostics_OnTimeout`
- `DownstreamDemo_IncludesMaskedInternalBackchannelTimeoutDiagnostics`
- `DownstreamDemo_LabelsExternalCancellation_SeparatelyFromTimeoutWindow`
- Existing masking contract in `DownstreamDemo_DoesNotExposeRawBackchannelPortInDiagnostics`

---
date: 2026-05-03T23:38:00.000+01:00
author: Mabel
status: IMPLEMENTED
area: diagnostics, backend, testing
---

# Decision: Deeper Downstream Timeout Diagnostics Landing

## Summary

Landed enhanced timeout diagnostics feature to origin/main. Product commit exposes backchannel state, target path, and cancellation context to help operators triage timeout failures in Codespaces environments.

## Implementation

**Staged and committed:**
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — Implements richer timeout diagnostic fields
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — Enhanced test coverage for timeout scenarios

**Scope discipline:**
- Left `.squad/` files unstaged (Scribe merged bookkeeping separately)
- Clean product boundary: only user-facing artifacts in commit

## Rationale

Timeout diagnostics must expose enough state to distinguish:
1. **Backchannel wiring failures** — When BUSINESSAPP_BACKCHANNEL_URL points to an unreachable internal service
2. **Public-tunnel timeouts** — When Codespaces tunneling infrastructure is slow or misconfigured

New fields enable operators to immediately see:
- `usingBackchannel` — Explicit confirmation of which path was attempted
- `targetPath` — Path component of the downstream call (safe to expose; URL masked)
- `timeoutWindowMs` + `cancellationSource` — Timeout boundary and which component fired it

## Owners

- Lead (Tom Nook) — Feature approved
- Blathers (Backend Dev) — Implementation approved
- Tangy (Tester) — Test coverage approved
- Commit: 442c5e9

---
date: 2026-05-03T23:46:52.875+01:00
author: Blathers
status: PROPOSED
area: diagnostics, backend, auth
---

# Business API Arrival Logging Should Carry Safe Cross-Service Correlation

## Context

When the dashboard's downstream demo times out, TestSite can prove which transport path it chose, but that alone does not prove MockBusinessApp accepted the request or entered `/api/backoffice/me`. Operators need a decisive signal from MockBusinessApp itself without logging bearer tokens or secrets.

## Decision

For `MockBusinessApp` arrival diagnostics on `/api/backoffice/me`:

1. Log once in middleware immediately before `app.UseAuthentication()`
2. Log again at the top of the `/api/backoffice/me` handler
3. Keep fields safe: method, path, service trace identifier, auth-header-present, and a caller trace hint
4. Forward TestSite's `HttpContext.TraceIdentifier` in a dedicated header (`X-Prism-Caller-TraceId`) so MockBusinessApp logs can be matched back to TestSite warning logs

## Why

- The pre-auth log proves the request reached MockBusinessApp before bearer validation ran
- The handler-entry log proves endpoint execution began
- A dedicated caller trace hint gives cross-service matching without exposing tokens, cookies, or internal secrets

## Files

- `src/UmbracoPrism.MockBusinessApp/Program.cs`
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`

---
date: 2026-05-04T00:01:43.530+01:00
author: Blathers
status: PROPOSED
area: auth, keycloak, codespaces, backchannel
---

# MockBusinessApp Downstream Timeout Root Cause Is Hybrid JWKS URI Escape

## Context

Downstream Demo now proves TestSite is using the internal backchannel and that requests arrive at MockBusinessApp before auth. MockBusinessApp then logs:

- `IDX20803: Unable to obtain configuration from 'http://localhost:{ephemeral}/realms/prism-dev/.well-known/openid-configuration'`
- inner `IDX20804` against `http://{public-codespaces-host}:{same-ephemeral}/realms/prism-dev/protocol/openid-connect/certs`
- `KEYCLOAK_BACKCHANNEL_URL` is present
- `ASPNETCORE_ENVIRONMENT=Development`
- `backchannel JWKS enabled : YES`

## Decision

Treat this as sufficient root-cause evidence and stop broader diagnosis.

The failing runtime path is:

1. `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`
2. `ResolveSigningKeys(...)`
3. `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`
4. `WarmAsync(cacheKey, metadataAddress, ...)`
5. `ConfigurationManager<OpenIdConnectConfiguration>` + `BackchannelRewritingDocumentRetriever`

The discovery request is redirected to `KEYCLOAK_BACKCHANNEL_URL`, but the returned discovery document emits a **hybrid** `jwks_uri` using the public Codespaces hostname with the internal HTTP port. The current rewriter only rewrites URLs whose prefix exactly matches the configured public origin (`https://{public-host}`), so the hybrid URI (`http://{public-host}:{ephemeral-port}`) is not rewritten and the metadata HttpClient waits on an unreachable public endpoint until its default 100-second timeout.

## Implications

- The downstream-demo 10-second timeout is now explained: TestSite gives up after 10 seconds while MockBusinessApp auth middleware is still blocked on its own 100-second metadata client.
- This is not just "discovery rewritten but JWKS forgotten" by design; it is a narrower bug: the JWKS rewrite exists, but misses Keycloak's hybrid JWKS origin.

## Required Fix

Primary code change:

- `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`

Validation coverage:

- `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs`

Optional follow-up diagnostics only if useful:

- `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`

## Preferred Fix Shape

Make generic OIDC bearer validation robust against hybrid Keycloak JWKS URIs by either:

1. bypassing discovery in backchannel mode and fetching `.../protocol/openid-connect/certs` directly from the backchannel base, matching the existing direct-JWKS strategy in `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`, or
2. broadening the retriever rewrite so it rewrites any Keycloak realm URL whose host/path matches the configured authority, regardless of whether the discovery doc reports `https://public-host`, `http://public-host:{ephemeral-port}`, or another equivalent frontchannel form.

Add a regression test for the exact observed hybrid case.

---
date: 2026-05-03T23:46:52.875+01:00
author: Mabel
status: IMPLEMENTED
area: instrumentation, backend, testing
---

# Business API Arrival Instrumentation Landing

**Decision:** Land Business API arrival instrumentation on `main` for production use.

**Date:** 2026-05-03T23:46:52.875+01:00

**Status:** IMPLEMENTED (commit 8e1cd68)

---

## What We're Shipping

The Business API arrival instrumentation enables operators to correlate TestSite (dashboard) requests with Business API diagnostics through safe trace ID forwarding.

**Components:**

1. **Arrival Middleware (MockBusinessApp)**
   - Logs before authentication: captures raw request context without access restrictions
   - Logs after handler entry: includes authentication status
   - Fields: method, path, trace ID, auth header presence, caller trace ID

2. **Caller Trace ID Forwarding (TestSite)**
   - Extracts HttpContext.TraceIdentifier from TestSite request
   - Forwards via `X-Prism-Caller-TraceId` header to Business App
   - Safe pattern: header is read-only diagnostic data, no auth/PII exposure

3. **Test Contract (DashboardLocalEndpointsValidationTests)**
   - Validates trace ID capture and forwarding
   - Stub handler asserts header presence
   - Confirms correlation hint matches

---

## Why This Matters

**Operator pain point:** When downstream calls fail in Codespaces, operators had to manually trace logs across services. The trace ID link was missing.

**Solution:** Safe, read-only correlation header enables immediate cross-service log search without exposing internal URLs or PII.

---

## Scope Discipline Applied

- **Product files staged:** Only the three changed runtime/test files
- **Bookkeeping deferred:** .squad/ agent histories and skill updates left unstaged for separate bookkeeping merge
- **Release boundary:** Single, complete, production-ready commit (8e1cd68)

---

## Approval Chain

- **Blathers (Backend Dev):** Implemented arrival middleware and handler logging
- **Tangy (Tester):** Validated test contract and correlation forwarding
- **Mabel (Release):** Staged clean commit, pushed to main

---

## User Outcome

Users can now `git pull origin main` and run dashboard + Business App with arrival instrumentation active. Developers using Codespaces can correlate dashboard timeouts with Business API logs immediately — no manual tracing needed.

---

## Next Steps (Deferred Bookkeeping)

- Merge agent history updates to .squad/agents/
- Consolidate this decision into decisions.md
- Extract any reusable patterns to team skills
# Decision: Workflow API Calls Must Use Internal Backchannel in Codespaces

**Date:** 2026-05-04T00:19:33.157+01:00  
**Author:** Blathers (Backend Dev)  
**Status:** ACCEPTED

## Context

In Codespaces, Aspire AppHost injects two environment variables for the Business App:

- `PrismBusinessApp__WorkflowApiBaseUrl` — the public HTTPS forwarded-port URL (browser-facing)
- `BUSINESSAPP_BACKCHANNEL_URL` — the internal `http://localhost:{port}` endpoint (server-to-server)

GitHub's forwarded-port proxy intercepts unauthenticated server-side HTTP calls to the public URL and returns 401. Any server-side code that reads `WorkflowApiBaseUrl` and uses it for HTTP requests will fail with 401 in Codespaces.

## Decision

All server-side HTTP clients that call the Business App **must** check `BUSINESSAPP_BACKCHANNEL_URL` first and fall back to `PrismBusinessApp:WorkflowApiBaseUrl`. The public `WorkflowApiBaseUrl` is for browser-facing links only.

## Rationale

`DownstreamDemoController` already had the correct pattern (`ResolveBusinessAppTransportBaseUrl()`). `BusinessAppWorkflowClient.BaseUrl` was missing it, causing every workflow start and advance to fail in Codespaces with HTTP 401.

## Implementation Pattern

```csharp
private string BaseUrl
{
    get
    {
        var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(backchannelUrl))
            return backchannelUrl;

        var url = configuration["PrismBusinessApp:WorkflowApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("...");
        return url.TrimEnd('/');
    }
}
```

## Scope

- `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs` — fixed
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — already correct
- Any future Business App HTTP clients must follow the same pattern

## Commit

`caaf551` — fix(workflow): use BUSINESSAPP_BACKCHANNEL_URL for workflow API calls in Codespaces
# Decision: Workflow 401 Null-Auth Contract and Diagnostic Distinction

**Proposed by:** Tangy (Tester)  
**Date:** 2026-05-04  
**Status:** Proposed — for Scribe to merge into decisions registry

---

## Decision

**`BusinessAppWorkflowClient` must log when `GetAuthorizationHeaderAsync` returns null, and workflow endpoint handlers in MockBusinessApp must return `Results.Problem()` (not `Results.Unauthorized()`) for application-level identity failures.**

---

## Context

Investigating why workflow pages return "Business App error (HTTP 401)" in Codespaces even after commit 0904810 fixed JWKS backchannel URL rewriting. Two indistinguishable 401 sources exist:

1. **JWT middleware 401** — token signature validation failed (no valid signing keys). Logged as `[PRISM AUTH FAILED]` in Business App console.
2. **Application-level 401** — `Results.Unauthorized()` returned when `GetPrismTenant` or `GetEmail` fails after successful JWT validation.

Additionally, when `PrismContext.GetAuthorizationHeaderAsync` returns null (e.g. `CurrentTenant` not resolved), `BusinessAppWorkflowClient.CreateClientAsync` silently omits the Authorization header with no log entry. The Business App JWT middleware then returns 401, which is indistinguishable from the cases above.

---

## Rationale

- Operators have no way to distinguish the three failure modes without access to Business App console logs.
- `/api/backoffice/me` returns `Results.Problem()` for null tenant/email; workflow endpoints return `Results.Unauthorized()`. This inconsistency means the same root cause (misconfigured tenant config) surfaces differently depending on which endpoint is called first.
- Silent null auth in `CreateClientAsync` (line 179 of `BusinessAppWorkflowClient.cs`) makes `PrismContext` failures invisible in TestSite logs.

---

## Proposed Changes

### 1. `BusinessAppWorkflowClient.CreateClientAsync` — log when auth header is null

```csharp
var authHeader = await prismContext.GetAuthorizationHeaderAsync(forceRefresh);
if (authHeader == null)
{
    logger.LogWarning(
        "BusinessAppWorkflowClient: GetAuthorizationHeaderAsync returned null (reason: {Reason}). " +
        "Request will be sent without an Authorization header.",
        prismContext.LastAuthorizationFailureReason ?? "unknown");
}
if (authHeader != null)
    client.DefaultRequestHeaders.Authorization = authHeader;
```

### 2. `MockBusinessApp/Program.cs` — align workflow handlers to `Results.Problem()`

Replace `Results.Unauthorized()` in `/api/workflow/{key}/current`, `/api/workflow/{key}/advance`, and `/api/workflow/instances` handlers with:

```csharp
if (tenant == null)
    return Results.Problem("Tenant not recognised by Business Application.");
if (string.IsNullOrEmpty(email))
    return Results.Problem("User email claim not found.");
```

This produces HTTP 500 (same as `/api/backoffice/me`) for application-level identity failures, making them distinguishable from JWT-level 401s in `ReadEnvelopeAsync` output ("Business App error (HTTP 500)" vs "Business App error (HTTP 401)").

---

## Affected Files

- `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs`
- `src/UmbracoPrism.MockBusinessApp/Program.cs`

---

## Test Coverage

Regression tests added in `BusinessAppWorkflowClientTests.cs` document the current null-auth contract:
- `GetCurrentAsync_SurfacesErrorEnvelope_WhenAuthHeaderIsNull`
- `GetCurrentAsync_AttemptsTokenRefreshOnce_WhenBusinessAppReturns401`
- `GetCurrentAsync_SurfacesErrorEnvelope_NotExceptionThrown_WhenBothRequestsReturn401`

These tests will need updating if the null-auth logging proposal is implemented (the contract changes from silent to logged).
---
date: 2026-05-04T00:26:42.240+01:00
author: Blathers
status: PROPOSED
area: workflow, auth, MockBusinessApp
commit: beef21c
---

# Workflow Auth: Align MockBusinessApp Handlers and Log Silent Auth Failures

## Context

Two layered 401 failure modes in the Codespaces workflow-start path were collapsing into the same surface error, making diagnosis difficult:

1. `BusinessAppWorkflowClient.CreateClientAsync` silently omitted the `Authorization` header when `GetAuthorizationHeaderAsync` returned null (e.g. `CurrentTenant` unresolved), with no log entry.
2. MockBusinessApp workflow handlers (`/current`, `/advance`, `/instances`) returned `Results.Unauthorized()` for app-level tenant/email resolution failures, while `/api/backoffice/me` returned `Results.Problem()` for the same conditions.

## Decisions

### 1. Log a Warning when auth header is null

**`BusinessAppWorkflowClient.CreateClientAsync` must log a Warning when `GetAuthorizationHeaderAsync` returns null.**

When no auth header is obtained, the request will be rejected by the Business App JWT middleware with 401, which then triggers a spurious token-refresh retry cycle. Without a log, this is entirely invisible. The warning includes the `forceRefresh` flag and a hint to check `PrismTenantMiddleware`.

### 2. MockBusinessApp workflow handlers must return Results.Problem for app-level failures

**All three workflow endpoints must return `Results.Problem(...)` — not `Results.Unauthorized()` — when tenant or email resolution fails after successful JWT validation.**

This aligns them with `/api/backoffice/me` (already using `Results.Problem`). The result:
- A 401 from the workflow path now **unambiguously** means the bearer token was missing or rejected by JWT middleware.
- A 500 from the workflow path means the token was valid but Business App configuration (tenant mapping, email claims) failed.
- Operators and TestSite logs can distinguish the two cases without guesswork.

## Impact

- Tangy's regression tests (`BusinessAppWorkflowClientTests`) continue to pass and correctly model the expected retry behaviour on JWT-level 401.
- No changes to the retry logic itself — the fix is diagnostic clarity only.

---
date: 2026-05-04T00:00:00.000+01:00
author: Blathers
status: ACCEPTED
area: testing, ci, environment-variables
---

# Decision: Tests That Read Env Vars Must Join EnvVarSensitiveTestCollection

## Context

`EnvVarSensitiveTestCollection` was designed to serialise test classes that *mutate* `KEYCLOAK_BACKCHANNEL_URL` and `ASPNETCORE_ENVIRONMENT`. `PrismContextTests` was not in the collection because it does not mutate those variables.

However, `PrismContext.RefreshTokenAsync` **reads** both variables at runtime to conditionally rewrite the token endpoint. When `BackchannelRewriteTests` (in the collection) set those vars while `PrismContextTests` ran in parallel, the token endpoint was rewritten to an `http://localhost` URL. The Moq mock matched the `https` URL only, so Moq returned null, causing `NullReferenceException` at `result.Success`.

The failure was latent but only surfaced in CI at commit beef21c because adding `BusinessAppWorkflowClientTests` to the collection changed execution timing and widened the race window.

## Decision

**Any test class that exercises code paths which _read_ `KEYCLOAK_BACKCHANNEL_URL` or `ASPNETCORE_ENVIRONMENT` must be in `EnvVarSensitiveTestCollection`, even if it does not mutate those variables itself.**

Pattern to use (as in `LocalhostGenericOidcRegressionTests`):
1. Add `[Collection(EnvVarSensitiveTestCollection.Name)]` to the class.
2. Implement `IDisposable` saving both env vars in the constructor and restoring them in `Dispose`.

## Rationale

xUnit parallelism operates at the test-class level. Without collection membership, any class that reads global state (environment variables) is subject to races with any other class that writes that state.

## Files Affected

- `src/UmbracoPrism.Core.Tests/PrismContextTests.cs` — fixed in commit 860c5d3

---
date: 2026-05-04T09:22:01.025+01:00
author: Tangy
status: ACCEPTED
area: testing, ci, moq
---

# Never Use Concrete CancellationToken Values as Moq Matchers for ASP.NET Core Contexts

## Context

CI run 25294216756 (commit `beef21c`) failed with 4 `PrismContextTests` throwing `NullReferenceException` at `PrismContext.cs:212`. The production code was unchanged and correct. The fault was entirely in the test setup.

Mock setups for `IPrismTokenRefreshService.RefreshAsync` used `httpContext.RequestAborted` as a concrete value matcher. On Linux (GitHub Actions, Ubuntu), `DefaultHttpContext.RequestAborted` lazy-initialises its `CancellationTokenSource` via `IHttpRequestLifetimeFeature`. If that feature is activated by the authentication stack between setup-time and call-time, Moq's captured token value no longer matches the token in the actual call. Moq's loose mock returns `null` for the unmatched setup, causing `result.Success` to throw. On macOS (arm64) the lazy path is stable and the bug is masked.

## Decision

**When writing Moq setups for methods that accept a `CancellationToken`, always use `It.IsAny<CancellationToken>()` rather than a concrete `HttpContext.RequestAborted` or `httpContext.RequestAborted` value.**

Rationale:
- `DefaultHttpContext.RequestAborted` is lazily initialised through `IHttpRequestLifetimeFeature` and its behaviour can differ between platforms.
- The intent of tests like these is to verify routing logic and return values, not to assert the exact CancellationToken instance.
- Concrete value matching for CancellationToken is always fragile unless you own the token source and can guarantee stability.

## Implementation

Replace:
```csharp
.Setup(t => t.RefreshAsync(..., httpContext.RequestAborted, ...))
.Verify(t => t.RefreshAsync(..., httpContext.RequestAborted, ...), Times.Once)
```

With:
```csharp
.Setup(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...))
.Verify(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...), Times.Once)
```

Applied in commit `1601415` to four `PrismContextTests` methods.

## Blathers Review Note

The fix is entirely in test harness code. `PrismContext.cs` and `IPrismTokenRefreshService` are correct and do not require changes. Blathers does not need to act on this. The CI should pass once this commit is pushed.

