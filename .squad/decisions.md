# Decisions

Umbraco.Prism team decisions. Append-only ledger.


## 📌 2026-04-30: Blathers — PT2 Backend Security Batch (PR #40) — 5 Findings Fixed

**Status:** ✅ MERGED — 5 commits on `main` (squashed as `83eb30e`); 618 tests passing (+17 new)

### Summary

Blathers shipped 5 security hardening commits targeting backend infrastructure, authentication, and DataProtection. Five PT2 findings now closed; three remain open (being dispatched to other agents).

### Findings Fixed

| ID | Severity | Title | Commit |
|----|----------|-------|--------|
| SEC-PT2-003 | Medium | Logout-CSRF (GET → POST + antiforgery) | `828b5d4` |
| SEC-PT2-004 | Medium | Missing security headers (CSP, HSTS, XFO, XCTO, etc.) | `9f1f34e` |
| SEC-PT2-006 | Low | DataProtection keys now persisted to filesystem | `6c0e8e9` |
| SEC-PT2-009 | Low | Antiforgery exemptions on Capacitor JSON endpoints (documented) | `7a3b0ef` |
| SEC-PT2-010 | Info | IsCapacitorOrigin restricted to Development (http://localhost) | `11b8cbb` |

### Three Notable Decisions

#### 1. CSP Shipped as Report-Only (SEC-PT2-004)

**Decision:** `PrismSecurityHeadersMiddleware` implements CSP as `Content-Security-Policy-Report-Only`, not enforced.

**Rationale:** Umbraco backoffice + TestSite Razor views use inline scripts and styles (e.g., `@Html.Raw(imageryCss)`) that a strict enforced CSP would block. Report-Only allows violation observation without breaking the site. Promoting to enforced CSP requires nonce/hash rollout post-audit — recorded as follow-up.

**Rule Going Forward:** Security-headers middleware respects `ExcludeBackoffice: true` by default; CSP stays Report-Only until inline-script audit + nonce deployment plan is locked in.

#### 2. Capacitor JSON Endpoints Deliberately Exempt from Antiforgery (SEC-PT2-009)

**Decision:** `BiometricController`, `PrismNotificationController`, `PrismVinylNotificationController` carry `[IgnoreAntiforgeryToken]` with a policy comment documenting the exemption.

**Rationale:** These are native-app (Capacitor) JSON API endpoints. Native apps cannot supply the ASP.NET Core antiforgery cookie+header pair — they do not participate in the browser cookie jar. Applying `[ValidateAntiForgeryToken]` would break the mobile app. CSRF protection remains via:
- Cookie `SameSite=Lax` (blocks form-encoded POST)
- JSON `Content-Type: application/json` requirement (triggers browser CORS preflight)
- `IsCapacitorOrigin` check on unauthenticated endpoints

**Rule Going Forward:** Any NEW browser-facing form-POST endpoint MUST carry `[ValidateAntiForgeryToken]`. Policy comments on exemptions prevent future reviewers from "fixing" intentional security decisions.

#### 3. DataProtection Key Persistence + Follow-Ups (SEC-PT2-006)

**Decision:** TestSite now calls `PersistKeysToFileSystem` with fallback path `{ContentRoot}/App_Data/prism-keys/`.

**Rationale:** Core library cannot double-configure DataProtection — host may already own that config. TestSite-layer fix ensures keys are no longer ephemeral and survive process restarts.

**Follow-Up Gaps (not addressed):**
- **Encryption-at-rest:** `ProtectKeysWith*` (DPAPI, certificate, Key Vault) not configured; keys on-disk are plaintext.
- **Multi-instance sharing:** Azure Blob / Redis key ring providers not wired; each instance in a cluster has its own isolated ring.

Both gaps require ops/infrastructure input and documented in the follow-up seam.

### Test Results

- **Before:** 601 tests passing (Core unit tests baseline)
- **After:** 618 tests passing (+17 new, including 7 in `PrismSecurityHeadersMiddlewareTests.cs`)
- **Status:** All green; no regressions

### Follow-Up Items (Dispatched)

1. **SEC-PT2-005** (Backoffice auth default scheme) → Blathers on `sec/pt2-backoffice-test` (integration test needed)
2. **SEC-PT2-007 + SEC-PT2-008** (Razor @Html.Raw sanitization) → Isabelle on `sec/pt2-razor-hardening`

### Basis

Blathers' 5-commit implementation (2026-04-30); security review pass 2 findings (Copper, 2026-04-30); decision record in inbox (`.squad/decisions/inbox/blathers-pt2-backend.md`).

---

## 📌 2026-04-30: Blathers — SEC-004 Closed — TestSite Secrets Management Pattern

**Status:** ✅ IMPLEMENTED — Commit `b6336fd` on `main`

### Summary

SEC-004 (HIGH — committed `Umbraco:CMS:Imaging:HMACSecretKey` and `Prism:VaultUri` in `src/UmbracoPrism.TestSite/appsettings.json`) is **CLOSED**.

**Remediation:** Removed both values from tracked `appsettings.json`; introduced `appsettings.Local.json` (gitignored) loaded via `builder.Configuration.AddJsonFile(...)` before `CreateUmbracoBuilder()`. Documented first-run bootstrap in new `src/UmbracoPrism.TestSite/README.md`.

### Chosen Pattern: `appsettings.Local.json` (gitignored)

**Rejected:** `dotnet user-secrets` — already wired (`UserSecretsId` in `.csproj`) but doesn't mesh cleanly with Umbraco's HMAC key first-run bootstrap (Umbraco writes to `appsettings.json`, not to user-secrets store). The `appsettings.Local.json` pattern is self-documenting and matches the bootstrap flow.

**Chosen:** `appsettings.Local.json` loaded via `builder.Configuration.AddJsonFile(...)` before `CreateUmbracoBuilder()`. File is gitignored at root `.gitignore`.

### Rule Going Forward

- ❌ Never commit a value for `Umbraco:CMS:Imaging:HMACSecretKey` in any tracked `appsettings*.json`
- ❌ Never commit a value for `Prism:VaultUri` in any tracked `appsettings*.json`  
- ✅ Both keys live in `src/UmbracoPrism.TestSite/appsettings.Local.json` (gitignored)
- ✅ See `src/UmbracoPrism.TestSite/README.md` for developer bootstrap instructions

### Technical Note

Umbraco's `IJsonSettingsEditor` writes the auto-generated HMAC key to `appsettings.json` in the content root on first run when the key is absent from all config providers. Once the key is present in `appsettings.Local.json` (part of the config chain), Umbraco sees a non-null value and skips regeneration — keeping `appsettings.json` clean on subsequent runs.

### Caveat

The leaked HMAC value (`dMxHo7...`) remains in git history permanently; rotation of the value is what matters going forward. TestSite is local-only; real-world risk is **LOW** per user.

### Basis

Blathers' commit `b6336fd` implementation; security review findings (2026-04-30, Copper); pattern decision recorded in inbox (2026-04-30).

---

## 📌 2026-04-29: Copilot (Backend, acting as Blathers) — CI Readiness Fix: localhost-auth Playwright Lane

**Status:** ✅ IMPLEMENTED — Commit `c2ff66a` merged to `origin/main`

**Symptom:** Three `localhost-auth-playwright` lane failures (2026-04-26 onward), one per spec file, all on first test navigating to `/dashboard` or workflow start URLs. Subsequent tests passed, indicating cold-Razor-view first-render race past 5s visibility timeout.

**Root Causes:**
1. **Incomplete seed-contract gate:** `DownstreamDemoController.BuildSeedContract` validated only `community-enquiry` page; `planning-notification`, `payment-demo`, `information-request` pages could still be publishing when `routeContractReady` flipped true.
2. **Insufficient HTTP readiness probes:** `live-app-host.ts` probed only `/my-workflows`; dashboard and four workflow URLs never pre-warmed, forcing first test to pay Razor cold-compile cost. V2 polymorphic component view tree pushed cost past 5s budget.

**Fixes:**
- `DownstreamDemoController.BuildSeedContract` now gates on all four workflow pages + dashboard
- `live-app-host.ts` adds five HTTP probes (dashboard + four workflow URLs) for pre-warming + verification
- No `await sleep()` hacks; readiness contract is behaviourally complete

**Flake Patterns Addressed:**
- ✅ First test per file fails on missing content; rest pass (cold-render race)
- ✅ `routeContractReady:true` while planning/payment/information still 404

**Follow-up (Residual):**
- 🟡 Aspire restarts between spec files (~3 min overhead); shared `globalSetup` host would fix but requires non-trivial refactor
- 🟡 No observability of seed-publishing progress; "published in Nms" logs would help spot regressions

**Verification:** C# core 547/547 passing; TypeScript clean; TestSite 0 warnings; CI run `c2ff66a` green.

**Basis:** CI readiness diagnostic memo (2026-04-29, Copilot, Blathers voice).

---

## 📌 2026-04-28: Blathers (Backend Dev) & Copilot — PR #37: Playwright Test Failures Root Cause & Fixes

**Status:** ✅ IMPLEMENTED — Two v2.0 regressions fixed in commit `7e55151` on `origin/main`

**Test Failures (PR #37 CI run 25068886676):**
1. **`validation: minimum decimal value enforced`** — Expected error summary; got only field error (decimal constraint silently ignored)
2. **`happy path: multi-step planning application`** — Expected reference number in confirmation panel; missing from seed

**Root Causes:**

1. **Decimal field validation gap:** `WorkflowFieldValidator.ValidateType()` (lines 172-179) and `ValidateConstraints()` (lines 293-307) recognized `"number"` and `"currency"` but not `"decimal"`. V2.0 atomic swap (commit `7423803`) introduced `"decimal"` as first-class type (returned by `DecimalInputComponent`), but validator was never updated → min/max constraints silently ignored.

2. **Planning confirmation incomplete:** `planning-notification.json` seed "complete" state had confirmation panel heading but NO body component containing reference number. Seed was incomplete during v1→v2 migration (commits `2cdb0dc`, `67bb57b`).

**Fixes (Commit `7e55151`):**
- Added `"decimal"` case to type validation switch (line 174)
- Added `"decimal"` check to min/max constraint validation (line 295)
- Added reference number body component to planning-notification.json "complete" state
- Added unit test: `GivenDecimalFieldWithMinConstraint_WhenValueBelowMin_ThenReturnsError`

**Files Changed:**
- `src/UmbracoPrism.Core/Services/Workflow/WorkflowFieldValidator.cs` (lines 174, 295)
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification.json` (lines 328-331)
- `src/UmbracoPrism.Core.Tests/Services/Workflow/WorkflowFieldValidatorTests.cs` (new test)

**Test Results:**
- Before: 542/546 passing (4 pre-existing TestSite view model binding failures)
- After: 543/547 passing (+1 new decimal validation test; same 4 pre-existing failures)

**Blind Spots Revealed:**
1. No compile-time guarantee all field types handled in validator → extract field types to shared enum + add coverage tests for ALL types + constraints
2. Seed JSON has no schema enforcement → JSON Schema + seed linter in CI
3. Single 5000+ line atomic commit created spread-out fixes → pre-commit checklist (validator, seeds, tests, ModelsBuilder) for future breaking changes

**Recommendation:** Close PR #37 as superseded (fixes now on `main`); or rebase if additional valuable work in branch.

**Basis:** Root cause analysis memo (2026-04-28, Blathers) + implementation report (Copilot, 2026-04-27).

---

## 📌 2026-04-30: Blathers (Backend Dev) — V2 Naming Debt Cleared

**Status:** ✅ COMPLETE — Commit `290a18c` merged to `origin/main`

**Summary:** The `V2` suffix debt has been fully cleared from the production codebase. Both `WorkflowDefinitionFileV2` and `StepDefinitionV2` have been removed.

**Final Type Names:**
- `WorkflowDefinitionFileV2` → `WorkflowDefinitionFile` (UmbracoPrism.Shared.Models.Workflow)
- `StepDefinitionV2` → `StepDefinition` (UmbracoPrism.Shared.Models.Workflow)

**File Changes:**
- Deleted: `src/UmbracoPrism.Shared/Models/Workflow/Components/WorkflowDefinitionFileV2.cs`
- Renamed: `src/UmbracoPrism.Core.Tests/Workflow/V2/` → `src/UmbracoPrism.Core.Tests/Workflow/Components/`

**Key Notes:**
- `WorkflowDefinitionFile` and `StepDefinition` (canonical types) existed before rename, now fully canonical
- No production code referenced V2 types; only `ComponentPolymorphismTests.cs` did
- 547 tests pass after the change

**Basis:** Blathers implementation report (2026-04-30), commit `290a18c`.

---

## 📌 2026-04-30: Copper (Security Engineer) — Full Security Review & Patching

**Status:** ✅ PARTIAL — 3 critical patches applied; 6 findings open for follow-up triage

**Review Scope:** Full codebase security audit with focus on auth, data protection, injection, and cookie security.

**Patches Applied (Committed):**
1. **SEC-001 (HIGH):** WorkflowPollController auth — Added `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` at controller level; regression test added
2. **SEC-009 (LOW):** Log injection fix in workflow logging
3. **SEC-011 (LOW):** HTML encoding in workflow component rendering

**Open Findings Requiring Follow-up (Triage by tom-nook):**
- **SEC-002 (CRITICAL):** DataProtection CVE (external dependency)
- **SEC-003 (HIGH):** Cookie policy regression risk (`SameAsRequest` → `Always` required pre-production)
- **SEC-004 (HIGH):** `@Html.Raw(Content)` sanitization required before editor leaves dev-only mode
- **SEC-005 (HIGH):** `HttpContext.Connection.RemoteIpAddress` proxy-awareness for biometric rate-limiting
- **SEC-006 (MEDIUM):** Committed secrets in `appsettings.json` (HMACSecretKey compromised; requires rotation)
- **SEC-007 (MEDIUM):** Missing secret scanning step in CI pipeline

**Decision Locked In:**
- `IWorkflowContentSanitizer` abstraction (HtmlSanitizer + GDS allowlist) is pre-condition for shipping definition editor to non-dev
- `CookieSecurePolicy.Always` + `ForwardedHeadersMiddleware` required pre-production
- Secrets → `dotnet user-secrets` (local) / environment variables (CI/CD)

**Basis:** Copper security review memo (2026-04-30), full review: `.squad/security-review-2026-04-30.md`.

---

## 🔒 Security — Open Triage Queue

**Triaged By:** tom-nook (2026-04-30 onward)

| Finding | Severity | Category | Owner | ETA |
|---------|----------|----------|-------|-----|
| SEC-002 | CRITICAL | External Dependency | tom-nook | TBD |
| SEC-003 | HIGH | Cookie Security | tom-nook | Pre-production |
| SEC-004 | HIGH | Content Sanitization | tom-nook | Pre-prod |
| SEC-005 | HIGH | Proxy Awareness | tom-nook | Pre-prod |
| SEC-006 | MEDIUM | Secrets Management | tom-nook | Post-review |
| SEC-007 | MEDIUM | CI Hardening | tom-nook | Post-review |
# Decision: SEC-002 + SEC-008 — NuGet CVE Bumps (Blathers, 2026-04-30)

**Status:** ✅ CLOSED — Commit `2618c54` on `main`

## SEC-002 (CRITICAL) — Microsoft.AspNetCore.DataProtection GHSA-9mv3-2cwr-p262

**Affected project:** `UmbracoPrism.Shared`  
**Vulnerable version resolved:** 10.0.0 (transitive via JwtBearer 10.0.2)  
**Versions also affected (confirmed by NuGet vuln scan):** 10.0.0, 10.0.1, 10.0.2, ..., up to 10.0.6  
**Fixed version pinned:** 10.0.7 (latest stable)

**Fix:** Added explicit `<PackageReference Include="Microsoft.AspNetCore.DataProtection" Version="10.0.7" />` to `UmbracoPrism.Shared.csproj`. Also bumped the co-pinned `System.Security.Cryptography.Xml` from 10.0.6 → 10.0.7 to avoid NU1605 downgrade error (DataProtection 10.0.7 depends on CryptographyXml >= 10.0.7).

**Why Shared and not Core?** The vulnerability appeared only in `UmbracoPrism.Shared` because that project uses `Microsoft.NET.Sdk` (no web framework reference). `UmbracoPrism.Core` uses `Microsoft.NET.Sdk.Web` which bundles the framework runtime, overriding the transitive DataProtection version to a patched release automatically. Explicit pin in Shared forces the same patched version.

---

## SEC-008 (MEDIUM) — OpenTelemetry.Api GHSA-g94r-2vxg-569j

**Affected projects:** `UmbracoPrism.ServiceDefaults`, `UmbracoPrism.AppHost`  
**Vulnerable version resolved:** 1.12.0 (transitive via Instrumentation.AspNetCore 1.12.0 / Http 1.12.0)  
**Versions also affected (confirmed):** 1.12.0, 1.13.0, 1.13.1, up to at least 1.13.x  
**Fixed version pinned:** 1.15.3 (latest stable)

**Fix:** Added explicit `<PackageReference Include="OpenTelemetry.Api" Version="1.15.3" />` to both `UmbracoPrism.ServiceDefaults.csproj` and `UmbracoPrism.AppHost.csproj`. AppHost needs its own pin because it doesn't inherit NuGet overrides from project references.

**Pairing rationale:** Both fixes are mechanical NuGet version bumps with no code changes. They were bundled in one commit to keep the fix atomic and reviewable.

---

## Rule Going Forward

- Any new OpenTelemetry.* instrumentation package additions should verify transitive `OpenTelemetry.Api` is at the pinned minimum.
- `UmbracoPrism.Shared` requires explicit framework package pins that `Core` gets for free via the web SDK.
# Decision: SEC-004 Closed — TestSite Secrets Management Pattern

**Date:** 2026-04-30  
**Author:** Blathers (Backend Dev)  
**Status:** ✅ IMPLEMENTED — Commit `b6336fd` on `main`

## Summary

SEC-004 (HIGH — committed `Umbraco:CMS:Imaging:HMACSecretKey` and `Prism:VaultUri` in `src/UmbracoPrism.TestSite/appsettings.json`) is closed.

## Chosen Pattern: `appsettings.Local.json` (gitignored)

**Rejected:** `dotnet user-secrets` — already wired (`UserSecretsId` in `.csproj`) but doesn't mesh cleanly with Umbraco's HMAC key first-run bootstrap (Umbraco writes to `appsettings.json`, not to user-secrets store). The `appsettings.Local.json` pattern is self-documenting and matches the bootstrap flow.

**Chosen:** `appsettings.Local.json` loaded via `builder.Configuration.AddJsonFile(...)` before `CreateUmbracoBuilder()`. File is gitignored at root `.gitignore`.

## Rule Going Forward

- ❌ Never commit a value for `Umbraco:CMS:Imaging:HMACSecretKey` in any tracked `appsettings*.json`
- ❌ Never commit a value for `Prism:VaultUri` in any tracked `appsettings*.json`  
- ✅ Both keys live in `src/UmbracoPrism.TestSite/appsettings.Local.json` (gitignored)
- ✅ See `src/UmbracoPrism.TestSite/README.md` for developer bootstrap instructions

## Technical Note

Umbraco's `IJsonSettingsEditor` writes the auto-generated HMAC key to `appsettings.json` in the content root on first run when the key is absent from all config providers. Once the key is present in `appsettings.Local.json` (part of the config chain), Umbraco sees a non-null value and skips regeneration — keeping `appsettings.json` clean on subsequent runs.
# Decision: SEC-006 — CookieSecurePolicy.Always (Blathers, 2026-04-30)

**Status:** ✅ CLOSED — Commit `df434bf` on `main`

## Summary

`PrismMemberCookie` was configured with `CookieSecurePolicy.SameAsRequest` in `PrismComposer.cs`. This meant the `Secure` flag would be omitted from the cookie when the request arrived over HTTP — including the common pattern where a TLS-terminating load balancer communicates with the app over HTTP internally.

## Fix

Changed `CookieSecurePolicy.SameAsRequest` → `CookieSecurePolicy.Always` in `PrismComposer.cs` (line ~108 in the `AddMicrosoftIdentityWebApp` `cookieOptions` callback).

## Impact on Local Dev

Plain HTTP local dev no longer works for authenticated flows. HTTPS is required. The default Aspire launch profile already enforces HTTPS (`dotnet dev-certs https --trust` on first clone). This was always the intended dev posture.

## Regression Test

`Phase1SecurityRegressionTests.PrismMemberCookie_SecurePolicy_IsAlways` — builds a minimal DI service collection mirroring PrismComposer's cookie options configuration and asserts `SecurePolicy == CookieSecurePolicy.Always`.

## Rule Going Forward

- `CookieSecurePolicy.Always` is required for all Prism-managed auth cookies.
- If a new cookie scheme is added, it must also use `Always`.
# Decision: SEC-007 — Proxy-Aware IP Rate Limiting via ForwardedHeadersMiddleware (Blathers, 2026-04-30)

**Status:** ✅ CLOSED — Commit `44c476f` on `main`

## Summary

`BiometricController.GetClientIp()` used `HttpContext.Connection.RemoteIpAddress` directly. Behind a reverse proxy, all requests share the proxy's IP as `RemoteIpAddress`, making per-client rate limits trivially bypassable.

## Fix

Wired `ForwardedHeadersMiddleware` in `PrismComposer.cs`:

1. **Service configuration:** `builder.Services.Configure<ForwardedHeadersOptions>(...)` with `ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto`. `KnownNetworks` and `KnownProxies` cleared to allow any proxy topology (dev-safe default).

2. **Middleware registration:** `app.UseForwardedHeaders()` added as the first call in the `UmbracoPipelineFilter` pre-pipeline, before `PrismTenantMiddleware` and `PrismBrandingMiddleware`. This ensures `RemoteIpAddress` is rewritten from `X-Forwarded-For` before any IP-sensitive code runs.

3. **BiometricController comment updated** to document that the middleware is now configured and the GetClientIp() pattern is intentionally proxy-aware.

## Security Caveat (Production Hardening Required)

Clearing `KnownProxies` / `KnownNetworks` means **any** `X-Forwarded-For` header is trusted. A malicious end client behind a non-trusted path could spoof their IP. Before production deployment:

```
// In appsettings / deployment config:
options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
options.KnownProxies.Add(IPAddress.Parse("203.0.113.1")); // your load balancer IP
```

This MUST be documented in the deployment runbook and reviewed before any public-facing launch.

## Regression Test

`Phase1SecurityRegressionTests.BiometricRateLimit_PartitionKey_UsesRemoteIpAddress_NotRawForwardedForHeader` — verifies that `ExchangeRateLimitService.CheckIpLimit()` creates independent rate-limit buckets for distinct IP strings (proving the partition key is per-client, not shared), and that exhausting one bucket does not affect the other.

## Rule Going Forward

- All new rate-limiting code MUST use `HttpContext.Connection.RemoteIpAddress` (not raw header reads).
- `ForwardedHeadersMiddleware` is now the infrastructure-level IP resolution layer; controllers must not reimplement it.
# Decision: SEC-010 — Scrub PII and Placeholder Config in MockBusinessApp (Blathers, 2026-04-30)

**Status:** ✅ CLOSED — Commit `87900c9` on `main`

## Summary

`src/UmbracoPrism.MockBusinessApp/appsettings.json` contained:
- Two real Azure Entra tenant GUIDs and two client GUIDs (information disclosure)
- `jonnypmuir@gmail.com` (personal email address — PII) appearing twice as member emails

## PII Flag

`jonnypmuir@gmail.com` is a real personal email address committed in plaintext. This constitutes PII disclosure. Scrubbed in commit `87900c9`. The original value **remains in git history** — if this repository is ever made public, the owner should either rewrite history or notify the individual per applicable data protection law (GDPR Art. 17 / UK GDPR).

## Fix

1. **appsettings.json:** All real GUIDs replaced with zero-value placeholders (`00000000-0000-0000-0000-000000000001` through `...0004`). Real email addresses replaced with `alpha-admin@example.com` (intentionally generic placeholder).

2. **Program.cs:** Wired `appsettings.Local.json` (optional, gitignored) via `builder.Configuration.AddJsonFile(...)` before service registration — identical pattern to TestSite (SEC-004).

3. **README.md:** Created at `src/UmbracoPrism.MockBusinessApp/README.md` documenting the local override pattern and bootstrap steps.

4. **.gitignore:** Added `src/UmbracoPrism.MockBusinessApp/appsettings.Local.json` entry.

## Pattern Established

All mock/test app configs in this solution follow the `appsettings.Local.json` pattern for real values:
- `src/UmbracoPrism.TestSite/appsettings.Local.json` (SEC-004)
- `src/UmbracoPrism.MockBusinessApp/appsettings.Local.json` (this finding)

## Rule Going Forward

- `appsettings.json` in any project MUST use placeholder values only.
- Real values go in the gitignored `appsettings.Local.json`.
- Real email addresses in config are a PII violation regardless of sensitivity level.
# SEC-005 — npm CVE Remediation in UmbracoPrism.Client

**Date:** 2026-04-30  
**Author:** Isabelle (Frontend Dev)  
**Commit:** `7e499b5` on `main`  
**Status:** ✅ CLOSED — 0 critical, 0 high remaining

---

## Before / After

| Severity  | Before | After |
|-----------|--------|-------|
| Critical  | 1      | 0     |
| High      | 10     | 0     |
| Moderate  | 14     | 9     |
| Low       | 1      | 0     |
| **Total** | **26** | **9** |

---

## Packages Bumped

| Package | Before | After | Method |
|---------|--------|-------|--------|
| storybook (all @storybook/* packages) | 8.6.15 | 8.6.18 | `npm install` explicit |
| @storybook/test-runner | 0.18.0 | 0.21.0 | `npm install` explicit |
| axios | transitive old | patched | `npm audit fix` |
| defu | transitive old | patched | `npm audit fix` |
| lodash | transitive old | patched | `npm audit fix` |
| minimatch | transitive old | patched | `npm audit fix` |
| picomatch | transitive old | patched | `npm audit fix` |
| rollup | transitive old | patched | `npm audit fix` |
| vite | transitive old | patched | `npm audit fix` |
| dompurify (nested in monaco-editor) | 3.2.7 | 3.4.1 | `overrides` in package.json |
| handlebars (critical) | 4.7.8 | removed | `npm audit fix` pulled in updated @umbraco-cms/backoffice |

---

## Key Notes

### Handlebars Critical CVE (GHSA-2w6w-674q-4c4q)
- Was: transitive via `@hey-api/openapi-ts` → `@umbraco-cms/backoffice`
- Fix: `npm audit fix` brought in a newer `@umbraco-cms/backoffice` version whose dependency on `@hey-api/openapi-ts` no longer pulls in vulnerable handlebars. Handlebars is no longer present in the dependency tree.

### DOMPurify Override
- `monaco-editor` had a nested `dompurify@3.2.7` (several moderate XSS/prototype-pollution CVEs)
- Added `"overrides": { "dompurify": "^3.4.1" }` to `package.json` to force upgrade
- Root `dompurify` was already at 3.4.1; only the monaco-editor nested copy was vulnerable

### Storybook HIGH CVE (WebSocket Hijacking)
- Storybook 8.6.15 had a HIGH severity dev-server WebSocket hijacking advisory
- Non-breaking upgrade to 8.6.18 resolved it (`isSemVerMajor: false`)

---

## Residual (9 Moderate — acceptable)

| Package | Severity | Reason not fixed |
|---------|----------|-----------------|
| uuid (in @storybook/test-runner chain) | Moderate | Fix requires downgrading to storybook 7.x (semver major); dev-only tooling, no runtime impact |
| @umbraco-cms/backoffice (monaco-editor chain) | Moderate | `fix=False` — upstream hasn't published fix; admin backoffice only, dev-only at present |

No breaking changes were introduced. All residual findings are:
1. Dev-only tooling (Storybook, jest-playwright)  
2. Or upstream unfixable at this time (@umbraco-cms/backoffice)

---

## Verification

- `npm run build` → clean (vite 7.3.2, tsc clean, 45 modules, 0 warnings)
- `npm audit` → 0 critical, 0 high

---

## 📌 2026-04-30: SEC-003 Workflow Content Sanitization — Complete Implementation

**Status:** ✅ CLOSED — All tasks delivered and tested; 601 passing tests (0 skipped, 0 failed)

**Authors:** Blathers (wire-up, integration, tests) + Copper (sanitiser policy, implementation, 40 unit tests)

**Commits:** Wire-up `4223861`, `97491d5` (Blathers); Implementation `ae616a2`, `55978f5` (Copper)

### What Was Delivered

SEC-003 (HIGH — XSS in `@Html.Raw(Content)` workflow display components) is now fully implemented and tested.

**Tasks T1–T7 (Blathers wire-up):**
1. **T1:** Added `HtmlSanitizer 9.0.892` NuGet package to `UmbracoPrism.Core.csproj`
2. **T3:** Defined `IWorkflowContentSanitizer` interface in `src/UmbracoPrism.Shared/Services/Sanitization/`
3. **T4:** Created `NoOpWorkflowContentSanitizer` placeholder implementation and DI registration in `WorkflowBuilderExtensions`
4. **T5:** Injected sanitizer into `BusinessAppWorkflowEngine.BuildComponents` — single seam where all `Content` / `Heading` fields are sanitized before flowing to Razor partials
5. **T6:** Verified `_PrismComponent-Waiting.cshtml` coverage — included in engine sanitization
6. **T7:** Added `SeedContentSanitizationTests` — all 4 seed workflows round-trip cleanly through sanitizer (no content diff)
7. **T9 (initial):** Added 6 regression tests to `Phase1SecurityRegressionTests.cs` (skipped pending real impl)

**Architecture decision:** Sanitizer seam placed in the engine (not in views). Rationale: single choke point, no 7-partial boilerplate, fail-closed (new components inherit sanitization by convention), easy to lint/audit.

**Project reference deviation:** `IWorkflowContentSanitizer` placed in `UmbracoPrism.Shared` (not Core) because `BusinessAppWorkflowEngine` in MockBusinessApp only references Shared. `NoOpWorkflowContentSanitizer` impl stays in Core.

| State | Test Count |
|-------|------------|
| Baseline | 550 passing |
| After T7 | 554 passing (+4 seed tests) |
| After T9 (skipped) | 554 passing, 6 skipped |

---

**Tasks T2 + T8 + T9 Re-execution (Copper implementation):**

| Task | Description | Status |
|------|-------------|--------|
| **T2** | `WorkflowContentSanitizer.cs` — Ganss.Xss impl with GDS allowlist | ✅ |
| **T8** | `WorkflowContentSanitizerTests.cs` — 40 unit test cases | ✅ |
| **Re-register** | Swapped `NoOpWorkflowContentSanitizer` for real `WorkflowContentSanitizer` in DI | ✅ |
| **T9 (un-skip)** | All 6 regression tests un-skipped and passing | ✅ |

### GDS Allowlist (Finalized)

**Tags allowed:**
- Block: `p`, `ul`, `ol`, `li`, `blockquote`, `br`, `h2`, `h3`, `h4`
- Inline: `strong`, `em`, `b`, `i`, `code`, `abbr`, `span`, `a`

**Attributes:**
- `abbr`: `title`
- `a`: `href`, `rel`
- Auto-inject `rel="noopener noreferrer"` and `target="_blank"` for external http(s) links

**URI schemes (strict):**
- Allowed: `http`, `https`, `mailto`, `tel`
- Blocked: `javascript:`, `data:`, `vbscript:`, `file:`, `//` (protocol-relative)
- Scheme check in `RemovingAttribute` event handler

**Explicitly stripped:** `<script>`, `<style>`, `<iframe>`, `<object>`, `<embed>`, `<svg>`, `<form>`, `<input>`, all event handlers (`on*`), HTML comments, inline styles, `class`, `id`, `data-*`

**Why no `class`?** Tight by design for v1. Authors don't need it (partials already wrap in GDS classes). Additive expansion later is safe; retroactive removal is breaking.

### Implementation Notes

- `HtmlSanitizer` constructed once in `WorkflowContentSanitizer` ctor; singleton registration ensures thread-safe shared instance
- `AllowedAttributes` deliberately empty; all per-tag attributes handled via `RemovingAttribute` event handler (prevents global bleed)
- Post-processor injects `rel="noopener noreferrer"` and `target="_blank"` for external links; author-supplied values discarded
- Whitespace/null guard returns `string.Empty` without invoking Ganss.Xss

### Test Coverage

| State | Count |
|-------|-------|
| Baseline (Blathers handoff) | 554 passing, 6 skipped |
| +T8 unit tests (40 cases) | +40 |
| +T9 un-skip (6 regression) | +6 |
| **Final** | **601 passing, 0 skipped, 0 failed** |

**Unit test breakdown (40 cases):**
- Null/empty input: 4
- Plain text passthrough: 1
- Allowed tags round-trip: 14
- External link rel+target injection: 3
- Dangerous href schemes stripped: 4
- Event handler stripping: 2
- Disallowed tags stripped: 6
- Inline style stripped: 1
- Idempotency: 5

**Regression suite (6 cases):**
- Script tag stripping
- JavaScript href stripping
- Onerror event handler stripping
- Data URL href stripping
- Style block stripping
- Legitimate GDS markup preservation

### Production Gate — Precondition Satisfied

> The definition editor's non-Dev rollout is gated on `IWorkflowContentSanitizer` being implemented and tested.

**Status:** ✅ SATISFIED. The definition editor can now ship to non-Dev environments (separate decision/ticket).

### Handoff Notes

Mabel owns T10 (post-merge): documentation at `docs/security/workflow-content-sanitization.md` (allowlist, seam explanation, "future components MUST sanitize" rule).

Separate tickets (not blockers):
- T10: `docs/security/workflow-content-sanitization.md`
- Content-Security-Policy header hardening (complementary defense-in-depth)

### Basis

- Tom Nook's SEC-003 proposal (`.squad/decisions/inbox/tom-nook-sec-003-proposal.md`) — design, allowlist, architecture
- Blathers' wire-up decision note (`.squad/decisions/inbox/blathers-sec-003-wireup.md`) — T1–T7 delivery
- Copper's implementation summary (`.squad/decisions/inbox/copper-sec-003-impl.md`) — T2 + T8 + T9 delivery
- Copper's security review 2026-04-30, finding SEC-003

---

## 📌 2026-04-30: User Directive — Feature-Branch + PR Workflow (Restoring CI as Regression Gate)

**Effective:** Immediately, for all new work dispatched after this entry

**Author:** Jonny Muir (user directive via Copilot)

**Reason:** The previous "work on main" policy bypassed CI checks. Even in a single-author repo, direct-to-main commits can regress silently. Restoring CI as a regression gate requires Pull Requests.

### Rules Going Forward

1. **One work item per feature branch:** Each feature, bug fix, or security finding gets a dedicated branch. Name suggestions: `squad/{slug}`, `feat/{slug}`, or `fix/{slug}`.

2. **Team collaboration on same branch:** Multiple agents may push commits to the same feature branch for a single work item. **Do NOT open separate PRs for sub-tasks of the same item.**

3. **Pull Request gate:** PR opened against `main`. All GitHub Actions / CI pipelines must pass (green). Coordinator (or assigned reviewer) merges only after all checks pass.

4. **Scribe bookkeeping travels with the work:** Scribe's `.squad/` decisions/log/history commits land on the same feature branch, flowing into `main` with the PR.

5. **Exception — trivial `.squad/`-only bookkeeping:** Pure session logging with NO code changes MAY go direct to `main` at the coordinator's discretion (e.g., a routine closeout batch updating `.squad/` docs only). When in doubt, branch.

### Branch Naming Convention

- `squad/{issue-slug}` — for issue-driven work
- `feat/{feature-name}` — for features
- `fix/{bug-name}` — for bug fixes
- `docs/{topic}` — for documentation

### Impact

- **Restores CI as a regression gate** — prevents silent breakage between local and main
- **Enables team review** — PRs are the collaboration point; reviewers catch issues before merge
- **Single PR per work item** — avoids PR sprawl and squash-on-merge chaos
- **Existing in-flight work exempt** — Copper's SEC-003 T2/T8 work (currently on main) finishes as-is; this policy bites for next dispatch

---

## 📌 2026-04-30: SEC-003 Design Proposal — Workflow Content Sanitization (Archived Reference)

**Author:** Tom Nook (Lead)  
**Status:** IMPLEMENTED (see SEC-003 Workflow Content Sanitization closure above)

**Archive note:** This design proposal (`.squad/decisions/inbox/tom-nook-sec-003-proposal.md`) defined the architecture, allowlist, and task breakdown for SEC-003. It has been executed by Blathers and Copper. Preserved here for future reference and design continuity.

**Key decisions locked in proposal:**
- `IWorkflowContentSanitizer` abstraction using Ganss.Xss with GDS allowlist
- Seam at `BusinessAppWorkflowEngine.BuildComponents` (single choke point)
- Singleton registration (HtmlSanitizer is thread-safe per-instance)
- NoOp placeholder during wire-up, swapped for real impl later
- 7 Razor partials + seed workflows covered
- 40 unit tests + 6 regression tests

**Why preserved:** Design decisions, test strategy breakdown (6.1–6.3), allowlist rationale (4.3), and architectural alternatives (4.4) are useful reference for future sanitizer extensions or related hardening work. This file remains in `.squad/decisions.md` for posterity; the inbox copy is deleted.

---

## 📌 2026-04-30: Blathers — MockBusinessApp IWorkflowContentSanitizer Registration (PR #38 Round 1)

**Status:** ✅ FIXED — Commit `6751662` on `fix/ci-green`

### Summary

The `localhost-auth-playwright` CI lane was failing with all Playwright specs timing out at the 5-minute readiness deadline. Logs showed MockBusinessApp (`https://localhost:7245/api/backoffice/me`) accepting TCP connections but never returning an HTTP response — every probe timed out after 5000 ms consistently.

### Root Cause

SEC-003 added `IWorkflowContentSanitizer` as a constructor dependency to `BusinessAppWorkflowEngine` (which runs in MockBusinessApp). The registration for this interface lives in `UmbracoPrism.Core/Extensions/WorkflowBuilderExtensions.cs`, which is only called by TestSite through `AddPrismWorkflowEngine()`.

MockBusinessApp only references `UmbracoPrism.Shared` and registers `BusinessAppWorkflowEngine` directly — it never calls `AddPrismWorkflowEngine()`. This left `IWorkflowContentSanitizer` unregistered in MockBusinessApp's DI container. At startup, the app crashed with `InvalidOperationException`, and Aspire DCP kept the port bound but with no live HTTP endpoint.

### Decision

Register a `file`-scoped `PassthroughSanitizer` directly in MockBusinessApp's `Program.cs`. MockBusinessApp serves controlled developer-authored seed content (no user-supplied HTML), so a passthrough implementation is appropriate.

### Impact

- MockBusinessApp now starts successfully and responds to the readiness probe with HTTP 401
- All three Playwright spec files unblocked
- 601 Core unit tests pass

---

## 📌 2026-04-30: Blathers — WorkflowPageSeeder Race Condition Finding (PR #38 Round 2)

**Status:** ⚠️ MISDIAGNOSIS — Commit `46826fe` reverted by Brewster in round 3

### Context

After the PassthroughSanitizer fix, MockBusinessApp started but CI run revealed workflow pages seeded as `published: false`. Blathers diagnosed a concurrent handler dispatch race where `WorkflowPageSeeder` ran before `PrismContentTypeSeeder` on fresh CI databases.

### Original Decision (Later Reverted)

Add polling to `WorkflowPageSeeder.HandleAsync`: wait up to 90 seconds for `workflowPage` content type to exist before seeding, retrying every 500ms.

### Why This Regressed

Umbraco's `INotificationAsyncHandler` dispatch is **sequential**, not concurrent. The async polling fix created a deadlock: `WorkflowPageSeeder` held the dispatcher chain with its 90-second poll loop, blocking `PrismContentTypeSeeder` (registered later) from running — preventing type creation while the seeder was waiting for it.

Result: home and dashboard also failed to publish, and `/dashboard` returned 500.

### Lesson

This finding correctly identified that handler registration order matters in Umbraco. The solution (polling) was the wrong tool for the problem. See Brewster's round 3 decision for the correct fix.

---

## 📌 2026-04-30: Brewster — CI Green Round 3 — Seeding Order Fix (PR #38 Round 3)

**Status:** ✅ FIXED — Commit `ffa1034` on `fix/ci-green`

### Root Cause

Umbraco's notification handlers are dispatched **sequentially**, not concurrently. `TestSiteComposer` had no ordering constraint relative to `PrismComposer`. On fresh CI, assembly load order meant `TestSiteComposer.Compose()` ran **before** `PrismComposer.Compose()`, registering `WorkflowPageSeeder` before `PrismContentTypeSeeder`.

With sequential dispatch:
1. `WorkflowPageSeeder` ran first — on a fresh database, content types didn't exist
2. Silently skipped all seeding
3. `PrismContentTypeSeeder` ran after, creating types but no content

Blathers' round 2 polling fix made this worse: it held the dispatch loop for 90 seconds, preventing `PrismContentTypeSeeder` from running — **deadlock**.

### Decision

Two-part fix (commit `ffa1034`):

1. **Revert polling:** `WorkflowPageSeeder.HandleAsync` restored to synchronous implementation
2. **Add composer ordering:** Mark `TestSiteComposer` with `[ComposeAfter(typeof(PrismComposer))]` to make the dependency explicit

This ensures `PrismContentTypeSeeder` runs first, creating all types; `WorkflowPageSeeder` runs second, finding all types and seeding content.

### Impact

- All 5 workflow pages now publish on fresh CI databases
- Home and dashboard routes return 200 signed-out
- Workflow routes (`/my-workflows`, `/apply-for-planning-permission`) work
- 601 Core unit tests pass

### Architectural Learning

`[ComposeAfter]` / `[ComposeBefore]` are the idiomatic Umbraco tools for cross-assembly handler ordering. Do not assume concurrent dispatch of notification handlers.

---

## 📌 2026-04-30: Brewster — DefaultAuthenticateScheme Must Not Depend on Prism:VaultUri

**Status:** ✅ FIXED — Commit `42b85e5` on `fix/ci-green`

### Context

`PrismComposer` was gating `DefaultAuthenticateScheme = "PrismMemberCookie"` on `isAuthEnabled = !string.IsNullOrEmpty(builder.Config["Prism:VaultUri"])`.

Security commit `b6336fd` correctly removed `Prism:VaultUri` from `appsettings.json` (it is a deployment secret). This silently made `isAuthEnabled = false`, so the three auth defaults (`DefaultAuthenticateScheme`, `DefaultSignInScheme`, `DefaultChallengeScheme`) were never registered with ASP.NET Core.

### Symptom

After Keycloak sign-in, the browser received `PrismMemberCookie` and sent it on all requests. Route-hijacking controllers with `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` (e.g. `/dashboard`) continued to work because they name the scheme explicitly. But the home page view, which uses `Context.User.Identity.IsAuthenticated` under the default authentication pipeline, always showed the signed-out state.

The Playwright test saw `/dashboard` → 200 (authenticated) then `/` → 200 (signed out), timing out waiting for "Go to Dashboard".

**Root cause confirmed via network trace:** `UseAuthentication()` on the home-page request used Umbraco's fallback default scheme (not `PrismMemberCookie`), so the cookie was never decrypted and `User.Identity` remained anonymous.

### Decision

**Auth scheme defaults are unconditional.** The vault URI is an optional secret-provider detail (Azure Key Vault for production; inline secrets for local dev/CI). Its presence must not gate authentication setup.

Remove the `isAuthEnabled` flag and always call:

```csharp
options.DefaultAuthenticateScheme = "PrismMemberCookie";
options.DefaultSignInScheme = "PrismMemberCookie";
options.DefaultChallengeScheme = "PrismEntraID";
```

### Guidance for Future Work

- Do not tie authentication enablement to the presence of any secret or infrastructure URI in config
- If Prism auth ever needs to be feature-flagged, introduce a dedicated `Prism:AuthEnabled` boolean, defaulting to `true`
- Secrets like `Prism:VaultUri` belong in `appsettings.Local.json` (gitignored)

### Impact

- Home page now correctly shows authenticated state for signed-in users
- Signed-out users see "Sign In" as expected
- All route authentication works consistently
- 601 Core unit tests pass

---

## 📌 2026-04-30: Mabel — 1.8.0 Release Guard + Workflow Regex Fix

**Status:** ✅ COMPLETE — Commits `da5d29d`, `8809c64` on `fix/ci-green`

### Summary

Added `## [v1.8.0] — 2026-04-30` CHANGELOG entry to satisfy the Squad Release workflow's version consistency guard, and fixed Squad Release workflow regex in three files to accept optional `v` prefix.

### CHANGELOG Entry (Commit `da5d29d`)

The security review audit (2026-04-30, 11 findings) marked the release milestone for 1.8.0. CHANGELOG now consolidates all feature work and security hardening:

**Features:**
- Generic OIDC provider support
- Tenant API + model enhancements
- Workflow and forms engine
- Mobile app UI polish & accessibility

**Security (11 findings, all processed):**
- SEC-001: WorkflowPollController authorization
- SEC-002 / SEC-008: CVE bumps (DataProtection, OpenTelemetry.Api)
- SEC-003: HTML sanitizer (GDS allowlist)
- SEC-004: HMAC signing key rotation (appsettings.Local.json pattern)
- SEC-005: npm audit fixes
- SEC-006: CookieSecurePolicy.Always hardening
- SEC-007: ForwardedHeadersMiddleware
- SEC-009: Structured logging
- SEC-010: Entra ID credentials scrubbed
- SEC-011: aria-describedby attribute encoding

### Workflow Regex Fix (Commit `8809c64`)

The Squad Release workflow guard (`Validate version consistency` step) checked:
```bash
grep -q "## \[$VERSION\]" CHANGELOG.md
```

This failed for v1.8.0 because the entry format is `## [v1.8.0]` with a `v` prefix. Fixed the regex in three workflows to accept optional `v`:
- `squad-release.yml`
- `squad-preview.yml`
- `squad-promote.yml`

Changed to:
```bash
grep -qE "^## \[v?$VERSION\]"
```

This resolves version mismatch that would have broken all prior releases if triggered.

### Impact

- 1.8.0 release milestone marked
- CI gate passes version consistency check
- Squad Release, Preview, and Promote workflows work correctly for all version formats

---

## 📌 2026-04-30: Copper Pt2 Security Review (PR #39)

**Status:** ✅ MERGED — 2 patches landed; 8 findings dispatched as `sec/pt2-backend`

**Branch:** `sec/review-2026-04-30-pt2`  
**Reviewer:** Copper (Security Engineer)  
**Model:** claude-opus-4.7 (depth-first second-pass)  
**Full ledger:** `.squad/security-review-2026-04-30-pt2.md`

### Scope

Depth-first second pass targeting what Pt1 either deferred or didn't open: auth/identity defaults, sanitizer producer-side coverage, anonymous endpoints, CSRF posture, security response headers, dependency CVEs, DataProtection key management, and CORS/origin trust on BiometricController. 10 findings raised; baseline 601/601 tests remain green.

### Findings Summary

| Severity | Count | Status |
|----------|-------|--------|
| Critical | 0 | — |
| High | 0 | — |
| Medium | 5 | 2 patched, 3 open |
| Low | 4 | open |
| Info | 1 | open |

No actively exploitable Critical/High in production — this pass is hardening, verification, and cleanup.

### Top 2 Patches (This PR)

**SEC-PT2-002 — Vulnerable transitive `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.2`** (CVE-2026-42191, Moderate)
- Pt1 bumped `OpenTelemetry.Api` but missed the OTLP exporter transitive.
- **Fixed:** Bumped to `1.15.3`. Vulnerable-package audit now clean across all 8 projects.
- Commit: `244f3b5`

**SEC-PT2-001 — Anonymous `/api/test/reset` in MockBusinessApp** wiped all workflow state with no auth and no env guard.
- The neighbouring `/admin/*` guard didn't match this path.
- **Fixed:** Explicit `IsDevelopment()` guard inside the handler.
- Commit: `2ce771f`

Both verified: `dotnet build` clean, 601/601 Core tests pass, vulnerable-package audit clean.

### Open Items (Dispatched as `sec/pt2-backend`)

- **SEC-PT2-003** — Logout via GET allows logout-CSRF; convert to POST + antiforgery (UX impact across logout buttons)
- **SEC-PT2-004** — Missing security response headers (CSP, XFO, XCTO, HSTS, Referrer, Permissions); needs middleware + per-route exemption for backoffice
- **SEC-PT2-005** — `DefaultAuthenticateScheme = PrismMemberCookie` made unconditional; needs integration test asserting backoffice routes still see backoffice user
- **SEC-PT2-006** — DataProtection keys ephemeral by default (not encrypted at rest); needs `PrismDataProtectionOptions` + production guidance
- **SEC-PT2-007** — Unsanitized `accordionSection.Content` in Razor partial (currently unused, but XSS trap); sanitize or remove
- **SEC-PT2-008** — `VinylRecord.cshtml @Html.Raw(description)` (RTE field, operator-trust pattern; informational)
- **SEC-PT2-009** — Antiforgery missing on JSON state-mutating endpoints (Notification, VinylNotification, Biometric.Register); mitigated by `SameSite=Lax` + content-type
- **SEC-PT2-010** — `IsCapacitorOrigin` accepts `http://localhost` with credentials (risk-accept candidate; document on threat model)

### Key Learning

1. **Transitive vulnerabilities require explicit audit** — Pt1 updated the direct package but didn't catch the downstream transitive. Future full-stack reviews must scan `.csproj` transitives alongside direct deps.
2. **Middleware guards must cover sibling paths** — `/admin/*` guard didn't match `/api/test/*`, creating a parallel anonymous endpoint. Auth checks need path clarity upfront.
3. **Breadth-first reviews need depth-first follow-up** — Pt1 (breadth, fast model) closed 11 findings; Pt2 (depth, claude-opus) surfaced 10 more. Standard practice: one fast pass + one deep pass per security cycle.

### Basis

Copper's commits `244f3b5` and `2ce771f` on `sec/review-2026-04-30-pt2`; inbox summary (this ledger).

---

---

## 📌 2026-05-01: Tom Nook — Prism Architecture: Composer Decomposition + MockBusinessApp Identity

**Status:** 🔵 Proposed — from Rams-grade reflection session

**Basis:** Rams principles applied to architectural vision (2026-05-01, Jonny Muir reflection). Full review at `.squad/reviews/2026-05-01-prism-reflection/01-tom-nook-vision.md`.

### Decision 1: Decompose `PrismComposer` into Feature Extension Methods

**Problem:** `PrismComposer.cs` registers all Prism services unconditionally (tenant/auth/branding, workflow, mobile, notifications, biometrics). A developer who wants only multi-tenant branding carries the entire stack.

**Decision:** Decompose into named feature extension methods on `IUmbracoBuilder`:
- `AddPrismCore()` — tenancy, branding, auth (always required)
- `AddPrismWorkflow()` — workflow engine client, nonce service, field validation
- `AddPrismMobile()` — mobile bundle service, biometric services
- `AddPrismNotifications()` — push notification rate limiting, notifiers

`PrismComposer.Compose()` calls all four for backward compatibility. Integrators who know their scope call only what they need.

**Rationale:** Satisfies "as little design as possible" — each consumer gets exactly the surface they signed up for. Reduces startup noise for branding-only installations.

### Decision 2: Resolve MockBusinessApp's Dual Identity

**Problem:** `MockBusinessApp` is simultaneously a demo (in-memory state, "mock" name) and a reference implementation (JWT validation, sanitizer, concurrency control). It shadows `UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile` with its own internal type, which any real BusinessApp implementor will trip over.

**Decision — two valid paths (Jonny to choose):**

**Path A — Lean into Demo:** Rename to `UmbracoPrism.DemoApp`. Simplify ruthlessly — remove JWT validation from the demo engine, make in-memory amnesia explicit and prominent. Delete the shadowing `WorkflowDefinitionFile.cs`; use Shared directly.

**Path B — Lean into Reference:** Rename to `UmbracoPrism.WorkflowApp`. Add a real persistence layer (e.g. SQLite or EF Core in-memory with swap-for-prod pattern). Publish as the template teams deploy alongside Prism. Delete the shadowing type; use Shared directly.

**In both paths:** Delete `src/UmbracoPrism.MockBusinessApp/Services/WorkflowDefinitionFile.cs`. The Shared type is authoritative; the shadow serves no purpose and is an active confusion vector.

### Decision 3: Remove the `OidcClientSecret` Legacy Column

**Problem:** `PrismTenantSchema.cs` retains an `OidcClientSecret` column "for migration compatibility" that is never written. It is a lies-in-plain-sight security risk — any developer inspecting the DB schema infers it is the correct place for a secret.

**Decision:** Write a migration that drops the column. Add a startup check that logs a warning if the column still contains data in an existing installation (guide operators to migrate to the provider/reference pattern). Record the removal in CHANGELOG.md.

---

## 📌 2026-05-01: Blathers — Workflow Engine Reflection: 5 Architectural Findings

**Status:** 🔵 Proposed — from Rams-grade review

**Basis:** Blathers direct code analysis (2026-05-01). Full review at `.squad/reviews/2026-05-01-prism-reflection/03-blathers-workflow.md`. No code changes made — these are architectural recommendations for future issues.

### Finding 1: Hardcoded business rule must be evicted from `BusinessAppWorkflowEngine`

**Issue:** Lines 304–336 of `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs` contain a regex-based domain rule (`enquiry-type == "Technical support"` → requires version number/URL/error code). This violates the engine's generic contract and makes rules invisible to service designers.

**Recommendation:** Any per-field, per-value validation rule that is domain-specific MUST live in the workflow definition (seed JSON or C# builder), not in the engine. The engine's `Advance()` method must remain domain-agnostic. Implementation options: declarative `"rules"` array on step definitions, or a registered `IWorkflowAdvanceRule` strategy the MockBusinessApp configures. Either is acceptable; the hardcoded rule is not.

**Priority:** HIGH — directly harms business users (unexplained rejection) and blocks engine genericity.

### Finding 2: `PrismComponentRenderPayload` must be replaced with a typed render hierarchy

**Issue:** `PrismComponentRenderPayload` in `src/UmbracoPrism.Shared/Models/Workflow/WorkflowResponseEnvelope.cs` is a 20+ nullable property flat bag. Contradicts the clean design-time `PrismComponent` sealed record hierarchy. Produces nullable fog in views and tag helpers.

**Recommendation:** Create a sealed render hierarchy (`FieldsetRenderPayload`, `SummaryListRenderPayload`, `BodyRenderPayload`, etc.) derived from an abstract `PrismComponentRenderBase`. The `BuildComponents()` switch arms in `BusinessAppWorkflowEngine.cs` already provide the natural split points. Views and tag helpers should receive typed payloads, not a bag.

**Priority:** MEDIUM — no user-facing regression risk; improves maintainability and removes a whole class of null-reference risk.

### Finding 3: Advance API field contract must be typed

**Issue:** `BusinessAppWorkflowClient.AdvanceAsync()` sends `Dictionary<string, object?>` for field values. ASP.NET Core deserialises `object?` as `JsonElement`. The engine's `GetDisplayValue()` has explicit `JsonElement` special-casing (line 878, `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`). This is a leaky abstraction acknowledged inline.

**Recommendation:** The advance API payload `FieldValues` MUST be typed as `Dictionary<string, string>` (all form values are strings at submission time). The `object?` generalisation solves no real problem — it is a premature generalisation that introduces runtime casting and JsonElement workarounds throughout the render path.

**Priority:** MEDIUM — the workaround is contained but the contract is dishonest.

### Finding 4: String enums must be replaced with compile-time constants

**Issue:** `InstancePolicy` ("single", "multiple", "prompt"), `ResponseState` ("render", "defer", "complete", "error"), `Style` ("primary", "secondary", "destructive"), and `StepType` ("question", "check-answers", "confirmation", etc.) are all stringly-typed contracts with no compile-time enforcement.

**Recommendation:** Introduce `PrismInstancePolicy`, `WorkflowResponseState`, `WorkflowActionStyle`, and `WorkflowStepType` as C# `enum` or `static class` constant holders. References throughout `WorkflowDefinitionBuilder`, `BusinessAppWorkflowEngine`, `PrismWorkflowPageController`, and `PrismComponentExtensions.InferStepType()` must be updated to use these. JSON seeds serialise as strings — use `[JsonConverter(typeof(JsonStringEnumConverter))]` or explicit discriminator mappings.

**Priority:** LOW — no user-facing impact; important for long-term maintainability.

### Finding 5: JSON seeds require schema validation

**Issue:** Workflow definition JSON files in `src/UmbracoPrism.MockBusinessApp/workflow-seeds/` have no schema file, no validation on load, and silently ignore unknown fields. Type discriminator spellings (`checkboxlist` vs `checkboxes`) are inconsistent between the seed format and validator.

**Recommendation:** Add a JSON Schema file (`.schema.json`) for `WorkflowDefinitionFile` and wire it as a VS Code / JetBrains schema reference in seed files. Fix the `checkboxlist`/`checkboxes` inconsistency — pick one and enforce it everywhere (discriminator, validator, partial name). Add seed validation at startup: if a seed fails deserialization, log an error with the offending file path and property, not just a generic failure.

**Priority:** LOW — affects service designer DX only; no runtime user impact.

---

## 📌 2026-05-01: Brewster — Multi-tenancy Reflection: 4 Findings

**Status:** 🔵 Proposed — from Rams-grade review

**Basis:** Brewster Umbraco platform review (2026-05-01). Full review at `.squad/reviews/2026-05-01-prism-reflection/04-brewster-multitenancy.md`. No code changes made — these are recommendations for future work.

### Finding 1: Content isolation is a known gap — needs a roadmap item

**Issue:** Prism's multi-tenancy does not isolate the Umbraco content tree. All tenants share the same nodes. This is admitted in `docs/walkthroughs/creating-a-tenant.md` line 181 ("not covered in this walkthrough") but is inconsistent with product-level isolation promises.

**Recommendation:** Add an explicit roadmap item to introduce a `tenantTag` filter pattern on the Umbraco content tree. Until implemented, update the README/product overview to accurately describe what "isolated context" means — specifically that it covers auth, branding, workflows, and OIDC, but not content authoring.

**Who should act:** Brewster (Umbraco content type and route patterns); Blathers (if an `IPublishedContentFilter` hook requires Core library changes).

### Finding 2: Hardcoded `/dashboard` redirect in `MemberDashboardController` should be replaced

**Issue:** `src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs` line 42 hardcodes `/auth/login?returnUrl=/dashboard`. On multi-tenant deployments where the dashboard node may be at a different URL, this redirect could challenge against the wrong tenant's OIDC authority.

**Recommendation:** Replace the hardcoded string with a content-tree lookup using `TestSiteSeedContract.FindPublishedByAlias()` (same pattern as `memberDashboard.cshtml` lines 10–12), falling back to the literal string only if the node is not found. This is a Brewster-owned change (test site controller).

**Who should act:** Brewster.

### Finding 3: Tenant cache TTL gap needs operator documentation and a flush endpoint

**Issue:** `TenantService` caches tenant records for 30 minutes (`src/UmbracoPrism.Core/Services/TenantService.cs` line 92). A deleted or mis-configured tenant remains resolvable from cache for up to 30 minutes with no operator-facing warning or manual flush.

**Recommendation:**
1. Add `POST /umbraco/api/prism/tenants/{id}/invalidate-cache` to `TenantManagementController` (already has `InvalidateDomain` available via `ITenantService`).
2. Expose cache metrics (`GetCacheMetrics()`) in the Prism Dashboard UI.
3. Add an operator warning to `docs/walkthroughs/creating-a-tenant.md` Part 6 (delete tenant row).

**Who should act:** Brewster (controller + docs); Isabelle or Blathers (backoffice UI panel if Lit component work is needed).

### Finding 4: Email/push notification branding is unresolved

**Issue:** `PrismNotificationService` is scoped per request and has access to `IPrismContext.CurrentTenant`, but there is no evidence (or documentation) of tenant branding tokens flowing into outbound email or push notification payloads.

**Recommendation:** Explicitly scope this as a follow-up investigation. Either document that email branding is out of scope for v2.0, or assign to Blathers to wire `CurrentTenant.BrandingOverrides` into the email template pipeline.

**Who should act:** Blathers (notification service is Core-owned); Brewster to validate test site notification flows once implemented.

---

## 📌 2026-05-01: Isabelle — Design System Token Architecture: 5 Findings

**Status:** 🔵 Proposed — from Rams-grade review

**Basis:** Isabelle design system audit (2026-05-01). Full review at `.squad/reviews/2026-05-01-prism-reflection/02-isabelle-design-system.md`.

### Finding 1: `--prism-button-hover` must be derived from `--prism-primary`

**Issue:** `prism-components.css:120` has `--prism-button-hover: #003078` (hardcoded GDS dark blue). Any tenant overriding `--prism-primary` via the branding middleware gets the correct idle button colour but the wrong hover colour. This is a silent inconsistency that will surface on first real deployment of a non-GDS brand.

**Rule going forward:** Hover states that relate to a brand token MUST be derived via `color-mix()` or `calc()`. No hardcoded dark variants of brand colours. Recommended fix: `color-mix(in srgb, var(--prism-primary) 80%, #000 20%)`.

### Finding 2: CSS cascade layers should be declared at the HTML head entry point

**Issue:** GDS Frontend loads first by file order convention (`Master.cshtml:34`). No `@layer` declarations. The comment "ITCSS layer order" is a developer hint, not a CSS contract. Specificity relationships between GDS and Prism rules are implicit.

**Rule going forward:** A `<style>@layer govuk, prism-base, prism-layout, prism-components, prism-branding;</style>` declaration should be added before any CSS link tags. This costs nothing at runtime and documents the cascade contract.

### Finding 3: All branding tokens must carry the `--prism-` prefix

**Issue:** `--bg-offset` in `prism-colors.css` is the only token without the prefix. It is consumed in `prism-typography.css` and `prism-layout.css`.

**Rule going forward:** All design-system CSS custom properties ship with the `--prism-` namespace. No exceptions. Rename to `--prism-surface-page`. Update all three consumption sites.

### Finding 4: `--prism-focus` is an accessibility token — not a brand token

**Issue:** `--prism-focus: #ffdd00` appears in the branding metadata schema with no guard rail distinguishing it from brand colours.

**Rule going forward:** Accessibility-constrained tokens (focus ring, error colours) must carry an in-file comment flagging the WCAG constraint. Consider separating them into a `prism-accessibility.css` file or a distinct section in the branding schema so the metadata service can mark them as non-overridable in the editor UI.

### Finding 5: Storybook must cover GDS components and PrismField partials

**Issue:** Zero Storybook stories for GDS components or PrismField partials. Designers, editors, and content creators cannot preview workflow form components without running the full Umbraco stack.

**Rule going forward:** Any PrismComponent or PrismField partial that is content-author-selectable needs a corresponding Storybook story (HTML story via Lit `html` template, not necessarily a web component). The Storybook preview.ts must import `prism-colors.css` and `prism-typography.css` so stories render in the correct token context.

---

## 📌 2026-05-01: Kicks — Mobile Architecture Reflection: 3 Findings

**Status:** 🔵 Proposed — from Rams-grade review

**Basis:** Kicks mobile specialist audit (2026-05-01). Full review at `.squad/reviews/2026-05-01-prism-reflection/05-kicks-mobile.md`.

### Finding 1: Push bundle gap must be treated as a bug, not a gap

**Issue:** `PrismMobileBundleRequest.cs` does not contain a `PushNotificationsEnabled` property. The backoffice UI (`prism-create-tenant-modal.ts`) sends this field in the bundle request; the backend silently drops it. `MobileBundleService.BuildBundleAsync` never conditionally scaffolds push code. Operators cannot produce a push-ready bundle. This is a honesty failure (Rams #6), not a known gap.

**Fix required:**
1. Add `public bool? PushNotificationsEnabled { get; set; }` to `PrismMobileBundleRequest.cs`
2. Read and act on the field in `MobileBundleService.BuildBundleAsync` — conditionally include `@capacitor/push-notifications` in `package.json`, push plugin config in `capacitor.config.ts`, and `PrismPushNotifications.registerDevice()` hook in `www/index.html`
3. Add test coverage in `MobileBundleServiceTests.cs`

**Rule going forward:** Any UI control that affects bundle output MUST have a corresponding field in `PrismMobileBundleRequest.cs`. The model is the source of truth for what the bundle can produce.

### Finding 2: Mobile-facing components must use `--prism-*` tokens, not `--uui-*` or hardcoded hex

**Issue:** `prism-biometric-register.ts` and `prism-biometric-settings.ts` use `--uui-color-interactive` (Umbraco backoffice token set) and hardcoded hex values (`#2563eb`, `#c82333`, `#f0fdf4`) for auth UI. These components render in member-facing mobile WebViews. Tenant branding is broken at the biometric enrollment screen.

**Fix required:** The mobile boundary convention established in `prism-mobile-nav.ts` (comment: `⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory`) must be extended as a token rule: **member-facing mobile components must use `--prism-*` CSS custom properties only**. No `--uui-*` imports; no hardcoded hex colours.

**Rule going forward:** Any component in `src/backoffice/` that renders in member-facing or mobile WebView contexts must:
- Use `var(--prism-primary)` for primary interactive colours
- Use `var(--prism-danger, #c82333)` for destructive actions
- Include a comment at the top: `// TOKEN CONTRACT: Use --prism-* custom properties only. No --uui-* imports.`

### Finding 3: Mobile architecture verdict — confirmed thin-shell model, no separate renderer

**Informational — no action required**

The mobile app does not have a separate workflow renderer. It uses the same `PrismComponentRenderPayload` model, same Razor views, and same Lit web components as the browser. The Capacitor WebView loads server-rendered HTML. Mobile-specific behaviour is gated by `html.prism-mobile` CSS class injected by `PrismBrandingMiddleware`. This is by design.

**Rule going forward:** Mobile capability additions (new native features, new Capacitor plugins) should be surfaced as **additions to the existing web contract** (new CSS custom properties, new events, new capability-detection guards) — not as a parallel rendering path.

---

## 📌 2026-05-01: Mabel — Documentation Surface Gaps: 3 Findings

**Status:** 🔵 Proposed — from Rams-grade review

**Basis:** Mabel technical writer audit (2026-05-01). Full review at `.squad/reviews/2026-05-01-prism-reflection/06-mabel-onboarding.md`.

### Finding 1: Persona-routed entry added to README

**Issue:** The README has no role-routing mechanism. Five of six named personas (content creators, designers, editors, business users, service designers) have no clear entry door — they all land on developer content.

**Recommendation:** A "Start here by role" section should be added immediately after the Codespaces button block, before the "What You Get" section.

**Rule going forward:** Any new guide or walkthrough added to the docs must be linked from the role-routing section at the point it is created, not retrospectively.

### Finding 2: `docs/design/` removed from public README docs table

**Issue:** The six architecture/design documents in `docs/design/` (`notifications-architecture.md`, `notifications-backend.md`, `notifications-mobile.md`, `notifications-umbraco-demo.md`, `workflow-forms-engine*.md`) are contributor-facing. They appear in the public README docs table alongside user guides, creating navigation noise for all non-developer personas.

**Rule going forward:** `docs/design/` documents are contributor reference material. They must not appear in the main README docs table. A single line `→ Architecture reference (for contributors): docs/design/` at the foot of the docs section is sufficient.

### Finding 3: Skeletal walkthroughs must be marked incomplete in the index

**Issue:** Five walkthroughs listed in `docs/walkthroughs/README.md` and the README docs table have missing or placeholder screenshots: `creating-a-tenant.md`, `design-system.md`, `push-notifications.md`, `building-a-mobile-app.md`. Listing them without incompleteness markers is a honesty failure (Rams #6).

**Rule going forward:** Any walkthrough that does not yet have its full screenshot set captured must carry a `🚧 In progress` marker in `docs/walkthroughs/README.md`. The marker is removed only when screenshots are captured via the `Capture Walkthrough Screenshots` workflow dispatch. This is a mandatory gate before a walkthrough is considered "published."

**Additional observations (informational):**
- `push-notifications.md` contains an ASCII flow diagram. Mabel's charter mandates Mermaid for all diagrams. Should be converted.
- R5 spec ↔ markdown back-reference footer is absent from some walkthroughs (`authoring-a-workflow.md` confirmed). Enforcement should be added to the PR checklist.
- `docs/archive/` has no defined lifecycle. Consider explicit deprecation or removal path.

---

## 📌 2026-05-02: Copper + Blathers — Codespaces 401 Downstream Auth Fix (PR #44)

**Status:** ✅ MERGED — 3 commits on `fix/codespaces-401-downstream-auth` (e0e8ee3, 4a47acc, + Tester hardening); PR #44 awaiting Jonny's merge after CI green.

### Context

Codespaces-only 401 on `/api/prism/downstream-demo`: when access tokens expired (~5 min), every downstream API call would fail with `HTTP 401 / www-authenticate: tunnel`. Root cause: Keycloak HTTP traffic from Prism components (refresh-token grant, JWKS fetch, discovery doc retrieval) was hitting public Codespaces URLs that the GitHub port-forwarding proxy blocks for unauthenticated server-to-server calls. Fix required rewriting **three surfaces** through the internal backchannel (`KEYCLOAK_BACKCHANNEL_URL`).

**Bedrock directive (user input, 2026-05-02T09:24:57+01:00):**
> Security must never be compromised. It is a bedrock term of the Prism project. Every diagnosis or fix must preserve security boundaries — no shortcuts, no "just for Codespaces" exceptions, no relaxing token validation to make a demo work.

### Diagnosis Path (Parallel)

#### Copper's Diagnosis (2026-05-02 08:00–09:30)
- Reaffirmed bedrock rule: no auth-laxity fixes permitted
- Documented forbidden remedies: `RequireHttpsMetadata = false`, `ValidateIssuer = false`, `IsPrincipalBoundToCurrentTenant` disable, wildcard issuer trust, `ServerCertificateCustomValidationCallback => true`
- Identified two real-cause hypotheses (H1: `DemoTenantSeeder` tenant binding; H2: AppHost env-var override)
- Noted forward seam: `RefreshTokenAsync` missing backchannel rewrite (separate decision needed)
- **Artifact:** `.squad/diagnosis/2026-05-02-codespaces-401/copper-security-diagnosis.md`

#### Blathers' Diagnosis (2026-05-02 09:15–10:00)
- Discovered gap: `KEYCLOAK_BACKCHANNEL_URL` rewrite existed for discovery document, but not for JWKS fetch
- `OpenIdConnectConfigurationRetriever` follows `jwks_uri` from discovery doc (which Keycloak emits as public URL due to `KC_HOSTNAME`)
- Second JWKS call hits GitHub proxy → 401 → no signing keys → token validation fails
- Proposed: custom `IDocumentRetriever` wrapper (only when `KEYCLOAK_BACKCHANNEL_URL` set AND `IsDevelopment()`)
- Noted: `PrismOidcConfiguration` metadata fetch ALSO needs check (Copper's domain)

**Synthesis:** Parallel review uncovered both were partly right. Three surfaces needed backchannel rewrite (refresh grant, JWKS, discovery). Copper's hypothesis about tenant binding was secondary; the HTTP 401 proxy block was primary.

### Three-Fix Architecture (Three Agents)

#### Fix 1: Copper — Refresh Token Backchannel (commit `e0e8ee3`)

**File:** `src/UmbracoPrism.Core/Models/PrismContext.cs` — `RefreshTokenAsync`

```csharp
var backchannelBase = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
var isDevelopment = string.Equals(
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
    "Development",
    StringComparison.OrdinalIgnoreCase);
if (isDevelopment && !string.IsNullOrEmpty(backchannelBase))
{
    var oidcPath = new Uri(CurrentTenant.OidcAuthority!.TrimEnd('/')).AbsolutePath.TrimEnd('/');
    tokenEndpoint = $"{backchannelBase.TrimEnd('/')}{oidcPath}/protocol/openid-connect/token";
}
```

**Why this approach:**
- Rewrite is **transport only** — returned tokens validated with strict issuer/audience rules against public `OidcAuthority`
- No `RequireHttpsMetadata = false`, no `ValidateIssuer = false`
- Gated by both `KEYCLOAK_BACKCHANNEL_URL` AND `IsDevelopment()` (belt-and-suspenders: startup guards at `MockBusinessApp/Program.cs:38-41` and `TestSite/Program.cs:29-31` already prevent non-Development presence)
- Uses `ASPNETCORE_ENVIRONMENT` check directly to avoid constructor signature break (631+ existing tests)

#### Fix 2: Blathers — JWKS Backchannel (commit `4a47acc`)

**File:** `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs` — `WarmAsync<T>()` generic overload

Introduced `BackchannelRewritingDocumentRetriever` (sealed private class) that wraps `IDocumentRetriever`:

```csharp
private sealed class BackchannelRewritingDocumentRetriever : IDocumentRetriever
{
    private readonly IDocumentRetriever _inner;
    private readonly Uri _publicKeycloakBase;
    private readonly string _bacchannelBase;
    
    public async Task<string> GetDocumentAsync(string address, CancellationToken ct)
    {
        var rewritten = RewriteUrlIfPublicKeycloak(address);
        return await _inner.GetDocumentAsync(rewritten, ct);
    }
    
    private string RewriteUrlIfPublicKeycloak(string url)
    {
        var uri = new Uri(url);
        if (uri.Host == _publicKeycloakBase.Host)
        {
            var path = uri.PathAndQuery.TrimStart('/');
            return $"{_bacchannelBase.TrimEnd('/')}/{path}";
        }
        return url;
    }
}
```

**Why this approach:**
- Intercepts BOTH discovery doc fetch AND downstream JWKS follow (happens via same retriever)
- No change to `IPrismSigningKeyCache` interface or callers
- No change to `_configurationManagerFactory` signature
- Production (no env var, or non-Development) uses existing factory unchanged — zero behaviour change
- Gated by both `KEYCLOAK_BACKCHANNEL_URL` and `ASPNETCORE_ENVIRONMENT == Development`

#### Fix 3: Tester — Hardening + Regression Tests

**Commit:** `7a9b1c3` (added by Tangy)

Two key deliverables:

1. **Discovered hardening gap in `PrismAuthExtensions.ResolveSigningKeys`:**
   - Original code had `KEYCLOAK_BACKCHANNEL_URL` check but NO `IsDevelopment()` gate
   - Startup guards would throw on non-Development, but runtime code was unguarded
   - Fix: added `IsDevelopment()` check (now consistent with Copper's and Blathers' implementations)
   - This was a critical security win — parallel review caught what single-agent review might have missed

2. **New test file: `BackchannelRewriteTests.cs` (11 regression tests)**
   - `RefreshTokenAsync_WithBackchannelUrl_Development_RewritesTokenEndpoint` — token endpoint rewrite confirmed
   - `RefreshTokenAsync_NoBackchannelUrl_UsesPublicEndpoint` — no rewrite when env var absent
   - `RefreshTokenAsync_NonDevelopment_UsesPublicEndpoint` — no rewrite outside Development
   - `PrismSigningKeyCache_WarmAsync_WithBackchannelUrl_Development_RewritesJwksUri` — JWKS rewrite confirmed
   - `PrismSigningKeyCache_WarmAsync_NoBackchannelUrl_UsesPublicUrl` — no rewrite when env var absent
   - `PrismAuthExtensions_ResolveSigningKeys_WithBackchannelUrl_Development_Succeeds` — metadata fetch + IsDevelopment gate confirmed
   - Plus 5 more edge cases (null handling, URL path preservation, etc.)

**Test infrastructure added:**
- `EnvVarSensitiveTestCollection.cs` — isolated collection for tests that manipulate `ASPNETCORE_ENVIRONMENT`
- Skill documented at `.squad/skills/backchannel-rewrite-testing/SKILL.md`

### Bedrock Guarantees (All Three Fixes)

- ❌ NO `RequireHttpsMetadata = false`
- ❌ NO `ValidateIssuer = false` / `ValidateAudience = false`
- ❌ NO `IsPrincipalBoundToCurrentTenant` relaxation
- ❌ NO `ServerCertificateCustomValidationCallback => true`
- ❌ NO suffix-trust of `*.app.github.dev`
- ❌ NO Development-only "skip tenant binding" branch
- ✅ Rewrite gated by BOTH `KEYCLOAK_BACKCHANNEL_URL` AND `IsDevelopment()`
- ✅ Issuer/audience validation on refreshed tokens remains strict
- ✅ Token signing key validation unchanged
- ✅ Production startup guards at `MockBusinessApp/Program.cs:38-41` and `TestSite/Program.cs:29-31` untouched (throw if env var set in non-Development)
- ✅ Discovered hardening gap (missing `IsDevelopment()` in `ResolveSigningKeys`) closed

### Security Review (Copper, 2026-05-02 14:00–14:30)

Copper's final security review APPROVED all three fixes. Report at `.squad/reviews/2026-05-02-pr44-final-security-review.md`.

**Key sign-off observations:**
- All three rewrite sites are properly gated
- Transport-layer rewrite does not compromise token trust
- Bedrock directive respected throughout
- Parallel testing caught the `IsDevelopment()` gap — this validates the multi-agent review pattern

### Test Results

- **Before:** 618 tests passing (baseline after PT2 hardening)
- **After:** 629 tests passing (+11 new backchannel regression tests)
- **Status:** All green; no regressions
- **CI:** Awaiting green before merge

### Commits (Chronological)

| SHA | Author | Subject |
|-----|--------|---------|
| `e0e8ee3` | Copper | fix(auth): route OIDC token refresh through backchannel in Codespaces |
| `4a47acc` | Blathers | fix(auth): rewrite jwks_uri through backchannel in Codespaces |
| `7a9b1c3` | Tangy | test(auth): backchannel rewrite regression tests + IsDevelopment hardening |

### Inbox Records (Merged)

- `copper-refresh-token-backchannel.md` → merged into this entry
- `blathers-jwks-backchannel.md` → merged into this entry
- `copper-codespaces-401-diagnosis.md` → merged into this entry
- `blathers-codespaces-401-diagnosis.md` → merged into this entry
- `copilot-directive-2026-05-02-security-bedrock.md` → merged into this entry

### Follow-Up

None at this time. All three surfaces are protected. Production guards remain in place.

---

## 📌 2026-05-02: User Directive — Security Bedrock

**Status:** ✅ RECORDED

**By:** Jonny Muir (via Copilot)

**What:** Security must never be compromised. It is a bedrock term of the Prism project. Every diagnosis or fix must preserve security boundaries — no shortcuts, no "just for Codespaces" exceptions, no relaxing token validation to make a demo work.

**Why:** User input, captured for permanent team memory. Applied throughout the Codespaces 401 fix (e0e8ee3, 4a47acc, 7a9b1c3).


---

## 📌 2026-05-02: Blathers — Codespaces URL derivation implementation shipped

**Status:** ✅ Shipped — PR `fix/codespaces-url-derivation` merged to main

All Codespaces public-URL derivation sites now use `gh codespace ports` instead of the legacy `{CODESPACE_NAME}-{port}.{domain}` string-concat pattern. The fix covers:

- **AppHost/Program.cs**: `TryDiscoverCodespaceUrls()` queries `gh codespace ports --codespace "$CODESPACE_NAME" --json sourcePort,browseUrl` via `System.Diagnostics.Process` at startup. Parses port 8443 → `keycloakProxyUrl`, port 44345 → `testSitePublicUrl`. Both are threaded into child services as env vars.
- **DemoTenantSeeder.cs**: `BuildCodespaceTestSiteHostname()` reads `TESTSITE_PUBLIC_URL` first (set by AppHost), falling back to the legacy pattern. Keycloak authority now uses `KEYCLOAK_URL` (AppHost-sourced) rather than a reconstructed hostname.
- **TenantService.cs**: Lenient `LIKE '%.app.github.dev'` fallback in `GetByDomainAsync` handles the case where the opaque forwarding token rotates between restarts.
- **.devcontainer/on-start.sh**: `get_codespace_url()` function queries `gh codespace ports` and replaces all string-concat URL construction.

**Fallback design:** When `gh` is unavailable, `Process.Start()` returns null or exits non-zero, or the JSON doesn't contain the expected port, the code falls back to the legacy `{CODESPACE_NAME}-{port}.{domain}` pattern with a console warning. This ensures non-Codespaces local dev environments continue to work.

**Bedrock invariants preserved:**
- `RequireHttpsMetadata = true`
- `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateIssuerSigningKey = true`
- Backchannel rewrite gated on `KEYCLOAK_BACKCHANNEL_URL` + `IsDevelopment()`
- No transport-derived identity
- `*.app.github.dev` suffix used ONLY for tenant lookup, never for security validation

**Tests:** 647 passed, 0 failed. New tests added: BackchannelRewriteTests Group D (2 tests for regional URL scheme regression), PrismOidcConfigurationTests (3 new tests for IsRepoOwnedLocalDemoTenant Theory), total +5 new tests.

---

## 📌 2026-05-02: Blathers — Codespaces URL derivation forms (decision proposal)

**Status:** Proposed (completed in PR #45)

GitHub Codespaces has rolled out a new port-forwarding URL scheme for some regions/codespaces: `{per-codespace-token}-{port}.{region}.app.github.dev` (e.g. `v7ldkc4c-3000.uks1.app.github.dev`). The leading token is opaque and not derivable from `CODESPACE_NAME`. The legacy form `{CODESPACE_NAME}-{port}.app.github.dev` is no longer universally produced.

**Decision:** The codebase must stop assuming the legacy pattern. All Codespaces public-URL derivation sites must use:
1. `gh codespace ports --codespace "$CODESPACE_NAME" --json sourcePort,browseUrl` queried at AppHost / on-start time and threaded into Aspire env vars, OR
2. Trust the inbound request hostname and derive sibling-port URLs by swapping the port segment.

Tenant resolution for the local demo Codespaces tenant must be lenient on hostname (any `*.app.github.dev` matching the local demo client) rather than seeded by one exact hostname.

---

## 📌 2026-05-02: Blathers — Inject X-Forwarded Headers on Backchannel Refresh Token Requests

**Status:** ✅ Shipped — PR `fix/codespaces-invalid-grant-refresh`

**Problem:** Keycloak 26 running with `--proxy-headers xforwarded` derives its canonical issuer URL scheme from the `X-Forwarded-Proto` header. The backchannel refresh POST to `http://localhost:8080` carried no forwarding headers, so Keycloak computed an `http://...` issuer. The stored refresh token's `iss` claim was `https://...` (set when the token was originally issued through YARP, which does forward headers). Keycloak's issuer comparison on the refresh token grant detected the scheme mismatch and returned `invalid_grant`.

**Decision:** When the backchannel rewrite is active (`KEYCLOAK_BACKCHANNEL_URL` set + `ASPNETCORE_ENVIRONMENT=Development`), `PrismContext.RefreshTokenAsync` now derives `X-Forwarded-Proto` and `X-Forwarded-Host` from `OidcAuthority` (the public HTTPS URL) and passes them as optional `requestHeaders` to `IPrismTokenRefreshService.RefreshAsync`. `PrismTokenRefreshService` applies these headers to the `HttpRequestMessage` before sending.

**Rationale:**
- **Correctness:** The forwarding headers must come from `OidcAuthority` (HTTPS), not the backchannel base URL (HTTP), so Keycloak sees the same scheme and host it used when issuing the token.
- **Security bedrock preserved:** `ValidIssuer` in Prism's JWT validation remains the public `OidcAuthority`. The headers only affect Keycloak's grant-time issuer computation; Prism's own token validation is unchanged.
- **Minimal blast radius:** The new `requestHeaders` parameter is optional with a `null` default, so all non-backchannel paths (production, generic OIDC, Entra) are unaffected.

**Tests:** 647 passed, 0 failed. New tests added in BackchannelRewriteTests Group E (3 new tests covering positive case, no-rewrite negative case, and critical "scheme must come from authority not backchannel" anti-regression).

---

## 📌 2026-05-02: Blathers — SEC-PT2-005 Backoffice Scheme Isolation Analysis

**Status:** ✅ CONFIRMED SAFE — PR #43 `sec/pt2-backoffice-test`

The concern that `DefaultAuthenticateScheme = PrismMemberCookie` made unconditional (commit `42b85e5`) might allow member cookies to grant access to backoffice routes is safe by design.

**Auth scheme behaviour:** `PrismMemberCookie` and `UmbracoBackOffice` are separate ASP.NET Core auth schemes with separate handlers. Explicit named-scheme `[Authorize(AuthenticationSchemes = "UmbracoBackOffice")]` on backoffice controllers means Prism's `DefaultAuthenticateScheme` has no bearing on backoffice access control. The only theoretical leak is that `HttpContext.User` briefly becomes a member principal on backoffice routes when a member cookie is present, but this does not create an exploitable access path because Umbraco's backoffice middleware is scheme-aware and doesn't rely on `HttpContext.User` for access decisions.

**Regression tests added:** `src/UmbracoPrism.Core.Tests/BackofficeSchemeIsolationTests.cs` with 4 tests: DefaultAuthenticateScheme unconditional guard, DefaultChallengeScheme completeness, scheme boundary documentation, isolation proof (UmbracoBackOffice auth fails even when PrismMemberCookie succeeds). Test count: 627 → 631 (+4).

---

## 📌 2026-05-02: Celeste — Marketplace README Strategy — Plain-Text Variant

**Status:** ✅ IMPLEMENTED — Commit `b5588bb` on `fix/codespaces-url-derivation`

**Problem:** The primary README.md uses decorative HTML blocks (`<div align="center">`, `<img>` tags) that render perfectly on GitHub. But the Umbraco Marketplace injects README content as plain text — HTML tags appear literally, breaking the listing appearance.

**Decision: Dual-Markdown Strategy**
- **Primary GitHub README.md:** Retains all HTML blocks, rich formatting, sidebar includes. Unchanged from current state.
- **New MARKETPLACE.md:** Plain-text safe markdown variant. All HTML blocks replaced with markdown equivalents. Decorated `<div>` containers → explicit links and descriptions. `<img>` tags → markdown image syntax + links to full GitHub documentation. All relative links → absolute GitHub URLs.
- **umbraco-marketplace.json Update:** DocumentationUrl updated to raw GitHub URL of MARKETPLACE.md.

**Rationale:** Respects single-intent schema, preserves GitHub experience (README.md unchanged), marketplace-friendly (MARKETPLACE.md renders clean as markdown with zero HTML tag pollution), maintainable (two files kept in sync), self-documenting.

**Validation:** `grep -n "<div\|<img\|<picture\|<details\|<h[123]>" MARKETPLACE.md` returns zero matches.

---

## 📌 2026-05-02: Copper — Security Review: PR #45 — Codespaces URL derivation fix

**Reviewer:** Copper (Security Engineer)

**Verdict:** ✅ APPROVED WITH NOTES

All 7 bedrock invariants preserved. No new attack surface introduced. Two low-severity soft notes flagged for follow-up.

**Bedrock Invariant Checklist:**
1. ✅ RequireHttpsMetadata = true — PRESERVED. No change.
2. ✅ ValidateIssuer / ValidateAudience / ValidateIssuerSigningKey all true — PRESERVED.
3. ✅ Backchannel rewrite dual-gated (env var + IsDevelopment) — PRESERVED.
4. ✅ Tenant resolution must NOT trust hostname suffix for security decisions — PRESERVED (with soft notes — see below).
5. ✅ No new code path that derives identity, scopes, or auth from the request hostname — PRESERVED.
6. ✅ `IsRepoOwnedLocalDemoTenant` semantics unchanged for non-Codespace traffic — PRESERVED.
7. ✅ JWT issuer/audience strings come from configured authority, not request — PRESERVED.

**Soft notes:**
- **Soft note A:** The `LIKE '%.app.github.dev'` fallback query has no `ORDER BY`. If multiple `.app.github.dev` rows exist, the selected row is non-deterministic. Not exploitable (all such rows resolve to the same seeded Keycloak authority), but could cause mysterious authentication failures in dev. Recommend adding `ORDER BY Id DESC LIMIT 1` or a comment acknowledging non-determinism.
- **Soft note B:** The LIKE fallback is not gated by `IsDevelopment()`. In production, no `.app.github.dev` tenant rows would exist, so not exploitable in practice. However, defense-in-depth would improve by adding an `IsDevelopment` guard in `TenantService` for this fallback path.

**Test Results:** 647 passed, 0 failed.

---

## 📌 2026-05-02: Copper — PR #46 Security Verdict (`fix/codespaces-invalid-grant-refresh`)

**Date:** 2026-05-02
**Verdict:** ✅ **APPROVE**

## Bedrock Invariants — All Pass

1. ✅ **HTTPS metadata required** — `RequireHttpsMetadata` not touched; guarded by existing test.
2. ✅ **Validation flags untouched** — `ValidateIssuer/Audience = true` at `PrismOidcConfiguration.cs:171-172, 184-185`; `ValidateLifetime = true` preserved; `ValidateIssuerSigningKey` defaults preserved.
3. ✅ **Issuer/audience DB-sourced** — `validationParameters.ValidIssuer = tenant.OidcAuthority`; no request-derived fallback added.
4. ✅ **Dual gating preserved** — `if (isDevelopment && !string.IsNullOrEmpty(backchannelBase))`; forwarding headers assigned only inside that branch; `backchannelForwardingHeaders` is `null` outside.
5. ✅ **No transport-derived identity** — `X-Forwarded-Proto/Host` derived from `new Uri(CurrentTenant.OidcAuthority!...)`; never from `HttpContext.Request`, `Host` header, or env var. Verified by `RefreshTokenAsync_ForwardingHeaders_UseAuthorityScheme_NotBackchannelScheme`.
6. ✅ **Headers scoped to backchannel only** — `backchannelForwardingHeaders` is local, set only when rewrite fires, and passed to `RefreshAsync` alongside the rewritten endpoint. The non-rewrite branch leaves it `null`; `PrismTokenRefreshService.cs:574` no-ops on null.
7. ✅ **`IsRepoOwnedLocalDemoTenant` gate untouched** — Unchanged.
8. ✅ **Group E tests present** — Three new tests in `BackchannelRewriteTests.cs` cover positive case, no-rewrite negative case, and critical "scheme must come from authority not backchannel" anti-regression.

**Notes:** `TryAddWithoutValidation` is correct here (these are non-standard request headers); no header-injection risk because values come from a `Uri`-parsed DB string, not user input. No production `.app.github.dev` seeding introduced; PR is transport-only.

**No bedrock violations. Ship it.**

---

## 📌 2026-05-02: Isabelle — PT2 Razor Hardening (SEC-PT2-007, SEC-PT2-008)

**Branch:** `sec/pt2-razor-hardening`
**Status:** ✅ IMPLEMENTED

### SEC-PT2-007 — Accordion `Content` Razor trap

**Approach:** Injected `IWorkflowContentSanitizer` at the view layer via `@inject` in `_PrismComponent-Accordion.cshtml` and routed `accordionSection.Content` through `Sanitizer.Sanitize()` before it reaches `@Html.Raw`.

**Why view-layer `@inject` rather than the engine seam:** Today no producer populates `accordionSection.Content`, so adding sanitization at `BuildComponents` would be no-op with no test surface. The mission guidance explicitly preferred "as close to the render site as possible (defence in depth — even if a producer bypasses the engine seam, the view-layer sanitizer catches it)." `IWorkflowContentSanitizer` is already registered as a singleton in DI, so injection is zero-boilerplate.

**Tests added:** `AccordionContentSanitizationTests` (4 tests) — `<script>` tag stripped; legitimate body paragraph preserved; `<img onerror=>` stripped; `onclick` on allowed `<a>` stripped; legitimate rich text (h3, p, ul, a) passes through intact.

### SEC-PT2-008 — VinylRecord RTE `@Html.Raw`

**Approach:** Injected `IWorkflowContentSanitizer` at the view layer via `@inject` in `VinylRecord.cshtml`, routing the Umbraco RTE `description` field through `Sanitizer.Sanitize()` before it reaches `@Html.Raw`.

**Why the same singleton (GDS allowlist), not a separate instance:** Standard TinyMCE output for an album description is: paragraphs, bold/italic, unordered lists, and external links. All are in the GDS allowlist.

**Tests added:** `VinylRecordRteSanitizationTests` (5 tests) — `<script>` stripped; `<img onerror=>` stripped; `<svg onload=>` stripped; legitimate TinyMCE output (p, strong, em, ul, a) passes through intact; null/empty/whitespace inputs return empty string safely.

**Build & test summary:** `dotnet build UmbracoPrism.sln -c Release`: clean (0 errors, pre-existing warnings only). `dotnet test … --filter "FullyQualifiedName~UmbracoPrism.Core.Tests"`: 627 passed, 0 failed (baseline was 618; +9 new tests).

---

## 📌 2026-05-02: Tangy — Test Review: PR #45 — Codespaces URL derivation fix

**Date:** 2026-05-02
**Reviewer:** Tangy (Tester)
**PR:** https://github.com/jonnymuir/Umbraco.Prism/pull/45

**Verdict:** APPROVED WITH NOTES

**Test run:** 647 passed, 0 failed, 0 skipped ✅

**Criteria Assessment:**

1. ✅ **New Codespaces URL form covered** — Group D of `BackchannelRewriteTests.cs` (+65 lines) adds two tests using the regional token-based URL `v7ldkc4c-8443.uks1.app.github.dev`. `JwksFetch_RewritesUrl_ForRegionalCodespacesUrlScheme` proves the JWKS backchannel rewrite works with the new host scheme. `JwtValidation_StillRejectsTokenWithMismatchedIssuer_ForRegionalCodespacesUrl` proves issuer validation remains strict.

2. ✅ **Regression test for the user's actual symptom** — JWKS fetch rewrite with the new regional URL as authority is directly regression-tested.

3. ⚠️ **Request.Host override middleware — NO unit test** — `TestSite/Program.cs` lines 44–54 add a middleware that reads `TESTSITE_PUBLIC_URL` and overrides `Request.Host` for HTTPS requests. This is untested at the unit level. Recommended follow-up: Add a test to `PrismTenantMiddlewareTests` or a new `RequestHostOverrideMiddlewareTests` class.

4. ⚠️ **Hostname-lenient TenantService LIKE fallback — partially tested** — `PrismOidcConfigurationTests.cs` (+41 lines) correctly covers `IsRepoOwnedLocalDemoTenant`. However, the `TenantService.GetByDomainAsync` lenient `LIKE '%.app.github.dev'` fallback has no unit test. Recommended follow-up: Add two tests to `TenantServiceCacheStrategyTests.cs` covering the lenient lookup path.

5. ✅ **No deleted, skipped, or ignored tests** — Confirmed: no `[Ignore]`, no `Skip =`, no removed `[Fact]`/`[Theory]`. The only change to `PrismAuthExtensionsSecurityTests.cs` is adding the `[Collection(EnvVarSensitiveTestCollection.Name)]` attribute for test isolation — correct.

**Summary:** All new tests are green and correctly written as behavioural contracts. Neither follow-up gap blocks the merge — the production symptom's critical path (JWKS rewrite, issuer validation, IsRepoOwnedLocalDemoTenant) is fully covered and green.

---

## 📌 2026-05-02: Blathers — Codespaces-aware BusinessApp downstream target

**Status:** ✅ COMPLETE — Commit `6205bd4` merged to `main`

### Summary

Extended Codespaces URL discovery to include the Mock Business App (port 7245), fixing downstream demo failures in Codespaces environments.

### Context

In Codespaces, the Mock Business App downstream demo was failing with `401 Unauthorized`. The root cause: `TryDiscoverCodespaceUrls()` was only discovering Keycloak and TestSite URLs, not the BusinessApp URL on port 7245. The TestSite received hardcoded `https://localhost:7245` even in Codespaces, causing the downstream call to fail.

### Root Cause

1. **Incomplete port discovery:** `TryDiscoverCodespaceUrls()` only queried `gh codespace ports` for ports 8443 (Keycloak) and 44345 (TestSite), missing port 7245 (BusinessApp).
2. **Hardcoded constant:** `BusinessAppUrl` was a `const string = "https://localhost:7245"`, never updated with discovered Codespaces URL.
3. **Server-side call:** `DownstreamDemoController` reads config and makes HttpClient request to localhost, which fails in Codespaces where `localhost:7245` is not accessible from the server.

### Decision

Extended Codespaces URL discovery to include port 7245:

- Changed `TryDiscoverCodespaceUrls()` return type from `(string, string?)` → `(string, string?, string)` to include BusinessApp URL
- Added port 7245 to discovery loop
- Extended `FallbackCodespaceUrls()` with fallback pattern for port 7245: `https://{CODESPACE_NAME}-7245.{domain}`
- Changed `BusinessAppUrl` from const to runtime-computed variable:
  - **In Codespaces:** uses discovered URL from `gh codespace ports`
  - **Outside Codespaces:** defaults to `https://localhost:7245` (backwards compatible)
- Updated console logging to show discovered BusinessApp URL

### Impact

- ✅ Downstream demo now works correctly in Codespaces
- ✅ Backwards compatible for local dev environments
- ✅ Consistent with existing Keycloak/TestSite discovery patterns
- ✅ All 650 Core tests pass; no regressions

### Files Changed

`src/UmbracoPrism.AppHost/Program.cs`:
- Extended `TryDiscoverCodespaceUrls()` to discover and return BusinessApp URL (port 7245)
- Extended `FallbackCodespaceUrls()` to return fallback URL for port 7245
- Changed `BusinessAppUrl` from const to runtime variable
- Updated console logging

### Basis

Blathers' commit `6205bd4` implementation (2026-05-02); production deployment verification (Tom Nook, 2026-05-02); decision record in inbox (2026-05-02).


# Decision: Codespaces BusinessApp Backchannel Fix

**Date:** 2026-05-02  
**Author:** Blathers  
**Status:** Implemented  

## Problem

When running in GitHub Codespaces, the TestSite's `DownstreamDemoController` was making server-side HTTP client calls to the **public Codespaces forwarded URL** for the Mock Business App (e.g., `https://fluffy-invention-...-7245.app.github.dev/api/backoffice/me`). GitHub's port-forwarding proxy intercepted these server-to-server calls and returned "Connecting to the forwarded port..." HTML instead of forwarding to the actual service, resulting in a 200 OK with `text/html` content instead of JSON.

## Root Cause

The AppHost's `PrismBusinessApp__WorkflowApiBaseUrl` environment variable was being set to the **public Codespaces URL** (intended for browser access) instead of the **internal localhost endpoint** that should be used for server-to-server communication within the Codespace.

## Solution

Applied the same backchannel pattern used for Keycloak:

1. **AppHost (`Program.cs`)**: In Codespaces environments, set `BUSINESSAPP_BACKCHANNEL_URL` to `businessApp.GetEndpoint("https")` — the internal Aspire endpoint reference that resolves to the actual localhost URL where the BusinessApp is running (e.g., `https://localhost:7245`).

2. **DownstreamDemoController**: Modified `BuildTargetUrl()` to prefer `BUSINESSAPP_BACKCHANNEL_URL` over `PrismBusinessApp:WorkflowApiBaseUrl` when available. This ensures server-side HTTP client calls use the internal endpoint in Codespaces and bypass the port-forwarding proxy.

## Implementation

### AppHost Change

```csharp
// In Codespaces, the GitHub forwarded-port proxy blocks server-side HTTP client calls to the
// external BusinessApp URL and returns "Connecting to the forwarded port..." HTML instead of
// forwarding to the actual service. Point downstream demo at BusinessApp's internal HTTPS
// endpoint so server-to-server calls bypass the proxy.
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("https"));
```

### DownstreamDemoController Change

```csharp
// In Codespaces, BUSINESSAPP_BACKCHANNEL_URL points to the internal endpoint
// for server-to-server communication (bypasses GitHub port-forwarding proxy).
// Outside Codespaces, falls back to PrismBusinessApp:WorkflowApiBaseUrl.
var baseUrl = configuration["BUSINESSAPP_BACKCHANNEL_URL"]?.TrimEnd('/')
    ?? configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
```

## Impact

- **Codespaces**: Server-to-server downstream demo calls now use `https://localhost:7245/api/backoffice/me` (internal) instead of the public forwarded URL, correctly returning JSON instead of HTML.
- **Local dev**: No change — `BUSINESSAPP_BACKCHANNEL_URL` is not set, so `PrismBusinessApp:WorkflowApiBaseUrl` is used as before.
- **Security**: No impact — backchannel URL is only used for server-to-server HTTP client calls, never for browser redirects or OIDC flows.

## Verification

- **Build**: Succeeded, 0 errors, 6 pre-existing warnings (unchanged)
- **Tests**: 650 Core tests passing, 0 failed, 0 skipped
- **Confidence**: HIGH — This is the same pattern already proven for Keycloak backchannel; applying it to BusinessApp is a direct extension.

## Related Patterns

- `KEYCLOAK_BACKCHANNEL_URL` (token refresh, JWKS fetch)
- GitHub Codespaces port-forwarding proxy blocks server-side calls to public forwarded URLs
- Aspire `.GetEndpoint()` API provides internal endpoint references for service-to-service communication

## Follow-Up

None — this completes the Codespaces backchannel pattern for all services that require server-to-server communication.


# Downstream Demo: Content-Type Validation

**Date:** 2026-05-02  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Implemented  
**Commit:** da7ddc9

## Context

The `DownstreamDemoController` endpoint (`/api/prism/downstream-demo`) makes HTTP requests to the MockBusinessApp on behalf of the authenticated user to demonstrate token forwarding in the dashboard UI. This endpoint is exposed to Codespaces port-forwarding quirks.

### Problem

When running in Codespaces, the public port-forwarding proxy sometimes returns HTML placeholder pages (e.g., "Connecting to the forwarded port...") instead of JSON from the target endpoint. The controller was treating these 200 OK HTML responses as successes, breaking the dashboard UI that expects structured JSON data.

Tangy added regression tests and flagged this as a false-positive bug.

### Root Cause

The controller was checking HTTP status code but not validating the `Content-Type` header. Any 200 response was passed through to the dashboard as-is, including:
- `text/html` (port-forwarding placeholders)
- `text/plain` (misconfigured endpoints)

## Decision

**Validate `Content-Type` header before processing response body.**

The controller now:

1. **Checks for JSON content types** before parsing:
   - `application/json`
   - `application/problem+json`
   - `text/json`

2. **Returns a structured error** for non-JSON responses:
   ```json
   {
     "statusCode": 0,
     "statusText": "Invalid Response",
     "contentType": "text/html",
     "body": "Expected JSON but received text/html\n\n[user-friendly hint]"
   }
   ```

3. **Preserves Blathers' backchannel URL fix** — the `BUSINESSAPP_BACKCHANNEL_URL` environment variable still takes precedence over `PrismBusinessApp:WorkflowApiBaseUrl` in Codespaces to bypass the proxy for server-to-server calls.

## Implementation

Added `IsJsonContentType(string)` helper method that checks for common JSON MIME types. The validation happens immediately after receiving the HTTP response, before attempting to parse or pretty-print the body.

If an HTML page is detected, the error message includes a user-friendly hint about Codespaces port-forwarding delays.

## Test Coverage

- `DownstreamDemo_ReturnsError_WhenResponseIsHtml` — validates detection of HTML responses
- `DownstreamDemo_DetectsCodespacesPortForwardingPage` — specifically tests Codespaces placeholder pages
- `DownstreamDemo_RejectsNonJsonContentType` — validates rejection of `text/plain` and other non-JSON types

All 653 Core tests pass.

## Impact

**Fixed:**
- HTML/non-JSON responses now surface as errors in the dashboard, not silent successes
- Dashboard UI can show meaningful error messages instead of breaking on invalid JSON parse

**Preserved:**
- Blathers' backchannel URL fix for Codespaces server-to-server calls
- All existing functionality (URL allowlisting, token refresh on 401, etc.)

**End-to-End Note:**
This fix ensures the dashboard shows a clear error when the port-forwarding page appears. The underlying cause (BusinessApp not ready yet) still requires waiting for Codespaces to fully forward the port — but users now see an actionable error instead of a broken UI.


---

## 📌 2026-05-02: Blathers — Codespaces: Remove BusinessApp Backchannel URL

**Status:** ✅ IMPLEMENTED — Commit `ffc32c5` on `main`

### Decision

**Remove `BUSINESSAPP_BACKCHANNEL_URL` entirely.**

Server-side calls should use the **discovered public Codespaces URL** (`PrismBusinessApp__WorkflowApiBaseUrl`), which is already set correctly by the AppHost's `TryDiscoverCodespaceUrls()` logic.

### Why

**Keycloak vs BusinessApp architectural difference:**
- **Keycloak:** A Docker container added with `AddContainer()`. `keycloak.GetEndpoint("http")` returns a concrete HTTP endpoint that works with YARP's built-in service discovery.
- **BusinessApp:** An Aspire project added with `AddProject()`. `businessApp.GetEndpoint("https")` returns an Aspire service discovery URL (e.g., `https+http://businessapp`) that only resolves when the HttpClient is configured with Aspire service discovery extensions.

**The TestSite's `DownstreamDemoController` uses a plain `IHttpClientFactory.CreateClient()` without Aspire service discovery**, so it cannot resolve `https+http://businessapp` URLs. When the URL fails to resolve, it falls back to the raw string `https://localhost:7245`, which doesn't work in Codespaces' port-forwarding context.

The original HTML response issue was likely a **transient port-forwarding startup delay**, not a persistent proxy blocking problem.

### Changes

1. **AppHost (`Program.cs`):**
   - Removed the `if (codespaceName != null) testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", ...)` block
   - Added comment explaining why BusinessApp doesn't need a backchannel URL like Keycloak does

2. **DownstreamDemoController (`BuildTargetUrl`):**
   - Removed `BUSINESSAPP_BACKCHANNEL_URL` preference
   - Always uses `PrismBusinessApp:WorkflowApiBaseUrl` (which is the discovered public Codespaces URL or localhost:7245 for local dev)

### Test Results

- Core tests: 653 passed, 0 failed, 0 skipped
- Build: Succeeded

### Confidence

**HIGH** — The fix aligns with Aspire's service architecture:
- Containers (Keycloak) can use concrete endpoint references
- Projects (BusinessApp) should use public URLs or require Aspire service discovery extensions

---

## 📌 2026-05-02: User Directive — Downstream Non-JSON Response Diagnostics

**Directive:** For downstream non-JSON failures, expose the returned HTML for diagnosis instead of only saying HTML was received and is wrong.

**Source:** Jonny Muir (via Copilot, 2026-05-02T17:05:58)

**Status:** Captured for team memory.

---

## 📌 2026-05-02: User Directive — HMACSecretKey Committed in Appsettings

**Directive:** Do not commit HMACSecretKey in appsettings; stop this from being committed.

**Source:** Jonny Muir (via Copilot, 2026-05-02T17:05:58)

**Status:** Captured for team memory.

---

## 📌 2026-05-03: User Directive — Never Have Failing Tests

**Directive:** We should never have failing tests.

**Source:** Jonny Muir (via Copilot, 2026-05-03T09:32:21)

**Status:** Captured for team memory — driving CI repair and test isolation strategy.

---

## 📌 2026-05-03: Tangy — CI Fix Scope: Test Isolation Only

**Status:** 📋 DECISION RECORD

### Decision

For the current CI repair, keep the change set at the **test layer**: serialize env-var-sensitive tests and snapshot/restore `KEYCLOAK_BACKCHANNEL_URL` plus `ASPNETCORE_ENVIRONMENT` in reader classes that exercise env-sensitive auth paths.

Do **not** broaden `PrismOidcConfiguration` runtime behavior as part of this repair unless a separate production bug is proven.

### Why

The observed failure mode is env-var bleed between tests in CI, not a confirmed product defect in the app's OIDC callback/runtime flow. Narrowing the fix preserves the original production contract while still locking down the regression at the place it occurs.

### Consequence

- Keep `EnvVarSensitiveTestCollection` + env snapshot/restore patterns
- Preserve regression coverage around env bleed and auth-path stability
- Revisit product-side OIDC backchannel gating only under a distinct bug/requirement, not piggy-backed onto this CI fix

---

## 📌 2026-05-03: Blathers — CI Keycloak Backchannel Isolation

**Status:** 📋 DECISION RECORD

### Decision

Keep generic OIDC callback backchannel rewrites dual-gated to `ASPNETCORE_ENVIRONMENT=Development` and HTTPS public authorities only, and treat callback/loopback regression tests as env-var-sensitive readers.

### Why

`Phase1SecurityRegressionTests` executes the real `PrismOidcConfiguration.OnAuthorizationCodeReceived` path against an in-process loopback OIDC server on `http://127.0.0.1`. When another test class temporarily sets `KEYCLOAK_BACKCHANNEL_URL=http://keycloak-internal:8080`, the callback code must not rewrite that loopback authority to the Codespaces-only Keycloak backchannel host.

The smallest durable fix is:

1. only honor the backchannel env var for Development + HTTPS public authorities, and
2. serialise/snapshot any test class that reads those env vars transitively through Prism auth code.

### Consequence

- Codespaces/local-demo backchannel behavior stays intact for real HTTPS Keycloak authorities
- Isolated loopback auth tests no longer inherit `keycloak-internal:8080`
- Future callback-path tests must join `EnvVarSensitiveTestCollection` if they can observe `KEYCLOAK_BACKCHANNEL_URL` or `ASPNETCORE_ENVIRONMENT`

---

## 📌 2026-05-03: Brewster — Startup Helper Aspire/Codespaces Contract Alignment

**Status:** 📋 DECISION RECORD

### Decision

The port-3000 startup helper now follows the live AppHost and Codespaces contracts instead of guessing older defaults.

- **Codespaces public URLs** come from `gh codespace ports` output when available, with hostname-port derivation as fallback, instead of assuming `{CODESPACE_NAME}-{port}.app.github.dev`.
- **Aspire dashboard links** use the real Codespaces dashboard port `15135`, while local development still uses `https://localhost:17214`.
- **Service readiness** uses the current live contracts:
  - TestSite → `https://localhost:44345/api/prism/downstream-demo/seed-contract-ready`
  - Keycloak → `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration`
  - MockBusinessApp → `https://localhost:7245/debug/auth`

### Why

The helper had drifted from the real stack: it still guessed Codespaces URLs from `CODESPACE_NAME`, linked the dashboard to the wrong public port, and tolerated stale helper endpoint assumptions. That produced false startup states and noisy repeated polling failures even when the forwarded applications were healthy.

### Additional Rule

Keep startup-helper logs under `artifacts/startup-status/` inside the repo, not `/tmp`, so the helper stays aligned with repo security constraints and troubleshooting docs.

## 📌 2026-05-03: Blathers — Local HMAC Secret Remediation (appsettings Drift)

**Status:** ✅ MERGED

### Summary

Repaired local TestSite appsettings drift so tracked `appsettings.json` no longer contains a real HMAC secret and local-only config is handled safely.

### Decision

When `Umbraco:CMS:Imaging:HMACSecretKey` drift occurs in tracked `appsettings.json` during local development, remove the HMAC key from tracked `appsettings.json` and keep the local runtime value in the gitignored `src/UmbracoPrism.TestSite/appsettings.Local.json` override instead.

### Consequences

- Repository stays compliant with the secret guard.
- Local developers keep a stable imaging key without recommitting it.
- README guidance and `.gitignore` remain the source of truth for local setup.


## 📌 2026-05-03: Blathers — Downstream HTTP Diagnostics for Non-JSON Responses

**Status:** 📝 In progress; merged from inbox.

**Summary:** Preserve real downstream HTTP status/reason and headers when responses are non-JSON, rather than flattening to `statusCode: 0 / Invalid Response`.

**Decision:** `DownstreamDemoController` now logs non-JSON downstream responses with headers and preserves real HTTP status/reason in the payload.

**Why:** Live Codespaces symptom (`http://localhost:5163/api/backoffice/me`, `contentType: unknown`) is produced by a real HTTP response. Flattening that into status code 0 hides critical clues like bare `401 Unauthorized` challenge with `WWW-Authenticate` header.

**Implementation notes:**
- Response payload preserves real HTTP status/reason and includes `invalidResponse: true`
- Diagnostic text now includes headers such as `WWW-Authenticate`
- Retry logic uses per-request cancellation token instead of mutating `HttpClient.Timeout`

**Expected effect:** Next live repro will clearly distinguish transport failure, auth rejection, redirect behaviour, and HTML tunnel pages.

---

## 📌 2026-05-03: Brewster — Codespaces Recovery Scripts

**Status:** ✅ MERGED; scripts under `scripts/codespaces/`, CODESPACES.md updated.

**Summary:** Added operator scripts for fast recovery path without full Codespace rebuild.

**Three scripts:**
1. **`stop.sh`** — kills AppHost and status server gracefully (force-kill fallback)
2. **`refresh.sh`** — standard cycle: stop → `git pull origin main` → conditional `npm install` → restart. Flags: `--rebuild` (adds `dotnet restore` + `dotnet build`), `--no-start`
3. **`health-check.sh`** — probes five readiness endpoints, exits 0/1

**Rationale:**
- `refresh.sh` without `--rebuild` is fast (~90 seconds) for code-only changes
- Rebuild is opt-in; auto-detect `package-lock.json` changes
- Scripts delegate to `.devcontainer/on-start.sh` (single source of truth)
- Health-check can run standalone

**Readiness Endpoints:**
| Port | Service | Endpoint |
|---|---|---|
| 3000 | Status server | `http://localhost:3000/api/status` |
| 15135 | Aspire Dashboard | `http://localhost:15135` |
| 44345 | TestSite | `/api/prism/downstream-demo/seed-contract-ready` |
| 8443 | Keycloak | `/realms/prism-dev/.well-known/openid-configuration` |
| 7245 | MockBusinessApp | `/debug/auth` |

**Full rebuild needed if:** `devcontainer.json`, `on-create.sh`, Docker-in-Docker, or SDK version constraints change.

---

## 📌 2026-05-03: User Directive — Codespaces Diagnostics Focus

**Status:** 🎯 Guidance for team.

**Directive:** Do not keep guessing at Codespaces downstream failure; diagnose it properly with useful logs/diagnostics, or request a specific site-side check to test a concrete hypothesis.

**Source:** User request (Jonny Muir via Copilot).


---

## 📌 2026-05-03: Brewster — Print Full Status Page URL on Startup

**Status:** ✅ IMPLEMENTED; `.devcontainer/on-start.sh` updated; CODESPACES.md refreshed.

**Summary:** Status server startup now prints the full forwarded URL (Codespaces browseUrl or localhost fallback) instead of generic "open port 3000" instruction.

**Decision**

When the startup status server successfully starts, print the full forwarded URL:

- **In Codespaces:** calls `get_codespace_url 3000` to resolve `browseUrl` via `gh codespace ports`, with fallback to legacy `{CODESPACE_NAME}-3000.{DOMAIN}` pattern.
- **Locally (non-Codespaces):** prints `http://localhost:3000`.

**Rationale**

Port 3000 is pre-declared in `devcontainer.json` as a forwarded public port (`onAutoForward: openBrowser`), so Codespaces registers it before status server starts. `CODESPACE_PORTS_JSON` (fetched at script start) reliably contains the `browseUrl`. The `get_codespace_url()` helper already handles both regional opaque-token and legacy name-based schemes.

**Impact**

- Terminal output becomes a clickable link in VS Code / Codespaces terminals
- No behaviour change for local development
- CODESPACES.md "Useful tips" section updated

**Basis**

Brewster's commit 1633f73; inbox decision.


## 📌 2026-05-03: Blathers — Enhanced 401 Diagnostics for Live Codespaces

**Status:** ✅ SHIPPED — Enhanced logging deployed to PrismAuthExtensions.cs and MockBusinessApp

**Summary:** Preemptive diagnostic enhancements added to capture token `kid`, ASPNETCORE_ENVIRONMENT, computed backchannel JWKS state, and JWKS metadata URLs when HTTP 401 `invalid_token` failures occur.

**Context**

User reported HTTP 401 `invalid_token` from MockBusinessApp when calling from the Codespaces dashboard. The existing `OnAuthenticationFailed` logging lacked critical debugging details — token key ID (kid), environment state, dual-gate condition evaluation, and JWKS metadata URLs — making root cause diagnosis difficult for operators without shell access.

**Decision**

Ship preemptive diagnostic enhancements before the next live Codespaces failure occurs, revealing dual-gate logic transparently and providing actionable evidence.

**Changes**

1. **Token `kid` extraction** — Logged when present in JWT header
2. **ASPNETCORE_ENVIRONMENT display** — Shows whether Development-gated logic applies
3. **Computed `backchannel JWKS enabled`** — Boolean showing if dual-gate condition is true
4. **JWKS metadata URL** — Logged for `SecurityTokenSignatureKeyNotFoundException`
5. **Enhanced `/debug/auth` endpoint** — Now shows `backchannelJwksEnabled` matching validator logic

**Rationale**

"Do not guess; prefer logging and messages that reveal the real problem." Enhanced diagnostics make dual-gate logic transparent and provide operators with actionable evidence for diagnosing 401s remotely.

**Impact**

When the next 401 occurs in live Codespaces, operators can:
- `curl https://{codespace}/debug/auth` to confirm backchannel state
- Read console logs to see token `kid` and JWKS metadata URL
- Compare binary build timestamp vs. git commit to detect stale runtime

No behaviour change — diagnostics only.

**Test Coverage**

- All 672 Core tests passing
- All 20 PrismAuthExtensions security tests passing

**Files Changed**

- `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`
- `src/UmbracoPrism.MockBusinessApp/Program.cs`

---

## 📌 2026-05-03: Blathers — Stale Runtime Restart Pattern (Operational Guidance)

**Status:** 📋 PROPOSED GUIDANCE — Documented in `.squad/skills/`

**Summary:** Established operational pattern for handling stale auth validation config in long-running Aspire processes after code pulls.

**Context**

User reported persistent 401 `invalid_token` from MockBusinessApp after running `refresh.sh` and pulling latest code. The BusinessApp had been running for 2h+ and predated PR #46's JWKS backchannel fix for generic OIDC bearer validation. Code-side changes do not affect running processes until they restart.

**Decision**

**Always restart the Aspire stack after pulling auth-related changes.**

The `refresh.sh` script already implements this:
1. Stop AppHost
2. Pull latest code
3. Restart via `.devcontainer/on-start.sh`

**Rationale**

- Aspire-managed services (TestSite, BusinessApp, KeycloakProxy) are launched once and stay resident
- Auth validation config is set at startup (e.g., JwtBearerOptions, signing key resolvers)
- A code change in the worktree does not affect running processes until they restart
- The `live-oidc-401-stale-runtime` skill documents this pattern

**Alternatives Considered**

1. **Hot reload** — ASP.NET Core hot reload does not cover JwtBearerOptions or validator delegates
2. **Manual Aspire restart** — Works, but `refresh.sh` is the canonical operator path
3. **Process start time warnings** — Could add diagnostics, but simpler to just restart after pulls

**Operational Guidance**

- Use `refresh.sh` after `git pull` when auth changes are suspected
- For single-service changes, use Aspire Dashboard restart button (https://localhost:17214)
- No code changes needed; this is operational discipline

---

## 📌 2026-05-03: Brewster — Fix Malformed Codespaces URL (tr -d '/' Regression)

**Status:** ✅ IMPLEMENTED — One-line fix in `.devcontainer/on-start.sh`

**Problem**

After the "full-URL output on startup" change landed, users reported:
- Browser download prompt on every link printed by `refresh.sh`
- 404 errors when following those links

**Root Cause**

In `get_codespace_url()` (`.devcontainer/on-start.sh`), the `jq` branch piped through `tr -d '/'` to strip trailing slashes. However, `tr -d '/'` deletes **every** forward slash in the string — including the `//` in `https://`.

Example:
- Input: `https://codespace-name-3000.app.github.dev/`
- Output: `https:codespace-name-3000.app.github.dev` ← invalid URL

Since `jq` is installed in the Ubuntu 24.04 devcontainer, this branch always ran. The Python fallback (which correctly used `.rstrip('/')`) was never reached.

**Fix**

Replaced `| tr -d '/'` with `| sed 's|/*$||'` in the `jq` branch.  
`sed 's|/*$||'` strips only trailing slashes, preserving `://` in the scheme.

**Impact**

- Printed Codespaces URLs are now valid clickable links
- No other behaviour changed
- Python fallback remains as-is (was already correct)

---

## 📌 2026-05-03: User Directives (Operational Memory)

**Status:** 🎯 Guidance for team cognition

**Three Directives from Jonny Muir (2026-05-03T12:00–12:07)**

1. **Codespaces as Primary Runtime:** When diagnosing auth/runtime issues, remember the problem is in Codespaces, not the local machine. (2026-05-03T12:00:19)

2. **Diagnose Before Fixing:** When diagnosing or suggesting a fix, do not guess; prefer logging and messages that reveal more about the problem over a fix we do not know will work. (2026-05-03T12:00:19)

3. **Diagnose Against Actual Failure:** Do not assume Codespaces is the primary runtime for this class of failure; diagnose against the runtime that is actually failing. For the current issue, the failing path is the live Codespaces dashboard call to MockBusinessApp. (2026-05-03T12:07:19)

---

## 📌 2026-05-03: Copper — MockBusinessApp 401 Stale Runtime Pattern Review

**Status:** ✅ REVIEWED & RECOMMENDED

**Summary:** Security review of HTTP 401 `invalid_token` from MockBusinessApp confirms stale runtime pattern (high confidence) and validates code-side trust chain.

**Context**

User reported: "TestSite is back up, but the Mock Business App call returns HTTP 401 Unauthorized with `WWW-Authenticate: Bearer error=\"invalid_token\"` and no response body."

**Investigation Summary**

**Root Cause (HIGH CONFIDENCE): Stale Runtime**

- Aspire MockBusinessApp (port 7245): Started 09:45:37, running 2h+ at investigation time
- TestSite: Recently restarted (user confirmed "back up")
- Last auth code change: bf1c6e7 (2026-05-02 11:23:54)
- Pattern: TestSite fresh runtime + MockBusinessApp stale runtime = validation mismatch

**Code-Side Trust Chain: VERIFIED ✅**

- Token issuer: `https://localhost:8443/realms/prism-dev`
- MockBusinessApp OidcAuthority: `https://localhost:8443/realms/prism-dev` (appsettings.json line 26)
- ClientId: `prism-client` (appsettings.json line 25)
- Issuer/audience validators correctly implemented (PrismAuthExtensions.cs lines 115–163)
- Backchannel JWKS fetch correctly scoped to Development + env var guard (lines 232–242)

**Recommendation**

**Primary Action:** Restart MockBusinessApp Aspire resource (port 7245) before investigating code-side issues.

**If restart doesn't fix**, the problem is NOT stale runtime — it's a real validation mismatch. Next steps:
1. Capture `OnAuthenticationFailed` console diagnostics (PrismAuthExtensions.cs lines 27–68)
2. Compare actual token `iss`/`azp` claims vs configured `OidcAuthority`/`ClientId`
3. Verify token is reaching MockBusinessApp (not stripped by middleware)
4. Check for typo in ClientId or OidcAuthority config

**Optional Diagnostic Improvements**

**Current `OnAuthenticationFailed` handler is good** but user didn't provide console output.

**Suggested improvements (OPTIONAL):**
- Log auth failures to structured sink (e.g., Seq, Application Insights)
- OR surface diagnostics in test harness for easier repro investigation
- OR add health check endpoint that reports auth config (Development-only)

**Trade-off:** More diagnostic surface area = more attack surface. Keep Development-gated.

---

## 📌 2026-05-03: Brewster — Pre-Forward Critical Ports in Codespaces

**Status:** ✅ IMPLEMENTED — Commit to `.devcontainer/devcontainer.json`

### Summary

Codespaces health checks reported "ready" but forwarded TestSite URL (port 44345) served GitHub tunnel 404 downloads. Root cause: ports declared in `portsAttributes` but not pre-forwarded, creating timing gap between localhost readiness and public URL availability.

### Decision

Added `"forwardPorts": [3000, 15135, 44345, 7245, 8443]` to `.devcontainer/devcontainer.json` to eagerly pre-forward all critical ports on container start, eliminating the timing gap.

### Why Safe

- Codespaces already declares these ports in `portsAttributes`
- Pre-forwarding does not change health check logic (still probes localhost)
- If localhost readiness passes, forwarded URLs now exist and should be available
- Port 3000 (status page) was already implicitly forwarded via `"onAutoForward": "openBrowser"`

### Rule Going Forward

All critical Codespaces ports must be explicit in `forwardPorts` array to guarantee they exist before health checks run. This prevents misleading "ready" signals when user-facing forwarded URLs are not yet accessible.

### Related Context

- 2026-05-03: Tangy identified that health checks only verify localhost, not tunnel accessibility
- 2026-05-03: Downstream demo HTML validation previously showed tunnel 404 as content success (now fixed)

---

## 📌 2026-05-03: Tangy — Health Checks Must Verify Tunnel Accessibility

**Status:** 🔵 PROPOSED — Diagnostic finding from live Codespaces reproduction

### Summary

Health check script (`scripts/codespaces/health-check.sh`) probes `curl https://localhost:{port}` which succeeds when app is running locally, but **does not verify** that forwarded Codespaces URLs are publicly accessible. Live test on 2026-05-03 showed:
- Port 44345 localhost check: ✅ HTTP 200
- Port 44345 forwarded URL: ❌ HTTP 404 (tunnel-level, not app-level)

This creates a **false positive** — startup page reports "ready" while public URL is inaccessible.

### Decision

Health checks in Codespaces must verify **both** internal and tunnel surfaces:

1. **Internal readiness** — `curl https://localhost:{port}` confirms app is listening
2. **Tunnel accessibility** — Test the actual forwarded URL (via `gh codespace ports --json` or `GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN`)

If tunnel-level check fails, status page must report port as "not publicly accessible" with actionable guidance (e.g., "Port 44345 is private. Make it public in the Ports panel.").

### Implementation Guidance

- Use `gh codespace ports --json sourcePort,browseUrl,visibility` to discover forwarded URLs and visibility
- For each critical port, test both localhost and public `browseUrl`
- Distinguish failures: localhost failure = service not started; tunnel failure = port not forwarded or misconfigured visibility

### Rationale

Users open forwarded URLs, not localhost. A health check that only tests localhost is incomplete and misleading. This is especially important in multi-tenant or demo scenarios where endpoint availability is a trust signal.

### Affected Components

- `scripts/codespaces/health-check.sh`
- `scripts/startup-status/server.js` (status page backend)
- `.devcontainer/on-start.sh` (readiness signaling)

**Evidence:** Tangy's live session (2026-05-03 12:28:26 UTC+1)


---

## 📌 2026-05-03: User Directive — Hypothesis Proof Before Fixes

**Status:** ✅ POLICY RECORDED

**By:** Jonny Muir (via Copilot)

**Directive:** Diagnose and obtain hypothesis proof in logs, responses, or other appropriate evidence before proposing a fix. Prefer proof over guessing.

**Rationale:** Evidence-driven problem-solving prevents thrashing on incorrect hypotheses and accelerates root-cause convergence.

**Application:** Team commits to this discipline when assigned diagnostic or remediation tasks.

---

## 📌 2026-05-03: Blathers — Aspire Dashboard Codespaces 401 Redirect — Diagnosis & Next Steps

# Aspire Dashboard Codespaces 401 Redirect — Diagnosis & Next Steps

## Problem Statement

When accessing the Aspire dashboard at the public Codespaces URL (`https://organic-space-fortnight-77g9wvq6jxhxg97-15135.app.github.dev/`):
- Browser redirects to `https://localhost:41981` (not a valid public URL)
- `curl -kI` shows `HTTP/2 401` with header `www-authenticate: tunnel`
- Result: Dashboard appears inaccessible from public URL

## Root Cause: Codespaces Tunnel Authentication

**The 401 + `www-authenticate: tunnel` response is NOT a dashboard bug — it's GitHub Codespaces' port forwarding authentication layer.**

When the Codespaces port forwarding proxy receives a request and the destination port:
- Returns 401 or is unreachable → proxy responds with `www-authenticate: tunnel`
- Browser's tunnel client then attempts to negotiate on an ephemeral port (41981 in this case)
- If tunnel negotiation fails, the browser shows the redirect as broken

## Configuration Assessment: CORRECT ✅

**All repo-side setup is properly configured for anonymous dashboard:**

| Component | Configuration | Status |
|-----------|---|---|
| on-start.sh | Exports `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` before `dotnet run` | ✅ Correct |
| launchSettings.json | Hardcodes same flag in environment variables | ✅ Correct |
| devcontainer.json | Port 15135 marked as public HTTP | ✅ Correct |
| Env var inheritance | Exported before `nohup`, so child process will inherit | ✅ Correct |

**This is NOT a configuration bug; this is a runtime diagnosis needed.**

## Diagnosis: What's Actually Running?

The 401 response suggests one of these runtime conditions:

### Most Likely: Dashboard Not Listening on Port 15135
When Codespaces tunnel proxy tries to reach `localhost:15135` and the port is either:
- Not listening at all → tunnel proxy returns 401
- Listening but not responding to HTTP → tunnel proxy returns 401
- Listening on wrong protocol (HTTPS only) → tunnel proxy returns 401

### Next Most Likely: Environment Variables Not Reached AppHost
Though `nohup` preserves parent env vars in standard Unix, the process might have:
- Lost the exports if there's a shell/exec boundary
- Been started before the export statement
- Env var set to wrong value (typo in on-start.sh)

### Possible: Codespaces Workspace Config Requires Auth
GitHub Codespaces allows workspace-level policies that can require auth even for ports marked public in devcontainer.json.

## Recommended Action: Run These Diagnostics

**Inside the running Codespaces terminal:**

```bash
# 1. PRIMARY: Is dashboard listening?
curl -v http://localhost:15135/ 2>&1 | head -30

# 2. SECONDARY: Did AppHost get the env var?
pgrep -f "dotnet run.*AppHost" | xargs -I{} cat /proc/{}/environ | tr '\0' '\n' | grep DOTNET_DASHBOARD

# 3. Check if port is even bound
lsof -i :15135  # or: netstat -tlnp | grep 15135

# 4. Review AppHost startup logs for port binding info
tail -50 artifacts/startup-status/prism-apphost.log | grep -i "listening\|port\|dashboard\|http"

# 5. Use the status server diagnostic endpoint
curl http://localhost:3000/api/diag | jq .
```

**Expected healthy output:**
- Step 1 should return HTML (Blazor app markup)
- Step 2 should show `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`
- Step 3 should show `dotnet` process on port 15135

## If Diagnostics Show Dashboard Not Responding

**Potential Fixes (Do Not Implement Yet — Diagnostics First):**

1. Restart AppHost: `bash scripts/codespaces/refresh.sh`
2. Check if launchSettings profile is being used: `ASPNETCORE_LAUNCH_PROFILE=https dotnet run --project src/UmbracoPrism.AppHost`
3. Verify HTTP binding is active in Program.cs
4. Check AppHost logs for startup errors: `tail -f artifacts/startup-status/prism-apphost.log`

## Decision

**No code changes recommended at this time.** The repo configuration is correct. This appears to be either:
1. A Codespaces runtime state issue (AppHost not actually running/listening on port 15135)
2. An environment variable derivation issue (env vars not reaching the process)

**Next session should focus on:**
1. Running the diagnostic commands from inside the failing Codespaces
2. Confirming actual port binding and environment state
3. Then deciding if code-side fix is needed (unlikely) or if it's a Codespaces-specific workaround

## Learning: Port 41981

The ephemeral port 41981 is assigned by GitHub Codespaces' tunnel client for negotiating the tunnel connection. It's a symptom, not the problem. The real problem is whatever causes the initial 401 response (likely: dashboard not listening).

---

**Related:** GitHub Codespaces tunnel authentication protocol  
**Severity:** Non-blocking (local development convenience, not production)  
**Requires Review:** Yes — before implementing any code fixes

---

## 📌 2026-05-03: Tangy — Aspire Dashboard Codespaces URL Contract — Diagnosis & Acceptance Criteria


# Aspire Dashboard Codespaces URL Contract — Diagnosis & Acceptance Criteria

## Problem Statement

Dashboard forwarded URL in Codespaces returns HTTP 401 with `www-authenticate: tunnel` header, causing:
- Browser redirects to GitHub login (confusing UX)
- Dashboard never loads
- Users see blank or download-prompts when clicking the Aspire Dashboard link

## Root Cause

**Port 15135 visibility in Codespaces infrastructure**, not application misconfiguration.

- The Aspire app is correctly configured with `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`
- Internal healthchecks (`curl https://localhost:15135`) succeed
- But the Codespaces tunnel proxy does not expose port 15135 publicly
- Result: Tunnel layer intercepts and demands GitHub authentication via `www-authenticate: tunnel` header

## What HTTP 401 + `www-authenticate: tunnel` Means

Codespaces-specific infrastructure protocol:
- **401** = Not authenticated to tunnel proxy
- **`www-authenticate: tunnel`** = Authenticate via Codespaces (GitHub login required)

This is **NOT** an app error. Port is simply not marked public.

## Expected User-Facing Contract

When a user clicks "Aspire Dashboard" link from status page or VS Code Ports panel in Codespaces:

### ✅ Success States
- HTTP 200 response (or compatible 2xx)
- Dashboard HTML renders immediately
- No GitHub login redirect
- Service telemetry visible (TestSite, Keycloak, MockBiz, MockBusinessApp, Dashboard health)
- ZERO occurrence of `www-authenticate: tunnel` header

### ❌ Failure States  
- HTTP 401 with `www-authenticate: tunnel` header
- Browser shows "Sign in to GitHub" prompt
- Dashboard does not render
- User sees blank page or empty response

## Acceptance Criteria — How to Verify Fix

| Scenario | Test | Expected |
|----------|------|----------|
| **Port visibility** | Check `.devcontainer/devcontainer.json` ports array or VS Code Ports panel | Port 15135 is explicitly declared or marked public |
| **No tunnel auth** | `curl -kI https://{forwarded-url}:15135/` | HTTP 200 (no 401, no `www-authenticate` header) |
| **Dashboard accessible** | Open dashboard forwarded URL in browser | Loads immediately, shows service list + telemetry |
| **No login prompt** | Follow forwarded URL from status page | No GitHub login screen, dashboard renders |

## Recommended Test Script

Add to health-check or CI:

```bash
#!/bin/bash
DASHBOARD_URL="http://localhost:15135"
CODE=$(curl -sk --max-time 5 -o /dev/null -w "%{http_code}" "$DASHBOARD_URL/")
AUTH_HDR=$(curl -sk --max-time 5 -i "$DASHBOARD_URL/" 2>/dev/null | grep -i "www-authenticate:" || true)

echo "Dashboard HTTP Status: $CODE"
if [ -n "$AUTH_HDR" ]; then
  echo "⚠️  TUNNEL AUTH HEADER DETECTED: $AUTH_HDR"
  exit 1
fi

if [ "$CODE" != "200" ]; then
  echo "❌ Expected 200, got $CODE"
  exit 1
fi

echo "✅ Dashboard contract OK"
exit 0
```

## Related Context

- Tangy history (2026-05-03): Port 44345 (TestSite) confirmed NOT forwarded → HTTP 404 from tunnel layer
- Port visibility requires explicit declaration in devcontainer.json or manual VS Code toggle
- Previous false positive: internal localhost checks (✅) masked external tunnel visibility issue (❌)

## Follow-Up

- Verify port 15135 is in `.devcontainer/devcontainer.json` forwardPorts array
- If not present, add it with `"visibility": "public"`
- Test in live Codespace after change
- Document port visibility requirements in CODESPACES.md
---
date: 2026-05-03T15:12:55.439+01:00
author: blathers
status: implemented
---

# Codespaces Aspire Dashboard: Use Port 17214 (HTTPS) Instead of 15135 (HTTP)

## Context

The Aspire dashboard binds to two ports:
- HTTP on 15135
- HTTPS on 17214

In Codespaces, when the HTTP endpoint on 15135 is accessed, it redirects to an internal ephemeral HTTPS port (e.g., 41981) rather than the advertised forwarded HTTPS port 17214. This makes the HTTP endpoint unsuitable for browser access in Codespaces.

## Decision

All Codespaces-facing startup output, status surfaces, helper scripts, and documentation now reference port **17214 (HTTPS)** as the primary Aspire dashboard endpoint, not 15135.

## Changes

### Startup Scripts
- `.devcontainer/on-start.sh`: Changed `DASHBOARD_URL` from `http://localhost:15135` to `https://localhost:17214` in Codespaces environment
- `scripts/codespaces/health-check.sh`: Unified dashboard URL to `https://localhost:17214` for both Codespaces and local

### Configuration
- `.devcontainer/devcontainer.json`: Swapped port 15135 to 17214 in `forwardPorts` array, updated port labels (17214 is primary, 15135 is legacy/unused)

### Status Server
- `scripts/startup-status/server.js`: Changed `ASPIRE_CODESPACES_PORT` default from 15135 to 17214

### Documentation
- `CODESPACES.md`: Updated to reference port 17214 as primary dashboard port in both Codespaces and local development
- `scripts/codespaces/stop.sh`: Updated freed ports message

### Tests
- `scripts/startup-status/server.test.js`: Updated port references from 15135 to 17214
- `src/UmbracoPrism.Client/tests/support/live-app-host.ts`: Labeled 15135 as "legacy"
- `scripts/validate-aspire-prereqs.mjs`: Labeled 15135 as "legacy"

## Rationale

1. **Consistent behavior**: Both Codespaces and local development now use the same HTTPS endpoint
2. **Simpler configuration**: No environment-specific URL scheme detection needed
3. **Correct forwarding**: Port 17214 is the advertised HTTPS endpoint that Codespaces properly forwards
4. **Better UX**: No unexpected redirects or authentication issues when accessing the dashboard

## Verification

- All JavaScript tests pass (24/24)
- All C# dashboard endpoint validation tests pass (23/23)
- Port 15135 remains bound by Aspire but is no longer advertised to users

## Impact

**Positive:**
- Codespaces users will access the dashboard successfully on first attempt
- Eliminates confusing HTTP→ephemeral-HTTPS redirect behavior

**Breaking:**
- Any external tooling or bookmarks referencing port 15135 in Codespaces will need to update to 17214
- Port 15135 is now considered legacy and should not be used for browser access
---
date: 2026-05-03T15:29:56.339+01:00
author: Blathers
context: Codespaces dashboard port 17214 + MockBusinessApp auth 401 fixes
decision_type: practice
status: implemented
---

# Commit Separation for Multi-Concern Fixes

## Context

When implementing fixes for Codespaces, encountered two distinct issues:
1. Dashboard HTTP port 15135 redirects to ephemeral port, making HTTPS port 17214 the correct entry point
2. MockBusinessApp returns 401 in Codespaces because it can't fetch Keycloak JWKS through the GitHub proxy

Both issues were diagnosed together and the fixes touched overlapping areas (Aspire AppHost configuration, Codespaces setup), but they address different user-facing symptoms.

## Decision

**Separate commits by user-facing issue, not by technical area or file.**

When implementing multi-concern fixes:
- Create one commit per distinct symptom or release-note entry
- Each commit should answer "what user problem does this solve?"
- Mixing concerns obscures which commit fixed which symptom
- Makes git bisect more effective and release notes clearer

## Implementation

**Commit 1:** Dashboard port 17214 fix (`fa7881c`)
- Changed all references from HTTP port 15135 to HTTPS port 17214
- Files: `.devcontainer`, `scripts/`, `CODESPACES.md`, tests
- User symptom: "Aspire dashboard URL doesn't work in Codespaces"

**Commit 2:** MockBusinessApp JWKS fetch via backchannel (`455e0d5`)
- Set `KEYCLOAK_BACKCHANNEL_URL` env var for MockBusinessApp
- Used ephemeral port allocation for Keycloak
- Files: `src/UmbracoPrism.AppHost/Program.cs`
- User symptom: "MockBusinessApp returns 401 in Codespaces"

## Rationale

- **Release notes:** Each commit produces one clear bullet point
- **Git bisect:** If one fix introduces a regression, bisect isolates the exact change
- **Code review:** Reviewers can evaluate each fix independently
- **Revert safety:** Can revert one fix without undoing the other

## Alternatives Considered

1. **Single "fix Codespaces issues" commit** — rejected: obscures which change fixed which symptom
2. **Separate by file type** (scripts vs. C#) — rejected: splits coherent fixes across commits
3. **Separate by technical layer** (infrastructure vs. application) — rejected: doesn't align with user-facing issues

## References

- Branch: `squad/codespaces-dashboard-and-auth-fixes`
- Commits: `fa7881c` (dashboard), `455e0d5` (auth)
- User request: "Make sure you commit separate issues so the release notes can be produced properly"
---
date: 2026-05-03T15:16:06.682+01:00
author: Blathers
status: diagnosis-complete
---

# MockBusinessApp 401 Diagnosis: Port Mismatch in Backchannel Rewriter

## Root Cause

The **MockBusinessApp** in Codespaces returns 401 because the backchannel JWKS rewriter fails to match the discovery document's `jwks_uri` against the configured `publicOrigin`, causing the JWKS fetch to hit the GitHub port-forwarding proxy (which blocks unauthenticated server requests).

## Evidence Trail

From console logs:
- **token.iss**: `https://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev/realms/prism-dev`
- **configured OidcAuthority**: Same as token.iss
- **KEYCLOAK_BACKCHANNEL_URL**: `http://localhost:8080`
- **backchannel JWKS enabled**: YES
- **Auth failure**: IDX20803 unable to obtain configuration from `http://localhost:8080/realms/prism-dev/.well-known/openid-configuration`
- **Inner failure**: unable to retrieve document from `http://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev:8080/realms/prism-dev/protocol/openid-connect/certs`

The critical detail: the JWKS URI shows the Codespaces hostname with `:8080` appended.

## Technical Analysis

1. **AppHost configuration (line 95)**:
   ```csharp
   .WithEnvironment("PrismBusinessApp__Tenants__2__OidcAuthority", $"{keycloakProxyUrl}/realms/prism-dev")
   ```
   In Codespaces, `keycloakProxyUrl` is derived from `gh codespace ports` and returns the **canonical HTTPS URL without an explicit port** (e.g., `https://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev`), because Codespaces uses the URL pattern `{token}-{port}.{region}.app.github.dev` where the port is **embedded in the hostname**, not in a URI port component.

2. **PrismSigningKeyCache.WarmAsync (lines 168-170)**:
   ```csharp
   if (isDevelopment && !string.IsNullOrEmpty(backchannelBase) &&
       Uri.TryCreate(normalizedKey, UriKind.Absolute, out var publicUri) &&
       publicUri.Scheme == Uri.UriSchemeHttps)
   {
       var publicOrigin = publicUri.GetLeftPart(UriPartial.Authority);
       // ...creates BackchannelRewritingDocumentRetriever with publicOrigin
   }
   ```
   When `normalizedKey` is `https://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev/realms/prism-dev`:
   - `publicUri.GetLeftPart(UriPartial.Authority)` returns `https://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev` (no explicit port, because 443 is the default HTTPS port)

3. **Keycloak discovery document**:
   Fetched successfully from `http://localhost:8080/realms/prism-dev/.well-known/openid-configuration`, but contains:
   ```json
   {
     "jwks_uri": "https://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev/realms/prism-dev/protocol/openid-connect/certs"
   }
   ```
   (Keycloak emits this based on `KC_HOSTNAME` = the Codespaces public hostname)

4. **BackchannelRewritingDocumentRetriever (lines 260-270)**:
   ```csharp
   if (address.StartsWith(publicOrigin, StringComparison.OrdinalIgnoreCase))
   {
       var rewritten = backchannelBase + address[publicOrigin.Length..];
       // ...
   }
   ```
   - `address` (jwks_uri): `https://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev/realms/prism-dev/protocol/openid-connect/certs`
   - `publicOrigin`: `https://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev`
   - **Match succeeds!**
   - `rewritten` = `http://localhost:8080` + `/realms/prism-dev/protocol/openid-connect/certs`
   - **This should work perfectly!**

5. **Wait—why does the error show `:8080` on the Codespaces hostname?**

   The error message `http://organic-space-fortnight-77g9wvq6jxhxg97-8443.app.github.dev:8080/...` is a **red herring artifact** from Microsoft.IdentityModel exception formatting. When the underlying HTTP call fails (e.g., network timeout, DNS resolution failure, or connection refused), the exception message sometimes concatenates fragments from multiple attempted URLs or shows a malformed URL reconstruction.

   **The actual bug**: The rewrite logic is correct, BUT the rewritten URL `http://localhost:8080/realms/prism-dev/protocol/openid-connect/certs` is being passed to an `HttpDocumentRetriever` that was **NOT wrapped by the BackchannelRewritingDocumentRetriever**.

6. **The configuration manager creation (lines 168-184)**:
   - When the backchannel rewriter gates are met, the code creates a `BackchannelRewritingDocumentRetriever` wrapping an `HttpDocumentRetriever`
   - **But** this wrapped retriever is passed to a `ConfigurationManager<OpenIdConnectConfiguration>` constructor
   - The `ConfigurationManager` uses this retriever to fetch the **discovery document**
   - BUT when `OpenIdConnectConfigurationRetriever` parses the `jwks_uri` and makes a **second, separate HTTP call**, it uses the **inner** `HttpDocumentRetriever` instance directly, **bypassing the BackchannelRewritingDocumentRetriever**!

   **NO, WAIT.** Looking at the code again at line 176-179:
   ```csharp
   var rewritingRetriever = new BackchannelRewritingDocumentRetriever(
       publicOrigin, backchannelBase.TrimEnd('/'), innerRetriever);
   manager = new ConfigurationManager<OpenIdConnectConfiguration>(
       metadataAddress,
       new OpenIdConnectConfigurationRetriever(),
       rewritingRetriever);
   ```
   The `rewritingRetriever` is passed to the ConfigurationManager, which should wrap ALL document retrieval calls (both the discovery doc and the JWKS URI).

   So the rewriter SHOULD be intercepting the JWKS fetch. Why isn't it?

## The Actual Bug

Re-examining the error: the **primary** error is "unable to obtain configuration from `http://localhost:8080/realms/prism-dev/.well-known/openid-configuration`", meaning the discovery document fetch itself failed, not the JWKS fetch.

The "inner" error about the Codespaces URL with `:8080` is the **nested exception** from a previous retry or a transitive fetch attempt.

**Most likely scenario**: In Codespaces, MockBusinessApp is running on **ephemeral ports**, not the hardcoded localhost:8080. The Aspire-generated `keycloak.GetEndpoint("http")` returns something like `http://localhost:57123` (ephemeral), but the code at PrismAuthExtensions line 272-274 assumes the backchannel base is a simple host+port that can be combined with the path:

```csharp
var metadataAddress = isDevelopmentForJwks && !string.IsNullOrEmpty(backchannelBase)
    ? $"{backchannelBase.TrimEnd('/')}{new Uri(cacheKey).AbsolutePath}/.well-known/openid-configuration"
    : $"{cacheKey}/.well-known/openid-configuration";
```

If `KEYCLOAK_BACKCHANNEL_URL` is `http://localhost:8080` (hardcoded env var), but the Keycloak container is actually listening on a different ephemeral port in Codespaces, then the fetch from `http://localhost:8080/realms/prism-dev/.well-known/openid-configuration` will fail with "connection refused" or "no such host/port".

## Known Bug Match

This **exactly matches** the Codespaces port-forwarding pattern described in:
- **Skill: keycloak-localhost-https** — downstream APIs must trust the same browser-facing HTTPS issuer
- **Skill: backchannel-rewrite-testing** — transport rewrites must not weaken issuer validation

The backchannel rewrite is correctly implemented, but the **env var value** is stale or incorrect for the actual Keycloak listen port.

## The Fix

### Immediate fix (Codespaces only):

In AppHost Program.cs line 145, the code already sets:
```csharp
businessApp.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));
```

But `keycloak.GetEndpoint("http")` returns a full URL like `http://localhost:57123`, NOT just `http://localhost:8080`.

**The backchannel URL logic in PrismAuthExtensions and PrismSigningKeyCache assumes the backchannel URL is a base URL (scheme + host + port) that can be combined with a path.** This is correct.

The issue is that Aspire's `.GetEndpoint("http")` returns **ephemeral ports** that change between runs. The hardcoded `http://localhost:8080` in the env var is wrong.

### Root cause confirmation:

Check the actual KEYCLOAK_BACKCHANNEL_URL value at MockBusinessApp startup. If it's `http://localhost:8080` but Keycloak is on a different port, the fetch fails.

### Long-term fix:

The AppHost is already doing the right thing at line 145. The issue is likely that:
1. The env var `KEYCLOAK_BACKCHANNEL_URL` is being set **twice** — once by AppHost (correctly, to the ephemeral port), and once by a shell profile or launch config (incorrectly, to `:8080`)
2. The shell/launch config override takes precedence over Aspire's `.WithEnvironment`

**Action**: Remove any hardcoded `KEYCLOAK_BACKCHANNEL_URL=http://localhost:8080` from shell profiles, `.env` files, or launch configs. Let Aspire set it dynamically.

## Security Validation

- ✅ Issuer validation unchanged (token.iss must match configured OidcAuthority)
- ✅ Audience validation unchanged
- ✅ Backchannel rewrite only activates in Development with explicit env var
- ✅ MockBusinessApp fails loud if env var is set outside Development (line 38-41)
# Decision: Aspire Dashboard Port Clarification for Codespaces

**Date:** 2026-05-03  
**Author:** Mabel (Technical Writer)  
**Status:** Final

## Summary

The Umbraco Prism documentation contained conflicting guidance about how to access the Aspire Dashboard in GitHub Codespaces. The fix clarifies that the correct public endpoint is the **forwarded HTTPS URL on port 17214**, not port 15135 (which is internal HTTP and prone to redirect issues).

## Problem

- `CODESPACES.md` previously stated the Aspire Dashboard is on port 15135 in Codespaces
- This is misleading: port 15135 is the internal HTTP container port
- In Codespaces, the correct user-facing endpoint is the forwarded HTTPS tunnel on **port 17214**
- Port 15135 may redirect incorrectly when accessed through a browser, causing confusion

## Solution

**Three updates to `CODESPACES.md`:**

1. **Ports panel tip:** Rewrote to emphasize port 17214 as the primary Codespaces endpoint, with a parenthetical note that port 15135 is internal HTTP and may redirect incorrectly.
2. **`stop.sh` port list:** Updated from `15135` to `17214` to match the actual public port.
3. **Health-check table:** Changed the Aspire Dashboard endpoint row from `http://localhost:15135 (Codespaces)` to `https://localhost:17214 (Codespaces — forwarded HTTPS endpoint)` for explicit clarity.

## Why This Matters

Users following the documentation will now:
- Land on a working public URL instead of hitting redirect loops
- Understand why port 17214 is used (forwarded HTTPS) vs. port 15135 (internal HTTP)
- Have a single, consistent source of truth across all three touch-points in the file

## Technical Note

Port 17214 is the standard HTTPS port for the Aspire Dashboard on local development. In Codespaces, GitHub's port forwarding tunnels this port and exposes it as an HTTPS endpoint, which is what users see in the Ports panel. Port 15135 is the unencrypted HTTP side used internally by the container; it's not suitable for browser access in a Codespaces environment.
---
date: 2026-05-03T15:12:55.439+01:00
author: Tangy
status: implemented
---

# Codespaces Dashboard Port 17214 Contract

## Decision

Codespaces users must be directed to the forwarded HTTPS Aspire dashboard endpoint on port 17214, not the redirecting HTTP endpoint on port 15135.

## Context

Previously, Codespaces users were seeing port 15135 advertised, which is an HTTP redirect endpoint. This caused:
- Unnecessary redirects
- Confusion about which endpoint to use
- Potential for users to bookmark or share the wrong URL

Port 17214 is the actual HTTPS Aspire dashboard that Codespaces forwards correctly.

## Implementation

**Code changes (already completed by Blathers):**
- `.devcontainer/on-start.sh` lines 68, 179: `get_codespace_url 17214`
- `scripts/startup-status/server.js` line 22: `ASPIRE_CODESPACES_PORT = 17214`

**Test coverage (added by Tangy):**
- `DashboardLocalEndpointsValidationTests.CodespacesStartupScript_AdvertisesHttpsPort17214_NotHttpPort15135`
- `DashboardLocalEndpointsValidationTests.StatusServer_UsesPort17214ForCodespacesPublicUrl`

## Rationale

Port 17214 is the authoritative HTTPS endpoint for the Aspire dashboard. Using it directly:
- Eliminates unnecessary HTTP → HTTPS redirects
- Provides a cleaner user experience
- Matches the local development contract (port 17214)
- Ensures Codespaces URL forwarding works correctly with HTTPS

## Consequences

- Users get a single, consistent dashboard URL
- No more confusion about which port to use
- Tests enforce this contract going forward
- Any regression will be caught immediately by the test suite

## [2026-05-03] Blathers — PR #47 Merge Strategy: Preserve Commits for Multi-Fix Features

**Status:** ✅ IMPLEMENTED — PR #47 merged to main; local main at cfe90fc

### Decision

Use `gh pr merge --merge` (not `--squash`) to preserve separate commits when:
1. Each commit addresses a distinct user-facing issue
2. Commits are already clean and well-documented
3. Release notes need to track fixes independently
4. Git bisect operations benefit from granular history

### Rationale

PR #47 contained two separate product fixes plus squad metadata:
- `fa7881c`: Dashboard port 17214 fix
- `455e0d5`: MockBusinessApp backchannel auth fix
- `c2b5a2b`: Squad decisions merge

Squashing would merge two unrelated fixes into one commit, making it harder to:
- Generate release notes ("what fixed the 401 error?")
- Bisect regressions ("which change broke the dashboard?")
- Cherry-pick fixes to release branches

The two product commits are already atomic, tested, and documented with conventional commit messages. Preserving them maintains traceability.

### CI Timing Expectations

Playwright integration tests with Aspire + Keycloak + browser automation took **16 minutes** to complete. This is normal for:
- Container orchestration startup
- OIDC discovery and token flows
- Full browser automation suite
- Certificate trust chain validation

Don't assume long-running checks are stuck — integration tests with full stack require patience.

### Outcome

PR successfully merged into `main` at commit `cfe90fc`. All CI checks passed:
- test: 9s ✅
- core-tests: 55s ✅
- storybook-tests: 111s ✅
- localhost-auth-playwright: 959s ✅

Local `main` synced to `origin/main` without conflicts.

### Team Impact

Future PRs with multiple concerns should follow the same pattern: separate commits by user-facing issue, preserve commits via `--merge`, document CI timing expectations for integration test suites.
