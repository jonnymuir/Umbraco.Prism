---
name: "inline-api-failure-states"
description: "Handle server-rendered dashboard API failures without leaking broken UI state or console-only errors"
domain: "error-handling"
confidence: "high"
source: "earned"
---

## Context

Use this when a Razor view or other lightweight server-rendered page calls an API directly from inline JavaScript. These flows often assume a single happy-path JSON contract, but real failures can return a different payload shape, plain text, HTML, or a network exception.

## Patterns

### Normalize from the `fetch` response first

- Treat `Response.status`, `Response.statusText`, `Response.url`, and the response headers as the baseline contract.
- If the JSON body includes richer demo fields like `statusCode`, `statusText`, `elapsedMs`, or `body`, layer them on top only when they are present and valid.
- Parse `res.text()` first so you can recover gracefully from non-JSON responses.

### Separate summary from raw details

- Show a short human summary above the raw payload so users immediately know what happened.
- Use the detailed response body as supporting evidence, not the primary explanation.
- Map common failure modes to explicit copy: session expired, network error, timeout, server error.

### Keep the primary action stable

- Do not remove or repurpose the main call-to-action when the request fails.
- Keep the button mounted, disable it while loading, and restore the original label afterward.
- This makes retry obvious and avoids weird state transitions that feel like the UI is broken.

### Add live-region feedback

- Use `aria-live="polite"` for the loading state and the summary line.
- When the request completes, update the summary text so screen-reader users hear the outcome without hunting through the result panel.

## Examples

- `src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml` now normalizes `/api/prism/downstream-demo` responses before rendering the status badge.
- The same view derives fallback values from the `fetch` response when the controller returns `401 { error: ... }` instead of the demo payload shape.
- `#api-btn` stays visible, becomes disabled/busy during the request, and resets to its original label once the request completes.

## Anti-Patterns

- Assuming every response body contains `statusCode` and `statusText`.
- Rendering raw template strings from unvalidated payload fields.
- Hiding the only retry action during an error state.
- Relying on browser console output as the only failure signal.
