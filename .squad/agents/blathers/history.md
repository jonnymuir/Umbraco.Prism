# Blathers — History

## Core Context

This agent manages backend services, authentication infrastructure, and CI/CD workflows.

**Key domains:** Auth/OIDC, Aspire local dev, CI infrastructure, Database services, Security hardening, Playwright/E2E

## 📋 Recent Sessions

---

## 📌 2026-04-30: Cross-Agent Note — V2 Code Identifiers Naming Review

**Alert:** Mabel's documentation cleanup (2026-04-30) flagged that source code identifiers like `WorkflowDefinitionFileV2.cs` and `ComponentPolymorphismTests.cs` retain "V2" suffixes.

**Question:** Should internal code identifiers be renamed as part of future cleanup? (Joint decision with Tom Nook; no immediate action required.)

---

## Session: Workflow Developer Experience Improvements (2026-04-28)

**Status:** ✅ Complete

**Work:** Client-side validation and Playwright test readiness improvements post-v2.0 rollout

**Key Fixes:**
1. Removed client-side blur validation (caused layout shift → failed form submissions)
2. Removed client-side submit interception (competing DOM mutations with GDS server-side error summary)
3. Fixed checkbox display value formatting (multi-valued checkboxes now render with proper separators)
4. Server is now the only validation source (prism-workflow-validation.js handles only form.noValidate + character counters)

**Playwright Readiness:**
- Fixed cold-Razor-view first-render race that caused first test per spec file to timeout
- Added 5 HTTP behavioural probes to pre-warm all workflow routes
- Expanded seed-contract gate to validate all 4 workflow pages + dashboard
- Result: ✅ localhost-auth-playwright lane now green

**Test Results:** 10 passed, 2 pre-existing TestSite binding failures (unrelated)

---

## Session: Option 1 Regression Fix (2026-04-26)

**Status:** ✅ Complete — Commit `7e55151` merged

**Bug Fixes:**
1. **Decimal field validation gap:** WorkflowFieldValidator didn't recognize `"decimal"` as numeric type → min/max constraints silently ignored. Added `"decimal"` case to validation logic + unit test.
2. **Planning confirmation incomplete:** Missing reference number body component in planning-notification.json seed. Added reference number body component.

**Root Cause:** Atomic v2.0 schema swap (commit `7423803`) introduced `"decimal"` type but validation wasn't updated; seed migration dropped confirmation body text.

**Test Results:** +1 new decimal validation test; 543/547 passing (same 4 pre-existing TestSite failures)

**Blind Spots:** No compile-time guarantee all field types handled in validator; seed JSON lacks schema enforcement; single 5000+ line atomic commit created spread-out fixes.

---

## Session: Workflow v2.0 Phase 1 — Polymorphic Component Hierarchy (2026-04-26)

**Status:** ✅ Complete — 9-commit atomic rollout concluded

**Scope:** Atomic direct replacement of v1 schema with v2 polymorphic components (no migrator, no dual schema, no feature flags)

**Key Implementation:**
- Abstract `PrismComponent` base + sealed derived types (16 component types)
- `[JsonPolymorphic]` with `"type"` discriminator
- `FieldsetComponent.Children: PrismComponent[]` replaces flat `fields[]`
- `ConditionalChildren` on Radios/Checkboxes only (v2.1 for generic conditionals)
- ModelsBuilder view generation disabled (TestSite uses Core's embedded views)

**Seed Roundtrip Guard:**
- All 4 seeds migrated to v2; added `SeedFileRoundtripTests` to catch schema drift
- Regression guard: all seeds deserialize correctly, no orphaned v1 properties

**E2E + Docs:**
- Playwright tests cover all 4 demos (happy paths + conditionals)
- Screenshot-driven walkthroughs for all 4 demos
- 12 design + guide docs refreshed for v2

**Test Results:** Clean build (0 warnings); 583 tests maintained; +4 seed roundtrip tests (546 total); no regressions

---

## Prior Work Summary (2026-04-22 and earlier)

**2026-04-22:**
- stepType Removal & Component Model Unification (decision made; v1 vs v2 architecture)
- Compound Content Field Types implementation
- GDS Component Model foundation work

**2026-04-21:**
- Instance Policy Implementation (single/multiple/prompt policies)
- Field Group API Endpoints
- Security Hardening Phase 2

**2026-04-20:**
- GDS Workflow Models Phase 1 Completion
- GDS Workflow Models Evolution analysis

**2026-04-14 (Pre-v2.0):**
- Aspire localhost auth CI job (manual rerun + Linux cert trust)
- Phase 1 Security Regression CI Test Fix
- CI workflow infrastructure baseline

**Key Learnings:**
- Server-side validation is source of truth; client-side decorative only
- Atomic breaking changes create spread-out follow-up fixes
- E2E tests catch subtle validator + seed gaps that unit tests miss
- Playwright pre-warming (HTTP probes) essential for cold-route performance
- Decimal as distinct type aligns with GDS component model
- Aspire restarts between test files have high overhead (~1 min per file)

---
