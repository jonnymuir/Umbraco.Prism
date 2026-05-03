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

