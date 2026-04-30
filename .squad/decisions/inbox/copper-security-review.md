# Decision: Security Review Conclusions — 2026-04-30

**Author:** Copper (Security Engineer)  
**Date:** 2026-04-30  
**Related:** `.squad/security-review-2026-04-30.md`

---

## Decisions Made

### 1. WorkflowPollController must require `PrismMemberCookie` auth (DECIDED + IMPLEMENTED)

Any endpoint that returns workflow instance state (step type, state version) must require authentication. `GET /api/prism/workflow/poll` has been patched with `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` at the controller level, consistent with `WorkflowHubController`. Regression test added.

### 2. `@Html.Raw(Content)` must be sanitised before the definition editor leaves Dev-only mode (DECIDED)

The current pattern of rendering workflow component `Content` fields via `@Html.Raw()` is acceptable only while the definition editor is gated to `IsDevelopment()`. Before that gate is ever removed or relaxed, a `IWorkflowContentSanitizer` abstraction using HtmlSanitizer with a GDS-aligned allowlist must be introduced. This is a **pre-condition for shipping the definition editor to non-dev environments**.

### 3. `CookieSecurePolicy.Always` is the target for `PrismMemberCookie` (DECIDED)

`SameAsRequest` is a security regression waiting to happen. The policy will be changed to `Always` before any production deployment. Local dev uses HTTPS via `dotnet dev-certs`.

### 4. Biometric IP rate-limiting requires `ForwardedHeadersMiddleware` before cloud deployment (DECIDED)

`HttpContext.Connection.RemoteIpAddress` is not proxy-aware. `ForwardedHeadersMiddleware` with `KnownProxies` must be configured before deploying to any environment behind a reverse proxy. The current in-memory single-instance design is otherwise acceptable.

### 5. Committed secrets policy (DECIDED)

No real keys, signing secrets, or Azure resource identifiers may be committed in version-controlled `appsettings.json`. The committed `HMACSecretKey` in `TestSite/appsettings.json` is considered compromised and must be rotated. Future values go to `dotnet user-secrets` (local) or environment-variable overrides (CI/CD). A secret scanning step should be added to CI.

---

## For the Scribe

Please merge into `.squad/decisions.md` under a `## Security — 2026-04-30` heading.
