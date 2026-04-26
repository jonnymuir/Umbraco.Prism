# Decision: Keycloak Health Check Endpoint Fix

**Date:** 2026-04-14  
**Author:** Blathers (Backend Dev)  
**Context:** CI run 24426777068 (origin/main commit da375b0) failing in localhost-auth-playwright job

## Root Cause

Keycloak container health check using wrong endpoint:

1. **Commit eb19498** added `.WithHttpHealthCheck("/health/ready")` to check Keycloak readiness
2. **Problem:** The `/health/ready` endpoint does NOT validate realm import completion—it only checks Keycloak process health
3. **Result:** Aspire marks container Ready before realm is available, causing downstream "connection refused" errors when tests try to access `/realms/prism-dev/.well-known/openid-configuration`

## Evidence

From CI run 24426777068 logs:
- `service /keycloak is now in state Ready` (Aspire marks ready prematurely)
- Immediately followed by: `Error handling TCP connection ... dial tcp 127.0.0.1:32768: connect: connection refused`
- Playwright timeout: Keycloak discovery endpoint unreachable, TestSite can't start
- TestSite seed contract: no response — body missing realm-dependent routes

## Investigation Path

Initial hypothesis: Port mismatch (health endpoints on port 9000 vs 8080 check). However, deeper analysis revealed:
- Keycloak's built-in `/health/*` endpoints are on management port 9000 when `KC_HEALTH_ENABLED=true`
- The realm's OIDC discovery endpoint `/realms/prism-dev/.well-known/openid-configuration` is **always** on the main HTTP port 8080
- The discovery endpoint is the authoritative signal for "realm imported and ready to serve OIDC requests"

## Decision: Check Realm Discovery Endpoint

Change health check from `/health/ready` to `/realms/prism-dev/.well-known/openid-configuration`.

**Change:**
```diff
-    .WithHttpHealthCheck("/health/ready")
+    .WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")
```

**Comment update:**
```diff
-// HTTP health check uses Keycloak's built-in /health/ready endpoint which includes
-// realm import validation, ensuring the container is fully initialized before dependent services start.
+// HTTP health check probes the realm's OIDC discovery endpoint to ensure both
+// Keycloak startup and realm import have completed before dependent services start.
```

## Why This Is Correct

1. **Non-circular:** Checks container's own HTTP port (8080), not the proxy (8443)
2. **Validates realm import:** The discovery endpoint only responds when the realm is fully loaded
3. **Proven approach:** This was the working configuration in commit 6b203ec before circular proxy check was added
4. **Aligned with tests:** Playwright probes the same endpoint (via proxy): `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration`
5. **Simpler:** No additional Keycloak flags needed; discovery endpoint always on port 8080

## Why This Is Safe

- Smallest necessary change: one-line endpoint path update + comment clarification
- Restores proven working configuration from 6b203ec without the circular dependency
- No changes to Aspire orchestration topology
- No additional environment variables or flags
- Preserves all security posture

## Pattern Validation

✅ **Container resource:** `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` → container's own HTTP port (safe, validates realm)  
❌ **Proxy resource:** `.WithHealthCheck(customCheckName)` → proxy's own HTTPS endpoint (circular, removed in 0497571)

## Historical Context

- **Commit 6b203ec** (regression): Used realm discovery endpoint for health ✅ but also added circular proxy check ❌
- **Commit 0497571** (over-correction): Removed ALL health checks
- **Commit eb19498** (incomplete fix): Restored health check but used `/health/ready` ❌
- **This fix:** Restores realm discovery endpoint check without proxy circular dependency ✅

## Implementation

File: `src/UmbracoPrism.AppHost/Program.cs`  
Line 30: Change from `/health/ready` to `/realms/prism-dev/.well-known/openid-configuration`

## Validation Plan

1. Push fix to branch
2. Re-run CI localhost-auth-playwright job
3. Verify Keycloak realm discovery succeeds before dependent services start
4. Confirm all 8 Playwright tests pass

## References

- CI run 24426777068: https://github.com/jonnymuir/Umbraco.Prism/actions/runs/24426777068
- Tangy's analysis: `.squad/decisions/inbox/tangy-latest-keycloak-followup.md`
- Previous commits: 0497571 (removal), eb19498 (partial restoration), 6b203ec (original working + regression)

