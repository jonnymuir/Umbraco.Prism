# Codespaces Port-Forwarding False Positive — Test Analysis (Tangy)

**Date:** 2026-05-02T14:38+01:00  
**Status:** Analysis Complete — Test Strategy Ready  
**Symptom:** "getting a 200 now, however... text/html ... Connecting to the forwarded port..."

---

## TL;DR — The Problem

The downstream demo is now returning **HTTP 200** (not 401), but the content is **`text/html`** containing GitHub's port-forwarding placeholder page ("Connecting to the forwarded port..."), not the expected JSON API response. The current implementation treats this as success and displays the HTML in the UI.

**This is a false positive.** The code successfully made an HTTP request and got a 200 response, but it's not validating that the response is actually from the target service. It's the HTML interstitial page GitHub Codespaces shows while a port is forwarding but not yet fully established.

---

## Root Cause

`DownstreamDemoController.Get()` (lines 69-95) returns success whenever:
1. The HTTP call completes without throwing (even if it returns HTML)
2. The status code is not an exception case (timeout/network error)

There's **no content-type validation** to confirm the response is the expected `application/json`. Line 93 reports whatever Content-Type the response has, but doesn't fail on mismatches. The controller just attempts JSON pretty-printing (lines 76-85) and falls back to raw string on failure — so an HTML page gets displayed as-is.

---

## Impact

### User Experience
- **Misleading success indication:** UI shows "Status: 200 OK" with HTML body
- **No actionable guidance:** User sees port-forwarding HTML but UI doesn't explain what went wrong
- **Silent degradation:** The actual issue (port not ready) is masked as success

### Test Gap
Current `DashboardLocalEndpointsValidationTests.cs` has:
- ✅ Success case with JSON response
- ✅ Network error (HttpRequestException)
- ✅ Timeout (TaskCanceledException)
- ❌ **Missing:** HTML/wrong-content-type response handling
- ❌ **Missing:** Port-forwarding placeholder detection

---

## Required Regression Tests

### Test 1: HTML Response Detection
**Purpose:** Prove the controller detects and surfaces HTML responses as failures, not success.

```csharp
[Fact]
public async Task DownstreamDemo_ReturnsError_WhenResponseIsHtml()
{
    var handler = new StubHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<html><body>Connecting to the forwarded port...</body></html>",
                Encoding.UTF8,
                "text/html")
        });

    var controller = BuildController(
        handler,
        new Dictionary<string, string?>
        {
            ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
        },
        authHeader: new AuthenticationHeaderValue("Bearer", "token"),
        isDevelopment: true);

    var result = await controller.Get();

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    var root = doc.RootElement;

    root.GetProperty("statusCode").GetInt32().Should().Be(0);
    root.GetProperty("statusText").GetString().Should().Be("Invalid Response");
    root.GetProperty("body").GetString().Should().Contain(
        "Expected JSON but received text/html",
        because: "HTML responses from port-forwarding pages must be detected and surfaced as errors");
}
```

### Test 2: GitHub Codespaces Port-Forwarding Placeholder Detection
**Purpose:** Specifically test the "Connecting to the forwarded port" scenario.

```csharp
[Fact]
public async Task DownstreamDemo_DetectsCodespacesPortForwardingPage()
{
    var handler = new StubHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<!DOCTYPE html><html><body><h1>Connecting to forwarded port...</h1></body></html>",
                Encoding.UTF8,
                "text/html")
        });

    var controller = BuildController(
        handler,
        new Dictionary<string, string?>
        {
            ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
        },
        authHeader: new AuthenticationHeaderValue("Bearer", "token"),
        isDevelopment: true);

    var result = await controller.Get();

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    var root = doc.RootElement;

    root.GetProperty("statusCode").GetInt32().Should().Be(0);
    root.GetProperty("body").GetString().Should().Contain(
        "port not ready",
        because: "port-forwarding placeholder pages must be clearly identified");
}
```

### Test 3: Non-JSON Content-Type Rejection (Plain Text)
**Purpose:** Ensure any non-JSON content type is detected, not just HTML.

```csharp
[Fact]
public async Task DownstreamDemo_RejectsNonJsonContentType()
{
    var handler = new StubHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "Service temporarily unavailable",
                Encoding.UTF8,
                "text/plain")
        });

    var controller = BuildController(
        handler,
        new Dictionary<string, string?>
        {
            ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
        },
        authHeader: new AuthenticationHeaderValue("Bearer", "token"),
        isDevelopment: true);

    var result = await controller.Get();

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    var root = doc.RootElement;

    root.GetProperty("statusCode").GetInt32().Should().Be(0);
    root.GetProperty("statusText").GetString().Should().Be("Invalid Response");
}
```

### Test 4: Success Case Unchanged (Regression Prevention)
**Purpose:** Confirm valid JSON responses still work after adding validation.

```csharp
// This test already exists (line 27-58), but we should verify it still passes
// after adding content-type validation. No changes needed — this is the 
// "healthy" baseline we're protecting.
```

---

## Expected Code Changes

The fix should be in `DownstreamDemoController.SendDownstreamRequestAsync` or a wrapper around the response processing (lines 69-95):

1. **After receiving the response**, check `Content.Headers.ContentType?.MediaType`
2. **If not `application/json`**, return an error structure similar to the timeout/network-error cases
3. **Preserve the HTML body** in the error message for debugging
4. **Special case for "Connecting to"** in HTML: suggest waiting for port to be ready

Example guard (pseudocode):
```csharp
var contentType = response.Content.Headers.ContentType?.MediaType;
if (contentType != null && !contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
{
    var rawBody = await response.Content.ReadAsStringAsync();
    var errorHint = contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase)
        ? "The service returned an HTML page instead of JSON. In Codespaces, this usually means the forwarded port isn't ready yet — wait a few seconds and try again."
        : $"Expected JSON response but received {contentType}.";
    
    return Ok(new
    {
        statusCode = 0,
        statusText = "Invalid Response",
        url = targetUrl,
        elapsedMs = sw.ElapsedMilliseconds,
        contentType = contentType,
        body = $"{errorHint}\n\nRaw response:\n{rawBody}"
    });
}
```

---

## Security Considerations

✅ **No security regression:** This is a UX/validation hardening, not a relaxation.
✅ **Content-type checking is defense-in-depth:** Prevents accidental display of malicious HTML.
✅ **Error messages don't leak tokens:** The existing error paths already follow this pattern.
✅ **Development-only endpoint:** Already gated by `IsDevelopment()` (line 42-49).

---

## Acceptance Criteria

Before merging, all of:
1. ✅ All three new tests pass
2. ✅ Existing tests (650 passing) remain green
3. ✅ Manual validation in Codespaces: HTML response now shows clear error, not success
4. ✅ Manual validation locally: JSON response still succeeds

---

## Test File Location

Add these tests to:
```
src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs
```

Insert after the existing `DownstreamDemo_ReturnsFriendlyNetworkError_WhenBusinessAppIsUnavailable` test (line 61-85).

---

## Key Learnings for Future

1. **Always validate response content type in dev tools/demos** — especially when wrapping HTTP calls for display in UI
2. **Port-forwarding placeholders are a Codespaces-specific edge case** — but the broader pattern (checking content-type before treating as success) applies universally
3. **False positives are as dangerous as false negatives** — a misleading success blocks users from understanding the real issue

---

## Handoff to Blathers

Blathers, this is your test contract. Implement the fix that makes these three tests pass. The production code is in:
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` (lines 69-95)

The guard should:
1. Check content-type before parsing body
2. Return a clear error structure (matching the timeout/network patterns)
3. Preserve the raw response for debugging
4. Special-case the "port forwarding" HTML hint for Codespaces users

I'll review the fix and confirm the tests prevent regressions.

— Tangy
