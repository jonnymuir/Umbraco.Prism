# 2026-04-14T21:34:14Z — Scribe — CI Failure Investigation Session

## Session Context
- **Topic:** Latest CI failure still failing after previous readiness work (Ubuntu cert-trust + workflow_dispatch fix)
- **Run ID:** `24420087047`
- **Commit:** `6827ff36c1c5e5d2950a66997177af5928d05b1c`
- **Agents Investigating:**
  - **Tangy:** Classification and evidence analysis
  - **Blathers:** Tracing failure and determining smallest next fix
- **User:** Jonnymuir

## Session Summary
Both investigative teams converged on the same root cause: **Aspire AppHost readiness contract failure centered on Keycloak**.

### Key Findings
1. **Workflow setup is now healthy:**
   - Linux certificate trust step: ✅
   - .NET dev cert trust step: ✅
   - Dependencies, browsers, ASP.NET prerequisites: ✅

2. **Failure Point:** `LiveAppHost.waitForReadiness()` timeout in `localhost-auth-playwright` lane
   - All probes pass except Keycloak
   - Keycloak discovery endpoint reports no response
   - AppHost logs show TCP connection refused on port 32768

3. **Root Cause:** Keycloak service marked "ready" by Aspire but upstream HTTP endpoint still not responding

## Proposed Action
1. Harden AppHost dependency chain: add real HTTP health/discovery gate instead of container-only readiness
2. Ensure Keycloak consumers wait for actual HTTP availability
3. Rerun `localhost-auth-playwright` lane
4. Only then evaluate if timeout increase is needed

## Decision Inbox Merged
- `blathers-latest-ci-failure.md`
- `tangy-latest-ci-failure.md`

Both consolidated into `decisions.md` with consensus classification and next action alignment.
