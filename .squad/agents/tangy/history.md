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

## Session: Backchannel Rewrite Regression Tests (2025-07-XX)

**Status:** ✅ Complete — 11 new tests, 642 total passing

**Task:** Regression coverage for Development-only backchannel URL rewrites:
- Copper's refresh-token rewrite (`PrismContext.RefreshTokenAsync`)
- Blathers' JWKS rewrite (`PrismAuthExtensions.ResolveSigningKeys`)

**Security Fix Found & Applied:**
`PrismAuthExtensions.ResolveSigningKeys` was missing the `isDevelopment` check on the JWKS backchannel rewrite path. Only `KEYCLOAK_BACKCHANNEL_URL` was checked; now requires `ASPNETCORE_ENVIRONMENT=Development` too. Matches Copper's dual-gate pattern.

**Test File:** `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs`

**Coverage (3 groups):**
- Group A: Refresh-token rewrite gating — endpoint URL, dev vs. prod gating, issuer validation resilience
- Group B: JWKS fetch rewrite gating — metadataAddress capture via mock IPrismSigningKeyCache, dual-gate verification
- Group C: Bedrock invariants — ValidateIssuer/ValidateAudience always true, MockBusinessApp fail-loud guard exists

**Test Stability Fix:**
Added `EnvVarSensitiveTestCollection` to serialise `BackchannelRewriteTests` and `PrismSigningKeyCacheTests`. Parallel env-var leakage (KEYCLOAK_BACKCHANNEL_URL + ASPNETCORE_ENVIRONMENT=Development) caused intermittent failures in `WarmAsync_WithMetadataAddress_RequiresHttps_ForHttpsUrl`.

**Key Learnings:**
- `BackOfficeTenant` is a positional record — config keys must match property names exactly: `EntraTenantId`, `ClientId`, `Code`, `DisplayName`, `OidcAuthority`. Wrong keys (`OidcClientId`) silently produce empty tenant lists causing early return in `ResolveSigningKeys`.
- JWKS tests need mock `IPrismSigningKeyCache` registered BEFORE `AddPrismAuthentication` (uses `TryAddSingleton`). Mock must return `IsExpired: true, ContainsRequestedKey: false` to trigger `WarmAsync` call.
- Env var mutations in parallel tests need `[Collection]` isolation to prevent flakiness.
- Path from test binary to solution root: `AppContext.BaseDirectory` = `bin/Release/net10.0/` → 5× `../` to reach solution root.

---


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
