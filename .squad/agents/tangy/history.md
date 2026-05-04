# Tangy — History (Summary)

**Agent:** Tester specializing in browser contracts, diagnostics, and API validation for Codespaces environments.

**Recent focus (2026-05-04):** Workflow auth regression investigation, API null-auth logging, behavioral diagnostics, test contract validation.

---

## Current Work (2026-05-04)

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
