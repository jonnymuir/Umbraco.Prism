# Orchestration Log: CI Regression Fix Session

**Date:** 2026-04-14T21:37:00Z  
**Session Owner:** Scribe  
**Topic:** CI regression fix after latest failed run  
**User:** Jonny Muir  

## Session Context

- **Tangy Investigation:** Concluded latest CI failure is regression from custom proxy health check at `https://localhost:8443`
- **Blathers Assignment:** Remove custom health check, keep container-level readiness only
- **CI Failure Reference:** GitHub Actions run `24423772285` (localhost-auth-playwright job timeout)

## Decisions Consolidated

### Tangy's Decision: CI Failure Analysis
- **Classification:** Keycloak readiness regression, not SSL/certificate setup
- **Root Cause:** Commit `6b203ec` added custom health checks creating timing/circular dependency
- **Impact:** Keycloak container marked ready in Aspire before HTTP endpoints accept connections
- **Recommendation:** Remove custom health check registration; keep Aspire built-in container health check

### Blathers' Decision: Remove Circular Health Check Dependency
- **Problem:** Health check probes proxy's own endpoint; creates deadlock before keycloakProxy serves requests
- **Additional Issue:** `.WithHttpHealthCheck()` on keycloak container may fail on raw HTTP instead of proxied HTTPS
- **Fix:** Remove both custom health checks; rely on Playwright's comprehensive readiness probes
- **Safe:** Playwright tests already have 240s timeout with app-level endpoint checks

## Implementation Plan (Blathers)

1. Remove `builder.Services.AddHealthChecks()` block from Program.cs
2. Remove `.WithHttpHealthCheck()` from keycloak container
3. Remove `.WithHealthCheck(KeycloakProxyHealthCheckName)` from keycloakProxy
4. Keep `.WaitFor(keycloak)` dependency chain intact
5. Verify CI passes with Playwright readiness probes

## Session Actions

1. ✅ Read Scribe charter and responsibilities
2. ✅ Read current decisions.md and consolidated consensus
3. ✅ Identified two decision inbox files: tangy-ci-failure-followup.md, blathers-ci-failure-followup.md
4. ✅ Prepared to merge decisions into canonical decisions.md
5. ⏳ Merge phase: consolidate consensus into decisions ledger

## Notes

- Both Tangy and Blathers reached independent but compatible conclusions on the same failure
- This represents team consensus on the root cause and fix approach
- Blathers is assigned to implement the fix; Scribe logs and merges for continuity
