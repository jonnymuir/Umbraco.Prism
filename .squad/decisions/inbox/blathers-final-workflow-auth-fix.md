---
date: 2026-05-04T00:26:42.240+01:00
author: Blathers
status: PROPOSED
area: workflow, auth, MockBusinessApp
commit: beef21c
---

# Workflow Auth: Align MockBusinessApp Handlers and Log Silent Auth Failures

## Context

Two layered 401 failure modes in the Codespaces workflow-start path were collapsing into the same surface error, making diagnosis difficult:

1. `BusinessAppWorkflowClient.CreateClientAsync` silently omitted the `Authorization` header when `GetAuthorizationHeaderAsync` returned null (e.g. `CurrentTenant` unresolved), with no log entry.
2. MockBusinessApp workflow handlers (`/current`, `/advance`, `/instances`) returned `Results.Unauthorized()` for app-level tenant/email resolution failures, while `/api/backoffice/me` returned `Results.Problem()` for the same conditions.

## Decisions

### 1. Log a Warning when auth header is null

**`BusinessAppWorkflowClient.CreateClientAsync` must log a Warning when `GetAuthorizationHeaderAsync` returns null.**

When no auth header is obtained, the request will be rejected by the Business App JWT middleware with 401, which then triggers a spurious token-refresh retry cycle. Without a log, this is entirely invisible. The warning includes the `forceRefresh` flag and a hint to check `PrismTenantMiddleware`.

### 2. MockBusinessApp workflow handlers must return Results.Problem for app-level failures

**All three workflow endpoints must return `Results.Problem(...)` — not `Results.Unauthorized()` — when tenant or email resolution fails after successful JWT validation.**

This aligns them with `/api/backoffice/me` (already using `Results.Problem`). The result:
- A 401 from the workflow path now **unambiguously** means the bearer token was missing or rejected by JWT middleware.
- A 500 from the workflow path means the token was valid but Business App configuration (tenant mapping, email claims) failed.
- Operators and TestSite logs can distinguish the two cases without guesswork.

## Impact

- Tangy's regression tests (`BusinessAppWorkflowClientTests`) continue to pass and correctly model the expected retry behaviour on JWT-level 401.
- No changes to the retry logic itself — the fix is diagnostic clarity only.
