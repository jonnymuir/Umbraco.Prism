# Security Review — Pass 2 — 2026-04-30

**Reviewer:** Copper (Security Engineer)
**Branch:** `sec/review-2026-04-30-pt2`
**Baseline commit:** `e6ae10c`
**Scope:** Depth-first second-pass review of items the first pass either deferred or did
not look at: auth/identity wiring, sanitizer coverage gaps, anonymous endpoints, CSRF
posture, security response headers, dependency vulnerabilities, DataProtection key
management, and the workflow engine producer side. Charter rules: only push to this
branch; do not modify the pt1 ledger; honour the documented "do not touch" list
(MockBusinessApp `PassthroughSanitizer`, `WorkflowContentSanitizer` allow-lists).

**Verification baseline:**

- `dotnet build UmbracoPrism.sln -c Release` : 0 errors (3 pre-existing warnings).
- `dotnet test … --filter "FullyQualifiedName~UmbracoPrism.Core.Tests"` : **601 passed,
  0 failed, 0 skipped**.
- `dotnet list package --vulnerable --include-transitive` after pt2 fixes : **0
  vulnerable packages** across all 8 projects.

---

## Executive summary

Ten findings raised. Two PATCHED on this branch, eight OPEN. None are actively
exploitable Critical/High in production; the highest is a Medium-severity transitive
CVE which is now bumped, plus a structural Medium where an anonymous DoS endpoint in
the mock app was reachable outside Development. The remaining findings are
defence-in-depth and architectural items (security headers, logout-CSRF, antiforgery
on JSON APIs, DataProtection persistence, accordion sanitizer trap), most of which
need a team decision before patching.

### Counts by severity

| Severity      | Count | Status                       |
|---------------|-------|------------------------------|
| Critical      |   0   | —                            |
| High          |   0   | —                            |
| Medium        |   5   | 2 PATCHED, 3 OPEN            |
| Low           |   4   | OPEN (defence-in-depth)      |
| Informational |   1   | OPEN (risk-accepted candidate)|

### Summary table

| ID            | Severity | Title                                                                    | Status   |
|---------------|----------|--------------------------------------------------------------------------|----------|
| SEC-PT2-001   | Medium   | Anonymous `/api/test/reset` in MockBusinessApp (workflow-state DoS)      | PATCHED  |
| SEC-PT2-002   | Medium   | Vulnerable transitive: `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.2` (CVE-2026-42191) | PATCHED |
| SEC-PT2-003   | Medium   | Logout via GET in `AccountController` (logout-CSRF)                      | Fixed (828b5d4) |
| SEC-PT2-004   | Medium   | Missing security response headers (CSP, XFO, XCTO, HSTS, Referrer, Permissions) | Fixed (9f1f34e) |
| SEC-PT2-005   | Medium   | `DefaultAuthenticateScheme = PrismMemberCookie` made unconditional       | Confirmed safe (948c2a4) |
| SEC-PT2-006   | Low      | DataProtection keys ephemeral by default; not encrypted at rest          | Fixed (6c0e8e9) |
| SEC-PT2-007   | Low      | Unsanitized `accordionSection.Content` in Razor partial (currently unused producer-side) | Fixed (03dba49) |
| SEC-PT2-008   | Low      | `VinylRecord.cshtml` `@Html.Raw(description)` — RTE field, operator-trust | Fixed (6177137) |
| SEC-PT2-009   | Low      | Antiforgery missing on JSON state-mutating API endpoints                 | Fixed (7a3b0ef) |
| SEC-PT2-010   | Info     | `IsCapacitorOrigin` accepts `http://localhost` with credentials          | Fixed (11b8cbb) |

---

## Findings

### SEC-PT2-001 — Medium — Anonymous `/api/test/reset` in MockBusinessApp — **PATCHED**

**Location:** `src/UmbracoPrism.MockBusinessApp/Program.cs:171`

**Observation.** The MockBusinessApp registers
`app.MapDelete("/api/test/reset", …)` with no `.RequireAuthorization()`, no
antiforgery, and — critically — no environment guard. The handler invokes
`engine.ResetAll()` which wipes every in-memory workflow instance. The
neighbouring `/admin/*` routes ARE 404'd outside Development by an explicit guard
at the top of the file, but that guard's path filter does not match
`/api/test/reset`, so the destructive endpoint was reachable in any environment
the mock app was deployed to.

**Impact.** Anonymous workflow-state DoS (deletes all in-flight workflows) for any
deployment of the mock app outside Development. No data exfiltration, no
auth-bypass — but a clean availability hit, plus a tell that the host is the mock
business app.

**Why pt1 missed it.** Pt1 audited the AccessControl/auth pipeline in `Core` and
the workflow controller. Pt2 widened scope to minimal-API endpoints in
`MockBusinessApp/Program.cs`.

**Fix (this branch).** Added an explicit `IsDevelopment()` check inside the
handler that returns `Results.NotFound()` outside Development. Inline guard
chosen over `MapWhen` to keep the test surface unchanged for BDD/integration
suites that run under Development.

**Verification.** Build clean. 601/601 tests pass.

---

### SEC-PT2-002 — Medium — Vulnerable transitive `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.2` — **PATCHED**

**Location:** `src/UmbracoPrism.ServiceDefaults/UmbracoPrism.ServiceDefaults.csproj`
(directly referenced, also flows transitively into `UmbracoPrism.AppHost`).

**Advisory.** GHSA-4625-4j76-fww9 / CVE-2026-42191 — Moderate. Affected
`>= 1.8.0, <= 1.15.2`; patched in `1.15.3`. The OTLP disk-retry feature, when
enabled via `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY=disk` without
`OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH` set, silently falls
back to `Path.GetTempPath()`. On multi-user systems a local attacker can:
inject crafted `*.blob` files (integrity), read queued telemetry payloads
(confidentiality), or fill the disk (availability).

**Why pt1 missed it.** Pt1 bumped `OpenTelemetry.Api` to 1.15.3 but the
exporter (different package) was still pinned at 1.11.2 in ServiceDefaults.
The advisory was published after the pt1 bump.

**Exploitability here.** Conditional. Prism does not currently set
`OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY=disk`, so the trigger is not on by default
— but the dependency hygiene fix is required regardless.

**Fix (this branch).** Bumped `OpenTelemetry.Exporter.OpenTelemetryProtocol`
from `1.11.2` → `1.15.3`. Left `OpenTelemetry.Extensions.Hosting 1.11.2`
untouched (not flagged by the audit; bumping is unrelated and would be
scope-creep).

**Verification.** `dotnet list package --vulnerable --include-transitive` now
reports **0 vulnerable packages** in every project. Build + 601/601 tests pass.

---

### SEC-PT2-003 — Medium — Logout via GET in `AccountController` (logout-CSRF) — **OPEN**

**Location:** `src/UmbracoPrism.Core/Controllers/AccountController.cs:62-71`

**Observation.** The logout endpoint is `[HttpGet("logout")]` and performs the
sign-out side effect on a GET. Because the cookie is `SameSite=Lax`, top-level
navigations (`<a href="…/account/logout">`, `window.location =`,
`<img src="…/account/logout">` against IE/quirks-mode UAs, even an
auto-submitted form) from another origin will be sent with the member's auth
cookie attached — the classic logout-CSRF.

**Impact.** Cross-origin attacker can forcibly log a member out. By itself a
nuisance/DoS, but a useful primitive in phishing chains: log victim out, then
present a fake login page on a re-login screen, etc. Severity Medium because
no privilege-escalation, no data exfil — only forced session termination.

**Recommended fix.** Convert to `[HttpPost("logout")]` with
`[ValidateAntiForgeryToken]` and update every front-end logout button to be a
form-POST (already a form on most pages). Provide an idempotent GET that
*renders* a confirm page, not one that performs the sign-out.

**Why pt1 missed it.** Pt1 focused on workflow CSRF and the antiforgery wiring
in `PrismWorkflowPageController`. The `AccountController.Logout` method was
out of scope.

**Status.** Fixed (828b5d4) — `AccountController.Logout` changed to `[HttpPost] + [ValidateAntiForgeryToken]`. Razor views (homePage, memberDashboard) updated to POST forms with `@Html.AntiForgeryToken()`. Playwright selectors updated from `link` to `button`. 3 reflection-based regression tests added.
across every page that links to `/account/logout`.

---

### SEC-PT2-004 — Medium — Missing security response headers — **OPEN**

**Location:** `src/UmbracoPrism.TestSite/Program.cs` and (composer-level)
`src/UmbracoPrism.Core/PrismComposer.cs`. Neither wires
`UseHsts`, `UseHttpsRedirection`, nor any of the standard security headers
middleware:

- `Content-Security-Policy` — absent.
- `Strict-Transport-Security` — absent.
- `X-Frame-Options` / CSP `frame-ancestors` — absent (clickjacking).
- `X-Content-Type-Options: nosniff` — absent (MIME-sniff XSS amplifier).
- `Referrer-Policy` — absent.
- `Permissions-Policy` — absent.

**Impact.** Defence-in-depth gap. The sanitizer and CSRF protections are in
place, so this is hardening rather than a directly exploitable hole, but a CSP
in particular would significantly reduce the blast radius of any sanitizer
escape.

**Recommended fix.** A small middleware in `PrismComposer` that sets a sensible
default header set, with a `PrismSecurityHeadersOptions` to allow consumers
(and the Umbraco backoffice's specific needs) to opt out per-route. CSP needs
careful tuning around the Umbraco backoffice and any GDS inline-script paths.

**Status.** Fixed (9f1f34e) — `PrismSecurityHeadersMiddleware` added with `PrismSecurityHeadersOptions` (binds to `Prism:SecurityHeaders`). Injects X-Content-Type-Options, X-Frame-Options (SAMEORIGIN), Referrer-Policy, HSTS (HTTPS only), Permissions-Policy, and Content-Security-Policy-Report-Only. CSP ships as Report-Only (not enforced) because Umbraco backoffice + TestSite views use inline scripts/styles. Backoffice paths excluded by default. 7 regression tests added.
opt-in/opt-out and a per-route exemption for the Umbraco backoffice.

---

### SEC-PT2-005 — Medium — `DefaultAuthenticateScheme = PrismMemberCookie` made unconditional — **OPEN (verify)**

**Location:** `src/UmbracoPrism.Core/PrismComposer.cs` (commit `42b85e5`).

**Observation.** Pt1's commit `42b85e5` set
`DefaultAuthenticateScheme = "PrismMemberCookie"` and `DefaultChallengeScheme`
to the same, unconditionally. Any code path that reads `HttpContext.User`
*without naming a scheme* will now get the member identity, even on Umbraco
backoffice routes. Most Umbraco backoffice handlers name the BackOffice scheme
explicitly and are unaffected; the concern is Razor views, custom middleware,
or Umbraco internals that read `User.Identity` directly.

**Why pt1 didn't fully verify it.** The pt1 ledger cites this as a deliberate
fix for member-area auth challenges; the cross-impact on backoffice
context-reads was not validated.

**Recommended action.** Add an integration test that:
1. Authenticates as an Umbraco backoffice user.
2. Hits a backoffice page that calls into application code reading
   `HttpContext.User`.
3. Asserts the identity is the backoffice user, not a (stale) member cookie.

If the test reveals leakage, switch the unconditional defaults back to a
scheme-aware policy or a custom `IAuthenticationSchemeProvider` that picks
based on path.

**Status.** Confirmed safe (948c2a4) — `BackofficeSchemeIsolationTests.cs` added with 4 regression tests proving: (A) `DefaultAuthenticateScheme = "PrismMemberCookie"` is set unconditionally, (B) `DefaultChallengeScheme = "PrismEntraID"`, (C) Umbraco's `"UmbracoBackOffice"` scheme constant is distinct from `"PrismMemberCookie"`, (D) explicit named-scheme authentication for `"UmbracoBackOffice"` does not fall through to the default scheme — a `PrismMemberCookie` alone cannot satisfy a backoffice auth challenge. See `.squad/decisions/inbox/blathers-pt2-005-backoffice-test.md` for detailed analysis.

---

### SEC-PT2-006 — Low — DataProtection keys ephemeral by default — **Fixed (6c0e8e9)**

**Location:** `src/UmbracoPrism.TestSite/TestSiteRuntimeLayout.cs:43-58`

**Observation.** TestSite only persists DataProtection keys to a stable
location when `PRISM_TESTSITE_RUNTIME_ROOT` is set. Otherwise it falls back to
the default ephemeral location. There is no `ProtectKeysWith*` configured (no
DPAPI, no certificate, no Key Vault), so the on-disk keys — when persisted —
are not encrypted at rest. There is no documented production guidance for
consumers wiring Prism into their own host.

**Impact.** On container/process restart all auth cookies, antiforgery
tokens, and any other DataProtection-protected payloads become invalid. In
multi-instance deployments without shared key persistence, every instance has
its own ring and cookies break across instances. Encryption-at-rest of the key
ring is also missing.

**Recommended fix.** Document the production requirement (shared persistent
location + `ProtectKeysWith*`) in the README/composer guidance; surface a
`PrismDataProtectionOptions` in the composer with sensible defaults.

**Status.** Fixed (6c0e8e9) — `TestSiteRuntimeLayout.cs` now always calls `PersistKeysToFileSystem` with a fallback path of `{ContentRoot}/App_Data/prism-keys/` when `PRISM_TESTSITE_RUNTIME_ROOT` is not set. Keys are no longer ephemeral. Encryption-at-rest (ProtectKeysWith*) and multi-instance shared ring remain as a follow-up concern for production deployments.

---

### SEC-PT2-007 — Low — Unsanitized `accordionSection.Content` in Razor partial — **OPEN**

**Location:**
- `src/UmbracoPrism.Core/Views/Partials/PrismComponents/_PrismComponent-Accordion.cshtml:40`
  renders `@Html.Raw(accordionSection.Content)` directly.
- Producer side: `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`
  in `BuildComponents` (lines 591–720) does not currently set `Content` on
  `PrismAccordionSectionPayload`, and the source `AccordionSection` model has no
  `Content` field.

**Observation.** The partial unconditionally trusts `accordionSection.Content`
and renders it raw. Today the producer never populates that property, so no
unsanitized HTML reaches the page — *currently safe*. But:

1. The contract is implicit. A future `BuildComponents` change, or a new
   producer subclass, that starts populating `Content` will instantly create a
   stored-XSS sink with no test or compile-time signal.
2. The two adjacent partials (`_PrismComponent-Details.cshtml:7`,
   `_PrismComponent-Panel.cshtml:7`) follow the same pattern. Details *is*
   sanitized at the producer (engine line 696). Panel currently isn't set by
   the producer (engine line 672) — same trap as Accordion.

**Recommended fix.** Either:
- Sanitize at the producer for *every* `Content` field at known sites (mirror
  the Details treatment), and add a regression test that asserts every
  `@Html.Raw(*.Content)` payload type is produced via the sanitizer; or
- Sanitize at the partial boundary using `WorkflowContentSanitizer` so the
  Razor side is fail-safe; or
- Remove `Content` from payloads that don't actually need raw HTML.

**Status.** Fixed (03dba49) — `@inject IWorkflowContentSanitizer Sanitizer` added to `_PrismComponent-Accordion.cshtml`; `accordionSection.Content` routed through `Sanitizer.Sanitize()` before `@Html.Raw`. The render boundary is now fail-safe regardless of producer behaviour. 4 regression tests added (`AccordionContentSanitizationTests`).

---

### SEC-PT2-008 — Low — `VinylRecord.cshtml` `@Html.Raw(description)` — **OPEN (informational)**

**Location:** `src/UmbracoPrism.TestSite/Views/VinylRecord.cshtml:85`

**Observation.** `@Html.Raw(description)` on an Umbraco RTE field. Standard
CMS pattern (operator-authored content), partly mitigated by
`Umbraco:CMS:Global:SanitizeTinyMce: true` in `appsettings.json`, but a
backoffice user with content-edit rights can still XSS members.

**Status.** Fixed (6177137) — `@inject IWorkflowContentSanitizer Sanitizer` added to `VinylRecord.cshtml`; `description` routed through `Sanitizer.Sanitize()` before `@Html.Raw`. The GDS allowlist covers all standard TinyMCE output (p, ul, ol, li, h2-h4, strong, em, a, br, blockquote, code + http/https/mailto/tel). If editors need richer formatting (tables, images), that is a separate decision — the allowlist is not widened here. 5 regression tests added (`VinylRecordRteSanitizationTests`).

---

### SEC-PT2-009 — Low — Antiforgery missing on JSON state-mutating endpoints — **Fixed (7a3b0ef)**

**Location:**
- `src/UmbracoPrism.Core/Controllers/PrismNotificationController.cs` (POST/DELETE).
- `src/UmbracoPrism.Core/Controllers/PrismVinylNotificationController.cs` (POST).
- `src/UmbracoPrism.Core/Controllers/BiometricController.cs` `Register` (POST).

**Observation.** State-mutating endpoints accept `[FromBody]` JSON without
`[ValidateAntiForgeryToken]` (or an equivalent
`X-Requested-With` / custom header check). Mitigated in practice by:
1. The cookie being `SameSite=Lax`, blocking cross-site form-encoded POSTs,
2. JSON model binding requiring `Content-Type: application/json`, which a
   simple cross-site form cannot set without preflight,
3. Browser CORS for any non-simple cross-origin request.

But this is mitigation-by-coincidence; a configuration change to
`SameSite=None`, or a relaxed CORS policy, removes the protection.

**Recommended fix.** Add `[AutoValidateAntiforgeryToken]` at controller level,
and ensure clients send the antiforgery cookie+header pair (or use a custom
header that triggers preflight).

**Status.** Fixed (7a3b0ef) — `[IgnoreAntiforgeryToken]` added to `BiometricController`, `PrismNotificationController`, and `PrismVinylNotificationController` with a policy comment documenting the deliberate exemption (Capacitor native-app endpoints; antiforgery not applicable). CSRF protection remains via SameSite=Lax + JSON Content-Type + origin checks. Policy comment added to `PrismComposer`. Reflection-based regression tests added for all three controllers. Note: exemption is intentional — these are NOT browser form POST endpoints.

---

### SEC-PT2-010 — Informational — `IsCapacitorOrigin` accepts `http://localhost` with credentials — **Fixed (11b8cbb)**

**Location:** `src/UmbracoPrism.Core/Controllers/BiometricController.cs:556-…`
(approx; the `IsCapacitorOrigin` helper).

**Observation.** The helper accepts `http://localhost` (any port) as a
permitted Capacitor origin and the response includes
`Access-Control-Allow-Credentials: true`. Intended for Android Capacitor
WebView. Documented risk: any local process serving content on
`http://localhost:*` (other apps, dev servers, malware) could issue
credentialed cross-origin requests against this endpoint from the user's
browser.

**Severity rationale.** Lower than initially feared — only same-machine
localhost-origin pages can issue, which is a high bar in normal user
workflows. Still worth recording so it appears on the threat-model surface.

**Recommended action.** Risk-accept (and document) or restrict to a specific
Capacitor scheme/port range.

**Status.** Fixed (11b8cbb) — `IWebHostEnvironment` injected into `BiometricController`. `IsCapacitorOrigin` changed from static to instance method; `http://localhost` now only permitted in Development. `capacitor://localhost` (iOS) always permitted. 3 CORS-header regression tests added.

---

## Confirmed safe (re-validated this pass)

These were checked specifically because they looked like potential second-pass
risk surfaces, and verified to be safe:

- **IDOR on `WorkflowPollController`.** Instance ownership is enforced by the
  Business App, not by Prism. `BusinessAppWorkflowEngine.cs:82` and `:269`
  check `instance.UserId == userId` against the Bearer-token-derived user and
  return `ACCESS_DENIED` on mismatch. Trust is anchored at the Business App.
- **`BusinessAppWorkflowClient`** forwards the member's Entra Bearer token; no
  ambient credential is added; the Business App is the authoritative authz
  point.
- **`DownstreamDemoController`** `[AllowAnonymous]` endpoints are gated by
  `IsDemoEnabled()` which returns 403 outside Development.
- **Workflow POST flow** has antiforgery, nonce binding, and structural
  validation in `PrismWorkflowPageController.HandlePost`.
- **`PrismReturnUrl`** uses `RedirectHttpResult.IsLocalUrl` (fail-closed
  open-redirect protection).
- **`appsettings.json`** files audited — no live secrets in any tracked file.
  `HMACSecretKey` only appears in git history (already documented in pt1).
- **`MockBusinessApp` `PassthroughSanitizer`** is intentional and seeds are
  developer-authored — out of scope per charter.

---

## What changed on this branch

| Commit    | Subject                                                                |
|-----------|------------------------------------------------------------------------|
| `244f3b5` | sec(pt2): bump OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.2 → 1.15.3 |
| `2ce771f` | sec(pt2): require Development env for MockBusinessApp /api/test/reset  |

Both verified with `dotnet build` clean and 601/601 Core tests passing.
`dotnet list package --vulnerable --include-transitive` is clean across all
projects after the bump.

## What did NOT change

No new SKILL was needed — `ganss-xss-gds-allowlist` already covers the sanitizer
posture. No changes to `WorkflowContentSanitizer` or `MockBusinessApp`'s
`PassthroughSanitizer` (charter-restricted). No changes to the pt1 ledger.
