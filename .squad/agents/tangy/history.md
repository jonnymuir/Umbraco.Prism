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

## Decision Archive

See `.squad/agents/tangy/history-archive.md` for detailed session logs from 2026-05-03 including:
- Downstream timeout diagnosis and operator flow reduction
- Transport diagnostics validation (5 behavioral contract tests, 680 tests passing)
- Business API arrival instrumentation trace ID forwarding
- Environment variable configuration diagnostics patterns
