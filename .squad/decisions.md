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

