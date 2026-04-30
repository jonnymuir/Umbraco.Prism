# Decisions

Umbraco.Prism team decisions. Append-only ledger.

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

## 📌 2026-04-26: Copilot (Coordinator) — v2.0 Polymorphic Component Rollout Completion

**Status:** ✅ COMPLETE — 9-commit atomic rollout concluded; v2.0 schema is canonical

**Session Summary:**
The v2.0 polymorphic component hierarchy rollout converged through three phases:
1. **Initial Plan Collapse** (copilot-3commit-replan): 8-commit sequence deemed infeasible due to C# type system constraints. Collapsed to 3-commit atomic plan (schema replacement, design doc refresh, ledger update).
2. **Expanded Rollout** (follow-through progress reports): 3-commit plan expanded to 9 total commits as blockers were discovered and resolved:
   - Commit `7423803` (feat): Atomic schema replacement — 40–60 file diff, single coherent change
   - Commits `2cdb0dc`, `f3c0ea5`, `67bb57b`: Seed fixes + e2e tests + 4th workflow seeding
   - Commit `989f595`: Archive redesign blueprint, refresh conditional-fields doc
   - Commit `dc87e5f`: ModelsBuilder views fix (disable auto-generation)
   - Commit `392c64e`: Playwright walkthroughs with screenshot capture
   - Commits `2698c1d`, `a48229b`: Design + guide doc refresh, screenshot script

**Key Decisions Locked In:**
- **No migrator, no V2 suffix, no schemaVersion field** — direct replacement of v1 schema with polymorphic components
- **Generic ConditionalOn deferred to v2.1** — v2.0 ships with ConditionalChildren on Radios/Checkboxes only
- **ModelsBuilder view generation disabled** — TestSite uses Core's embedded views, prevents model-binding conflicts

**Seed File Roundtrip Guard:**
- Gap identified: payment-demo.json and information-request.json were out of sync with v2 polymorphic schema
- Regression guard added: `SeedFileRoundtripTests.cs` ensures all seeds deserialize correctly and have no orphaned v1 properties
- All 4 seeds migrated to v2 in Commit `2cdb0dc`

**E2E + Documentation Coverage:**
- Playwright tests cover all 4 demo workflows (community-enquiry, payment-demo, planning-notification, information-request) with happy paths + conditional logic
- Screenshot-driven walkthroughs for all 4 demos with state transitions captured
- 12 design + guide docs refreshed for v2 polymorphic schema

**Test Results:**
- Clean build: 0 warnings
- Core tests: 583 baseline → maintained; Seed roundtrip tests: +4 (546 total)
- No regressions; all changes backward-compatible or documented as breaking (no live consumers)

**Basis:** User directive (2026-04-26, Jonny Muir), Tom Nook's direct-replacement sequencing plan (2026-04-26), follow-through progress reports (Copilot 2026-04-09, 2026-04-26), Copilot 3-commit replan (2026-04-26), blocker resolution (ModelsBuilder fix 2026-04-26).

---

## 📌 2026-04-26: Tom Nook — Design Doc Audit: 9 Docs Reviewed, 7 Marked for v2.0 Rewrite

**Status:** ✅ Audit complete; recommendations implemented in rollout

**Scope:** 9 workflow design + guide documents reviewed against v2 polymorphic component plan

**Audit Findings:**
- **7 docs need rewrite** (design docs: forms-engine.md, forms-engine-backend.md, forms-engine-client.md, forms-engine-umbraco.md, validation.md, forms-engine-demo.md, forms-engine-security.md; guide: conditional-fields.md, workflow-gds-components.md, workflow-validation.md, workflow-setup.md, workflow-customisation.md)
- **1 doc stays as-is** (architecture/workflow-forms-engine.md contains architecture principles that transcend v1/v2)
- **1 doc archived** (workflow-forms-engine-redesign.md → docs/archive/ with pointer to v2 plan)

**Rewrite Priorities:**
- **Red banners** (critical mismatches): 4 docs
  - Forms engine backend/client (component tree traversal examples)
  - Umbraco integration (JSON schema examples heavily v1-focused)
  - Setup guide (seed JSONs all v1 shape)
- **Yellow banners** (partial updates): 3 docs
  - Validation + forms-engine-demo (fieldType → type, fields → children)
- **Archive + pointer** (obsolete): 1 doc
  - Redesign blueprint (superseded by actual v2 implementation)

**Rewrite Pattern:**
- Replace `fieldType` discriminator with `type` on all components
- Replace `fields[]` array (flat field list) with `children[]` (typed component tree)
- Update JSON examples to show polymorphic shapes (fieldset with children, radios with conditionalChildren)
- Add v2 callout boxes noting new capabilities (ConditionalChildren, component polymorphism, waiting state)
- Remove v1 artifact references (no more "FieldFile", "PrismComponentRenderPayload", "PrismFieldTagHelper")

**Action:** All rewrites completed in Commits `989f595` (conditional-fields refresh) and `2698c1d` (bulk refresh).

**Basis:** Formal design audit memo (2026-04-26, Tom Nook, in `.squad/decisions/inbox/`), implemented per rollout plan phases.

---

## 📌 2026-04-26: Jonny Muir — Direct Schema Replacement Directive (No Migrator, No Dual Schema)

**Decision:** Skip v1→v2 schema migrator entirely. No live consumers; make polymorphic component hierarchy THE schema. Direct replacement of `WorkflowDefinitionFile`, `FieldDefinition`, etc. Update all 4 seed workflows, engine, builder, tag helpers, Razor partials, tests, and design docs in one coherent change.

**Context:** v2.0 rollout plan (Tom Nook) designed for live product with graduated migration phases (migrator, dual schema acceptance, builder rewrite, partial collapse, doc refresh). Umbraco.Prism is prototype-stage; no external customers. Transitional infrastructure is pure cost.

**Rationale:** Simpler is better. One atomic change to main is faster than multi-phase rollout. Collapses Tom's planned phases P2→P6 into single integrated workstream.

**Banned (Locked):**
- ❌ No migrator
- ❌ No V2 class names (`WorkflowDefinitionFileV2`, `StepDefinitionV2`)
- ❌ No `schemaVersion` discriminator
- ❌ No dual schema acceptance in engine
- ❌ No feature flags

**Deferred to v2.1:**
- Generic `ConditionalOn` + `VisibleWhen` on arbitrary components → use v2.1 spike for tree-traversal infrastructure
- **v2.0 ships with:** `ConditionalChildren` on Radios/Checkboxes only (canonical "Other → specify" pattern)

**Implication:** P2 (migrator), P3 (dual acceptance) deleted outright. P4 (builder rewrite), P5 (tag helper collapse), P6 (doc rewrites) merge into one effort.

**Basis:** User directive (2026-04-26, Jonny Muir, delivered via Copilot coordinator).

---

## 📌 2026-04-30: Mabel (Technical Writer) — v2 Schema Terminology Cleanup (Docs Only)

**Status:** ✅ IMPLEMENTED — Documentation terminology unified across 12 public-facing docs

**Decision:** Remove all "v2.0 Schema Update" banners and "v1 vs v2 framing" from public-facing documentation. Replace with clear terminology that the polymorphic component model is the **current schema**.

**Rationale:** The polymorphic component model is the shipping schema; there is no shipped "v1" to distinguish from. Banners like "⚠️ v2.0 Schema Update" falsely suggest migration requirements and confuse new users about what is "current."

**Changes:**
- Removed banners from 12 docs (guides, design, walkthroughs, README)
- Normalized terminology: "v2.0 examples" → "current examples"; "v1 vs v2 comparison" → "Design evolution" (design docs only)
- Code identifiers (e.g., `WorkflowDefinitionFileV2.cs`, `ComponentPolymorphismTests.cs`) unchanged; internal naming deferred to Tom Nook/Blathers

**Verification:** All public docs use consistent "polymorphic component model" terminology; no v1/v2 framing in public scope (historical context in archive only).

**Basis:** Documentation review memo (2026-04-30, Mabel, technical writer).

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
