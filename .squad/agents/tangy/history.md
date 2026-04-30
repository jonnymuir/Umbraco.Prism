# Tangy — History

## Core Context

QA validation, test coverage analysis, and edge-case identification.

**Key domains:** Playwright testing, E2E validation, Edge case coverage, CI/CD readiness, Performance analysis

## 📋 Recent Sessions

---

## 📌 2026-04-30: Cross-Agent Note — V2 Decimal Validation Test Coverage

**Context:** Blathers' 2026-04-28 option 1 fix added decimal field validation. Noted as blind spot: "No compile-time guarantee all field types handled in validator."

**Recommendation for Future:** Add comprehensive test suite for WorkflowFieldValidator covering ALL field types (`text`, `number`, `decimal`, `email`, `date`, `radios`, `checkboxes`, etc.) + constraint combinations. Extract field types to shared enum/constants to enable exhaustiveness checks.

---

## Session: Instance Policy Test Suite (2026-04-21)

**Status:** ✅ Complete — 19 new tests, 512 total passing

**Coverage:**
- Single policy: find-or-create behavior, parameter validation
- Multiple policy: new instance per call, resume by ID
- Prompt policy: picker trigger, action precedence, terminal state handling
- Cross-policy: access control (tenant/user isolation), lookup key consistency, concurrency

**Test File:** `src/UmbracoPrism.Core.Tests/Business/Workflow/BusinessAppWorkflowEngineInstancePolicyTests.cs`

**Strategy:** Arrange-Act-Assert pattern; multi-tenant security verified; zero regressions

---

## Prior Work Summary (2026-04-20 and earlier)

**2026-04-20:**
- GDS Field Type Test Coverage Phase 1 Completion (validator tests)
- Playwright E2E Tests for Planning Workflow (happy path + conditions)

**2026-04-19:**
- GDS Phase 2 — Playwright E2E for Planning Workflow

**2026-04-15:**
- GDS Field Type Test Coverage (new field types in validator)
- Workflow Builder Test Coverage

**2026-04-14:**
- Aspire localhost auth CI job QA
- Phase 1 Security Regression CI Test Fix

**Key Learnings:**
- Test-driven seeding strategy: create minimal JSON seeds programmatically in `IDisposable` fixtures (test isolation + real engine loading)
- GDS patterns validation: error summary, summary list, confirmation panel
- Web component tests target rendered HTML, not component tags
- Edge cases in multi-policy state machines best covered by cross-policy test scenarios
- Field type exhaustiveness requires shared enum or compile-time verification

---
