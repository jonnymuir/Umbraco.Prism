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

## Decision Archive

See `.squad/agents/tangy/history-archive.md` for detailed session logs from 2026-05-03 including:
- Downstream timeout diagnosis and operator flow reduction
- Transport diagnostics validation (5 behavioral contract tests, 680 tests passing)
- Business API arrival instrumentation trace ID forwarding
- Environment variable configuration diagnostics patterns
