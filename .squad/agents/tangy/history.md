# Tangy — History (Summary)

**Agent:** Tester specializing in browser contracts, diagnostics, and API validation for Codespaces environments.

**Recent focus (2026-05-04):** Walkthrough/test coverage audit, test inventory analysis, missing operator/admin flow identification.

---

## Current Work (2026-05-04 onwards)

### Walkthrough Coverage Audit (2026-05-04)
- **Scope:** Audit all Playwright tests and walkthrough specs for comprehensive coverage analysis
- **Deliverable:** Coverage audit report identifying gaps in operator/admin flows, mobile testing, and edge cases

---

## Previous Work (2026-05-04)

### Workflow 401 Root Cause Analysis
- **Finding:** Two distinct 401 sources produce identical surface error in `BusinessAppWorkflowClient`
  1. Null auth header silently dropped (JWT middleware 401)
  2. Application-level `Results.Unauthorized()` vs `Results.Problem()` inconsistency
- **Regression Tests Added:** 3 new tests in `BusinessAppWorkflowClientTests.cs` document exact contract
- **Diagnostics:** `[PRISM AUTH FAILED]` console log distinguishes JWT validation from application guard

### Workflow 401 Null-Auth Contract Decision
- **Proposed:** Logging when `GetAuthorizationHeaderAsync` returns null
- **Proposed:** Align workflow handlers to `Results.Problem()` for consistency with `/api/backoffice/me`
- **Status:** Merged to decisions.md (PROPOSED, Tangy, 2026-05-04)

### Key Learnings
- Null auth header in `CreateClientAsync` is silent danger—omits header without logging/throwing
- JWKS fix (0904810) necessary but insufficient if `PrismTenantMiddleware` fails tenant resolution
- Safe transport diagnostics pattern: classify failure modes without exposing ports/secrets

## Dispatch: CI Test-Fragility Fix (2026-05-04T08:22:01Z)

**Outcome:** Land the approved CI test-fragility fix and push to main.

**Background:** Two root causes from CI run 25294216756 (commit `beef21c`):
1. `PrismContextTests` reads env vars but was not in `EnvVarSensitiveTestCollection` → race condition
2. Moq setup used concrete `CancellationToken` matcher on Linux → lazy init mismatch

**Fixes delivered:** 
- Commit 860c5d3: Added `PrismContextTests` to `EnvVarSensitiveTestCollection`
- Commit 1601415: Replaced concrete `httpContext.RequestAborted` with `It.IsAny<CancellationToken>()` in 4 test methods

**Status:** Dispatched for final verification and merge to main.

---

## Learnings (2026-05-04) — Walkthrough Coverage Audit Findings

### Test Inventory Summary
- **Active Automated Tests:** 20 tests across 6 spec files
  - `localhost-auth-session.spec.ts`: 8 tests (auth/session/API contracts)
  - `workflow-gds-journey.spec.ts`: 5 tests (planning workflow journeys + edge cases)
  - 4 walkthrough specs with automated tests (community-enquiry, payment-demo, planning-notification, information-request)
- **Manual-Only Walkthroughs:** 5 specs with `test.skip(true)` (authoring, building-mobile-app, creating-tenant, design-system, push-notifications)

### Coverage Strengths
- ✅ All 4 end-user workflow happy paths have executable tests
- ✅ Conditional reveals tested (community-enquiry, planning-notification)
- ✅ Check-answers edit flow tested (workflow-gds-journey)
- ✅ Form validation tested for payment-demo and planning-notification
- ✅ Auth/session contracts comprehensive (8 tests including restart)
- ✅ Helper pattern (`assertHealthyPage`, `step()`) enforces "assert before screenshot" rule

### Coverage Gaps
1. **Operator/Admin Workflows:** Completely absent in automated tests
   - Backoffice login, tenant creation, workflow authoring, design system config
   - All relegated to manual-only walkthroughs (acceptable per R6)
2. **Back/Edit Flows:** Only planning-notification tests check-answers → change pattern
   - Missing: community-enquiry, payment-demo, information-request back flows
3. **Form Validation:** Only 2 of 4 workflows have validation tests
   - Missing: community-enquiry, information-request field validation
4. **Success States:** Information request doesn't assert "under-review" submission state
5. **Mobile Rendering:** No mobile viewport tests (all desktop-only)
   - Home page, workflows, and forms untested on mobile
6. **Home Page Navigation:** No dedicated walkthrough for hero/homepage → workflow entry

### Operator Flow Classification
- **Creating/Managing Tenants:** Manual-only (backoffice UI, OIDC setup — reasonable to keep manual)
- **Authoring Workflows:** Manual-only (requires C# fluent API knowledge — reasonable)
- **Design System Configuration:** Manual-only (Umbraco backoffice task — reasonable)
- **Mobile App Building:** Manual-only (Xcode/Android Studio — not browser-testable)
- **Push Notifications:** Manual-only (service worker, browser permissions — partially automatable but lower priority)

### Test Quality Observations
- All tests use `assertHealthyPage()` with URL + heading validation (good practice)
- `step()` helper ensures screenshot preconditions are verified
- Error handling uses standard `.govuk-error-summary` pattern
- No accessibility assertions (a11y) in any tests
- No timeout/long-operation assertions beyond default expect timeout

### Recommended Next Actions (by impact)
1. **Add back/edit flow tests** to 3 workflows (community-enquiry, payment-demo, information-request)
2. **Add field validation tests** to 2 workflows (community-enquiry, information-request)
3. **Add mobile viewport tests** to all 4 active walkthroughs
4. **Add success state assertion** to information-request (submission confirmation)
5. **Create home page hero walkthrough** (new shared/home-hero.walkthrough.spec.ts)

---

## Learnings (2026-05-04) — CI Failure Analysis: PrismContextTests

### Root Cause: Fragile CancellationToken Moq Matcher
- **CI run 25294216756** (commit `beef21c`) failed with `NullReferenceException` at `PrismContext.cs:212` in 4 `PrismContextTests` methods.
- **Not a regression in PrismContext.cs** — the production code was correct throughout. The bug was in the tests.
- **Root cause:** Mock setups used `httpContext.RequestAborted` as a concrete value matcher for the `CancellationToken` parameter on `IPrismTokenRefreshService.RefreshAsync`. On Linux (CI/Ubuntu), `DefaultHttpContext.RequestAborted` lazy-initialises its `CancellationTokenSource` via `IHttpRequestLifetimeFeature`; if the feature is activated between setup-time and call-time (during ASP.NET Core's authentication stack), the captured token no longer equals the one used in the real call. Moq returns `default(TokenRefreshResult) = null`, causing `result.Success` to throw.
- **Platform masking:** On macOS arm64 the lazy init path produces stable results, hiding the fragility completely.
- **Fix:** Replace `httpContext.RequestAborted` matchers with `It.IsAny<CancellationToken>()` in Setup and Verify for the 4 affected tests. The tests verify endpoint routing and bearer token return — not the exact CancellationToken instance.
- **Pattern to watch:** Never use a concrete `CancellationToken` value as a Moq matcher when that value comes from a lazily-initialised ASP.NET Core feature (`DefaultHttpContext.RequestAborted`, `HttpContext.RequestAborted`). Always use `It.IsAny<CancellationToken>()`.

---

## Learnings (2026-05-04) — CI Fix Landing: Approved Revision to main

### Task: Land Approved PrismContextTests Fix on main
- **CI run fixed:** 25294216756 (`beef21c`), which failed `core-tests` with 4 `NullReferenceException` in `PrismContextTests`.
- **Superseded commit:** `860c5d3` (Blathers) — added `EnvVarSensitiveTestCollection` to `PrismContextTests`; reduced but did not eliminate fragility; still resulted in CI failure in subsequent run `25309298569`.
- **Approved revision landed:** `1601415` (Tangy) — replaced concrete `httpContext.RequestAborted` Moq matchers with `It.IsAny<CancellationToken>()` in 4 `PrismContextTests` methods.
- **Pushed to origin/main** as part of commit chain ending at `d9fb7f7` (Scribe's decision merge, which included Tangy's decision entry). Substantive fix commit is `1601415`.

### Diagnostics pattern: gitignored stub views causing local-only test failures
- `TestSiteViewModelBindingTests` appeared to fail locally (4 failures) but pass in CI. Root cause: `workflowHub.cshtml` and `workflowPage.cshtml` are gitignored stubs (`.gitignore` lines 510-511) generated locally by ModelsBuilder; they do not exist in CI checkout, so the test correctly returns without failing there.
- **Pattern:** Local-only test failures caused by gitignored generated files are not CI regressions. Verify with `git check-ignore -v` before concluding a failure is CI-relevant.

### Verification method
- CI job `core-tests` in failed run showed: Failed: 4, Passed: 686, Total: 690 — all 4 failures were PrismContextTests NullReferenceException.
- Post-fix local run (after `dotnet build`): Failed: 4 (TestSiteViewModelBindingTests, local-only), Passed: 686 — PrismContextTests all green; CI-relevant pass count matches expected 690.

---

## Decision Archive

See `.squad/agents/tangy/history-archive.md` for detailed session logs from 2026-05-03 including:
- Downstream timeout diagnosis and operator flow reduction
- Transport diagnostics validation (5 behavioral contract tests, 680 tests passing)
- Business API arrival instrumentation trace ID forwarding
- Environment variable configuration diagnostics patterns

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-04 | Walkthrough Coverage Hardening

**Status:** PROPOSED

Completed walkthrough coverage audit hardening across five gaps:

**D1:** Viewport-first screenshots (fullPage: false default), per-step opt-in
**D2:** Persistence tests verify instance-policy contract (state persists post-submit)
**D3:** home-entry is a first-class walkthrough (signed-out → dashboard → hub path)
**D4:** skipHeading for variable-heading pages (home, dashboard) with explicit assertions
**Coverage gaps closed:** Back/edit flows, validation tests, success assertions

Files modified: walkthrough.ts, community-enquiry spec, information-request spec, payment-demo spec
Files created: home-entry.walkthrough.spec.ts, home-entry.md walkthrough doc

Decision recorded: "Walkthrough Coverage Hardening — Test Gaps and Screenshot Behaviour"
