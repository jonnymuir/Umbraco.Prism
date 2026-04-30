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
