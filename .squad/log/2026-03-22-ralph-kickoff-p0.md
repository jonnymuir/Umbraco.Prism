# Session Log: Ralph Kickoff Round – P0 Architecture Issues

**Date:** 2026-03-22
**Topic:** ralph-kickoff-p0
**Agents:** Blathers (issues #2, #3), Tom Nook (issue #4)
**Logged by:** Scribe

---

## What Happened

Ralph (async triage persona) completed the kickoff round for the three P0 architecture issues. Each issue received a decision document capturing first-PR scope, sequencing rationale, and guardrails. Decision notes were merged into the decisions ledger and inbox files cleared.

## Issues Covered

### #2 – Signing Key Cache / Async OIDC (squad:blathers)

**First PR scope:**
- Remove sync-blocking OIDC metadata calls from request-path key resolvers.
- Introduce tenant-scoped async-warmed signing key cache.

**Kickoff comment posted:** Yes (on GitHub issue #2).

### #3 – Token Refresh Resilience (squad:blathers)

**First PR scope:**
- Add retry with exponential backoff plus per-tenant circuit breaker to token refresh path.
- Cover resilience behavior with focused unit tests before broader refactor.

**Kickoff comment posted:** Yes (on GitHub issue #3).

### #4 – Standardize Authorization Model — Entra vs Umbraco Groups (squad:tom nook)

**Decision:** Adopt Entra token claims as single source of truth for Prism authorization. Compatibility mode first (Entra-first, optional Umbraco fallback), strict Entra after adoption.

**First PR scope:**
- Introduce authorization options for Entra admin claim (claim type + allowed values + compatibility toggle).
- Update `PrismAdminHandler` to evaluate Entra claims first.
- Add tests for `PrismTenantHandler` mismatch/missing scenarios.

**Follow-up:** Expected to split into 3 issues (core impl + tests; migration hardening; legacy cleanup).

**Kickoff comment posted:** Not explicitly posted as GitHub comment in this session (decision recorded in decisions ledger).

---

## Artifacts

- Decision entry appended to `.squad/decisions.md`
- Inbox files cleared:
  - `.squad/decisions/inbox/blathers-p0-kickoff.md` → merged
  - `.squad/decisions/inbox/tom-nook-auth-model-kickoff.md` → merged

---

## Open Items

- #4 kickoff comment on GitHub issue has not yet been posted; Tom Nook should post or Scribe can assist on next request.
- Follow-up split issues for #4 not yet created.
