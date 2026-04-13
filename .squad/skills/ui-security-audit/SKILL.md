---
name: "ui-security-audit"
description: "How to audit Prism browser-facing and backoffice-facing UI surfaces for practical security risk"
domain: "security"
confidence: "high"
source: "observed"
---

## Context

Use this skill when reviewing Web Components, Razor views, demo pages, mobile web shells, or Storybook stories before release. It is especially useful when the same repo contains both production UI and demo/TestSite code, because the most dangerous mistakes are often “safe in a demo, unsafe in production”.

## Patterns

### 1. Separate active production paths from dead/demo noise

- Confirm which renderer is live before raising an XSS finding.
- In this repo, workflow collection currently flows through `<prism-field>` and `PrismFieldTagHelper`, not the older `_WorkflowField.cshtml` partial.
- Demo/TestSite helpers (`prism-debug`, downstream demo APIs, UA mock widgets) need separate labeling in the report.

### 2. Inspect every HTML/URL/storage sink

- HTML sinks: `Html.Raw`, `innerHTML`, interpolated string-built markup, inline event handlers.
- URL sinks: `href`, `src`, `window.location`, `window.open`, generated mobile `startUrl`, `_blank` handling.
- Storage sinks: `localStorage`, `sessionStorage`, cookies, Capacitor `Preferences`, secure-storage bridges.

### 3. Check boundary assumptions, not just obvious XSS

- Ask whether content editors, tenant admins, IdP claims, or business-app payloads can reach the sink.
- Treat textbox-backed URLs as untrusted unless the schema enforces safe/local paths.
- Treat JS-readable persistence of auth-adjacent state as a risk for PII/financial contexts, even if tokens themselves stay in secure storage.

### 4. Record positive controls too

- Note antiforgery, local-redirect validation, allowlists, and encoded renderers.
- In this repo, workflow POSTs are protected by antiforgery validation plus local return-url enforcement.

## Examples

- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml` + `src/UmbracoPrism.Core/TagHelpers/PrismDebugTagHelper.cs`
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- `src/UmbracoPrism.Core/Middleware/PrismBrandingMiddleware.cs`
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs`
- `src/UmbracoPrism.TestSite/MobileNavSchemaSetup.cs`
- `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.ts`

## Anti-Patterns

- Treating demo helpers as harmless just because they live in `TestSite/`
- Reporting dead code as an active production vulnerability without tracing usage
- Ignoring privileged-author risks (CMS editors, tenant admins, IdP claim sources)
- Declaring a UI suitable for high-assurance PII/financial use when debug surfaces or JS-readable auth metadata still exist
