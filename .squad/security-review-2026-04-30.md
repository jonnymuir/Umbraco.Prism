# Security Review — Umbraco.Prism
**Date:** 2026-04-30  
**Reviewer:** Copper (Security Engineer)  
**Scope:** Post-V2 polymorphic component model — full stack audit  
**Prior review:** 2026-04-21 (Keycloak backchannel / redirect hardening)

---

## Executive Summary

1. **SEC-001 (HIGH — PATCHED)** WorkflowPollController had no `[Authorize]` attribute, exposing workflow state to unauthenticated callers. Fixed in this review.
2. **SEC-002 (CRITICAL — CVE)** Transitive `Microsoft.AspNetCore.DataProtection 10.0.0` has a known critical advisory (GHSA-9mv3-2cwr-p262) in `UmbracoPrism.Shared`. Requires version bump.
3. **SEC-003 (HIGH — XSS)** Four workflow display components render `Model.Component.Content` via `@Html.Raw()` without sanitisation. Content originates from operator-controlled workflow definitions — exploitable if any admin pathway ever accepts user-submitted content strings.
4. **SEC-004 (HIGH — Secret)** `HMACSecretKey` (Umbraco Imaging HMAC signing key) committed in `src/UmbracoPrism.TestSite/appsettings.json`. Key must be rotated and moved to user secrets or environment variables.
5. **SEC-005 (HIGH — npm)** Critical handlebars CVE and multiple high-severity CVEs in `UmbracoPrism.Client` npm dependency tree (mostly transitive via `@umbraco-cms/backoffice` and Storybook toolchain). `npm audit fix` recommended.

The prior redirect hardening and OIDC nonce work continues to hold. SQL parameterisation, tenant isolation, antiforgery, and biometric exchange are sound.

---

## Findings

| ID | Severity | Area | Title | Location | Status |
|----|----------|------|-------|----------|--------|
| SEC-001 | HIGH | Auth | WorkflowPollController — no authentication | `Controllers/WorkflowPollController.cs:14` | ✅ PATCHED |
| SEC-002 | CRITICAL | CVE | Microsoft.AspNetCore.DataProtection 10.0.0 | `UmbracoPrism.Shared.csproj` (transitive) | ⚠️ OPEN |
| SEC-003 | HIGH | XSS | `@Html.Raw(Content)` in workflow display components | 4 Razor partials (see below) | ⚠️ OPEN |
| SEC-004 | HIGH | Secret | HMACSecretKey committed to appsettings.json | `TestSite/appsettings.json` | ⚠️ OPEN |
| SEC-005 | HIGH | CVE | npm — handlebars critical + 10 high CVEs | `UmbracoPrism.Client/package.json` (transitive) | ⚠️ OPEN |
| SEC-006 | MEDIUM | Cookie | CookieSecurePolicy.SameAsRequest | `Core/PrismComposer.cs` | ⚠️ OPEN |
| SEC-007 | MEDIUM | CORS/Rate | IP rate-limit bypassed behind reverse proxy | `Services/ExchangeRateLimitService.cs` | ⚠️ OPEN |
| SEC-008 | MEDIUM | CVE | OpenTelemetry.Api 1.12.0 (GHSA-g94r-2vxg-569j, moderate) | `ServiceDefaults`, `AppHost` | ⚠️ OPEN |
| SEC-009 | LOW | Logging | Log injection via string interpolation in TenantMiddleware | `Middleware/PrismTenantMiddleware.cs` | ✅ PATCHED |
| SEC-010 | LOW | Secret | Entra tenant/client IDs + personal emails in MockBusinessApp appsettings | `MockBusinessApp/appsettings.json` | ⚠️ OPEN |
| SEC-011 | LOW | XSS | `DescribedBy` aria attribute uses unenoded `FieldKey` | `Models/Workflow/PrismFieldContext.cs:86` | ✅ PATCHED |

---

## Detail: Each Finding

### SEC-001 · HIGH · WorkflowPollController — No Authentication ✅ PATCHED

**File:** `src/UmbracoPrism.Core/Controllers/WorkflowPollController.cs`  
**Description:** `GET /api/prism/workflow/poll` accepted `workflowKey`, `instanceId`, and `knownStateVersion` query parameters and returned workflow state (step type, state version) with no authentication requirement. An unauthenticated attacker knowing or guessing a GUID `instanceId` could monitor another member's workflow progress.  
**Fix applied:** Added `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` at the controller class level. Regression test added to `Phase1SecurityRegressionTests.cs` (`WorkflowPollController_RequiresPrismMemberCookieAuth`).  
**Suggested owner:** Blathers (owns WorkflowEngine)

---

### SEC-002 · CRITICAL · Microsoft.AspNetCore.DataProtection CVE (GHSA-9mv3-2cwr-p262)

**File:** `src/UmbracoPrism.Shared/UmbracoPrism.Shared.csproj` (transitive)  
**Description:** Transitive dependency `Microsoft.AspNetCore.DataProtection 10.0.0` has a critical advisory. DataProtection is the cryptographic substrate for `PrismMemberCookie` encryption and antiforgery token generation. If this vulnerability allows decryption or token forgery, the entire authentication surface is at risk.  
**Remediation:** `dotnet list package --vulnerable` confirmed the advisory. Upgrade Umbraco.Cms (the package pulling this in) to a version that resolves the transitive dep, or add an explicit `<PackageReference>` for a patched version in `UmbracoPrism.Shared.csproj`.  
**Suggested owner:** Copper + Blathers

---

### SEC-003 · HIGH · `@Html.Raw(Content)` in Workflow Display Components

**Files:**
- `src/UmbracoPrism.Core/Views/Partials/PrismComponents/_PrismComponent-Body.cshtml`
- `src/UmbracoPrism.Core/Views/Partials/PrismComponents/_PrismComponent-InsetText.cshtml`
- `src/UmbracoPrism.Core/Views/Partials/PrismComponents/_PrismComponent-WarningText.cshtml`
- `src/UmbracoPrism.Core/Views/Partials/PrismComponents/_PrismComponent-NotificationBanner.cshtml`

**Description:** All four views call `@Html.Raw(Model.Component.Content)` to allow rich HTML (GDS Design System markup). Content originates from workflow definition JSON files, which are operator-authored. The risk is currently bounded by the admin-only definition editor (Dev-mode only). However, the unescaped rendering pattern will become a persistent XSS vector if:
- The definition editor is ever enabled in non-Dev environments
- A Content field is ever populated from user-supplied input

**Remediation (recommended):** Introduce a `IWorkflowContentSanitizer` abstraction using [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer) with a GDS-aligned allowlist (headings, paragraphs, strong, em, a, ul, ol, li, blockquote). Apply to all `Content` fields at render time. This gives defense-in-depth without breaking legitimate rich text.  
**Suggested owner:** Blathers (component model), Copper (sanitiser config)

---

### SEC-004 · HIGH · HMACSecretKey Committed to appsettings.json

**File:** `src/UmbracoPrism.TestSite/appsettings.json`  
**Description:** A base64-encoded HMAC key (`Umbraco:CMS:Imaging:HMACSecretKey`) is committed in plaintext. This key signs Umbraco Media image URLs; possession of it allows generating valid signed URLs for any media asset. The file also contains `Prism:VaultUri` pointing to a real Azure Key Vault instance.  
**Remediation:**
1. Rotate the HMAC key immediately — the committed value is compromised.
2. Move to `dotnet user-secrets` for local dev, or environment variable override in CI/CD.
3. Add a git pre-commit hook (or CI secret scanning) to prevent recurrence.

**Suggested owner:** Copper

---

### SEC-005 · HIGH · npm Critical/High CVEs in UmbracoPrism.Client

**File:** `src/UmbracoPrism.Client/package.json`  
**Description:** `npm audit` reports 26 vulnerabilities (1 critical, 10 high). Key findings:
- **Critical — handlebars**: JavaScript injection via AST type confusion (multiple CVEs). Transitive via `@hey-api/openapi-ts` and `@umbraco-cms/backoffice`.
- **High — vite / rollup / storybook**: Arbitrary file read / path traversal via dev server WebSocket. **Dev-only impact** — these tools are not bundled into production output.
- **High — axios**: SSRF via `NO_PROXY` hostname normalization bypass, prototype pollution. Transitive via `@umbraco-cms/backoffice`.
- **High — lodash**: Prototype pollution, code injection via template engine.

**Production risk assessment:** Vite/Rollup/Storybook are pure dev tooling (in `devDependencies`) and present no runtime risk. Handlebars, axios, and lodash are transitive deps of `@umbraco-cms/backoffice` (a production dependency) and represent real risk if any Umbraco backoffice feature interacts with untrusted input.  
**Remediation:** Run `npm audit fix` to address auto-fixable issues. Update `@umbraco-cms/backoffice` to the latest patch. Track residual issues in `npm audit --json`.  
**Suggested owner:** Blathers (client build), Copper (risk sign-off)

---

### SEC-006 · MEDIUM · CookieSecurePolicy.SameAsRequest

**File:** `src/UmbracoPrism.Core/PrismComposer.cs`  
**Description:** `PrismMemberCookie` is configured with `CookieSecurePolicy.SameAsRequest`. On HTTP origins (e.g. local dev, misconfigured prod) the `Secure` flag will be omitted from the cookie, allowing transmission over unencrypted HTTP. If TLS is terminated before reaching the app (e.g. at a load balancer) and the app runs on HTTP internally, cookies will flow without `Secure`.  
**Remediation:** Change to `CookieSecurePolicy.Always`. Ensure all non-TLS dev flows use HTTPS via `dotnet dev-certs`.  
**Suggested owner:** Blathers (PrismComposer)

---

### SEC-007 · MEDIUM · IP Rate-Limiting Not Proxy-Aware

**File:** `src/UmbracoPrism.Core/Services/ExchangeRateLimitService.cs`  
**Description:** `BiometricController.GetClientIp()` uses `HttpContext.Connection.RemoteIpAddress`. Behind a reverse proxy (nginx, Azure Front Door, AWS ALB), all requests appear to originate from the proxy IP. Biometric exchange rate limits (protecting the JWT/biometric token endpoint) would then be applied per-proxy rather than per end-user, making the limit trivially bypassable.  
**Remediation:** Read `X-Forwarded-For` (or `CF-Connecting-IP`) and validate it against a trusted proxy list. ASP.NET Core's `ForwardedHeadersMiddleware` with `ForwardedHeaders.XForwardedFor` + `KnownProxies` is the idiomatic solution.  
**Suggested owner:** Copper

---

### SEC-008 · MEDIUM · OpenTelemetry.Api 1.12.0 (GHSA-g94r-2vxg-569j)

**Projects:** `UmbracoPrism.ServiceDefaults`, `UmbracoPrism.AppHost`  
**Description:** Moderate advisory on the transitive `OpenTelemetry.Api 1.12.0`. Advisory details indicate trace data corruption under adversarial input — no known RCE/auth bypass.  
**Remediation:** Upgrade `OpenTelemetry.*` packages to `1.12.1` or later.  
**Suggested owner:** Blathers

---

### SEC-009 · LOW · Log Injection in PrismTenantMiddleware ✅ PATCHED

**File:** `src/UmbracoPrism.Core/Middleware/PrismTenantMiddleware.cs`  
**Description:** Log statement used `$"Unknown tenant domain: {host}"` string interpolation. If `host` contained CRLF sequences (possible in certain non-validating HTTP proxies), log lines could be injected.  
**Fix applied:** Changed to `logger.LogWarning("Unknown tenant domain: {Host}", host)` structured logging.

---

### SEC-010 · LOW · Entra Tenant/Client IDs and Emails in MockBusinessApp Config

**File:** `src/UmbracoPrism.MockBusinessApp/appsettings.json`  
**Description:** Real Azure Entra tenant and client GUIDs, and personal email addresses, are committed in plaintext. Not directly exploitable (IDs are not secrets), but constitutes information disclosure.  
**Remediation:** Use placeholder values (`00000000-0000-...`) in version-controlled config; supply real values via user secrets or CI environment variables.  
**Suggested owner:** Blathers

---

### SEC-011 · LOW · `DescribedBy` Aria Attribute — Unencoded FieldKey ✅ PATCHED

**File:** `src/UmbracoPrism.Core/Models/Workflow/PrismFieldContext.cs:86`  
**Description:** `FieldKey` was interpolated directly into an `aria-describedby` HTML attribute string without HTML encoding. An operator-supplied `FieldKey` containing `"` would break out of the attribute context. Exploitable only by operators with workflow definition write access.  
**Fix applied:** Added `System.Net.WebUtility.HtmlEncode(fieldKey)` before attribute construction.

---

## Phase 1 Security Regression Test Coverage

Current test count after this review: **547 passing** (19 new from prior reviews, +1 from SEC-001).

| Area | Covered? | Tests |
|------|----------|-------|
| Open redirect hardening | ✅ | 10 tests |
| Debug UI removed in production | ✅ | 4 tests |
| Notification broadcast requires auth | ✅ | 1 test + 1 tenant test |
| Downstream demo restriction | ✅ | 4 tests |
| WorkflowPollController requires auth (SEC-001) | ✅ | `WorkflowPollController_RequiresPrismMemberCookieAuth` |
| Security response headers (CSP, X-Frame-Options) | ❌ | Not tested |
| CookieSecurePolicy=Always (SEC-006) | ❌ | Not tested |
| IP rate-limit respects forwarded headers (SEC-007) | ❌ | Not tested |
| Content sanitisation in @Html.Raw paths (SEC-003) | ❌ | Not applicable until sanitiser introduced |

---

## Confirmed Solid

The following were reviewed and found to be correctly implemented:

| Area | Location | Notes |
|------|----------|-------|
| Open redirect | `PrismReturnUrl.Normalize()` | Uses `RedirectHttpResult.IsLocalUrl()`, hard fail-closed |
| OIDC nonce validation | `PrismOidcConfiguration.cs:376–501` | Hard fail with explicit exception on missing/mismatched nonce |
| Token validation (issuer + audience) | `PrismOidcConfiguration.cs:158–201` | Signing keys cached with rotation |
| Token refresh URI hygiene | `PrismOidcConfiguration.cs:506` | `props.RedirectUri = null` on refresh, prevents leakage |
| Antiforgery on workflow POST | `PrismWorkflowPageController` | `IAntiforgery.ValidateRequestAsync` before any processing |
| Nonce-based form tamper protection | `PrismWorkflowPageController` | Instance nonce validated against submitted value |
| SQL parameterisation | `TenantService.cs`, `PrismMigrationPlan.cs` | PetaPoco `@0` placeholders throughout |
| Tenant isolation | `PrismTenantMiddleware.cs` | Host header resolution, no spoofing surface |
| Biometric tenant cross-check | `BiometricController.cs` | JWT `tid` claim validated against current tenant |
| Biometric rate limiting | `ExchangeRateLimitService.cs` | Per-IP with in-memory store (single instance) |
| Biometric CORS | `BiometricController.cs` | Restricted to `capacitor://localhost` and `http://localhost` |
| Admin endpoint guard (MockBusinessApp) | `Program.cs:25–47` | `IsDevelopment()` 404 middleware gate |
| Keycloak backchannel guard | `Program.cs` | `InvalidOperationException` if env var missing in non-Dev |
| TenantManagementController auth | `TenantManagementController.cs` | Double-protected: `BackOfficeAccess` + `PrismAdmins` |
| WrapperAttrs / ConditionalOn encoding | `PrismFieldContext.cs:97–108` | `WebUtility.HtmlEncode` explicitly applied |
| PatternAttr encoding | `PrismFieldContext.cs` | `WebUtility.HtmlEncode(Field.Pattern)` |
| SecretVaultService | `SecretVaultService.cs` | Azure Key Vault via `DefaultAzureCredential`, 1h cache |
| Inline OIDC secret guard | `PrismOidcConfiguration.cs` | Only allowed for `IsRepoOwnedLocalDemoTenant()` — hostname/clientId validated |

---

## Recommended Next Actions (Priority Order)

1. **Rotate the committed HMAC signing key** (SEC-004) — treat as compromised.
2. **Upgrade `Microsoft.AspNetCore.DataProtection`** (SEC-002) — critical CVE on the auth crypto substrate.
3. **Run `npm audit fix`** (SEC-005) — auto-fixable items first, then track remaining.
4. **Introduce HTML sanitiser for `@Html.Raw(Content)`** (SEC-003) — design-system-compatible allowlist.
5. **Set `CookieSecurePolicy.Always`** (SEC-006) — low effort, high value.
6. **Add `ForwardedHeadersMiddleware` for biometric rate limiting** (SEC-007) — required before any cloud deployment.
7. **Upgrade `OpenTelemetry.Api`** (SEC-008) — `1.12.1+`.
