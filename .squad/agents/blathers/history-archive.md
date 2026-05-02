# Blathers — History Archive

**Summarized:** 2026-05-02  
**Sessions archived:** Pre-Codespaces 401 investigation (2026-05-01 and earlier)

---

## Session: Workflow Engine Rams-Grade Review (2026-05-01)

**Status:** ✅ Complete — review written to `.squad/reviews/2026-05-01-prism-reflection/03-blathers-workflow.md`

**Scope:** Deep review of the workflow engine and business app integration against Dieter Rams' 10 Principles of Good Design. Covered: `PrismComponent` hierarchy, `PrismComponentRenderPayload`, `WorkflowDefinitionBuilder`, `BusinessAppWorkflowEngine`, `PrismWorkflowPageController`, `WorkflowFieldValidator`, advance API contract, convention-based partial dispatch.

**Key Findings:** (1) Hardcoded business rule in generic engine — domain rule embedded in `BusinessAppWorkflowEngine.Advance()`, invisible to designers; (2) `PrismComponentRenderPayload` is a 20-property flat bag, contradicts clean design-time sealed record hierarchy; (3) Advance API contract leaks JsonElement; (4) Service designer journey is code-first (good via builder, obscure via JSON seed); (5) String enums everywhere; (6) `InferStepType()` is implicit magic.

**Rams Scorecard:** 4 × ✅, 5 × ⚠️, 1 × ❌ (Principle 10).

---

## Session: PR #40 PT2 Backend Security Batch — 5 Findings Fixed (2026-04-30)

**Status:** ✅ Complete — 5 commits merged as `83eb30e` on `main`

**Scope:** Close five PT2 security findings (SEC-PT2-003, 004, 006, 009, 010). Backend hardening: logout-CSRF, security headers, DataProtection persistence, Capacitor JSON antiforgery policy, origin restrictions.

**Test Results:** Baseline: 601 tests passing; After: 618 tests passing (+17 new); Status: All green; no regressions.

---

## Session: PR #38 CI Green — MockBusinessApp Sanitizer Fix (2026-04-30)

**Status:** ✅ Complete — Commit `6751662` on `fix/ci-green` (merged as `dc316fb` on main)

**Scope:** Fix `localhost-auth-playwright` CI timeout by registering `IWorkflowContentSanitizer` in MockBusinessApp's DI container.

---

## Session: SEC-003 — Sanitizer Wire-Up (2026-04-30)

**Status:** ✅ Complete — Commit `4223861` pushed to main

**Scope:** Wire the `IWorkflowContentSanitizer` abstraction across Core + MockBusinessApp per Tom Nook's SEC-003 proposal. Test delta: 550 → 554 passing + 6 skipped.

---

## Session: SEC-004 — Rotate Leaked HMAC Key & Extract TestSite Secrets (2026-04-30)

**Status:** ✅ Complete — Commit `b6336fd` pushed to main

**Scope:** Remediate SEC-004: remove committed `Umbraco:CMS:Imaging:HMACSecretKey` from `appsettings.json`; extract `Prism:VaultUri`; prevent re-leak via `appsettings.Local.json` pattern.

---

## Session: V2 Suffix Rename — Workflow Definition Types (2026-04-30)

**Status:** ✅ Complete — Commit `290a18c` pushed to main

**Scope:** Drop the meaningless `V2` suffix from workflow code identifiers. `WorkflowDefinitionFileV2.cs` deleted, `StepDefinitionV2` eliminated, test folder renamed.

---

## Session: Workflow Developer Experience Improvements (2026-04-28)

**Status:** ✅ Complete

**Work:** Client-side validation and Playwright test readiness improvements. Removed client-side blur validation and submit interception; fixed checkbox display value formatting.

---

## 📦 Full archived session details

Prior sessions and their complete context remain accessible in git history and prior squad records.
