# Tangy — Latest Keycloak CI Failure Followup (2026-04-14)

**GitHub Actions run:** `24426777068`  
**Failing job:** `localhost-auth-playwright` (job ID: 71362398259)  
**First meaningful failure:** `waitForReadiness()` timeout after ~240 seconds  
**Commit:** `da375b0` (latest main)

## Root Cause Analysis

The latest CI failure **reproduced the same failure mode** as previous runs: Keycloak marked "Ready" by Aspire, but HTTP connections refused.

### Evidence from Logs

```
[stdout] service /keycloak is now in state Ready	{"ServiceName": {"name":"keycloak"}, "Reconciliation": 23}
[stdout] Error handling TCP connection	{"Service": {"name":"keycloak"}, "error": "Could not establish TCP connection to endpoint: dial tcp 127.0.0.1:32768: connect: connection refused"}
```

Readiness probe failures:
- **TestSite home marker:** no response
- **TestSite seed contract:** no response  
- **Workflow hub seed:** no response
- **Keycloak:** no response — body missing `"issuer":"https://localhost:8443/realms/prism-dev"`

### Root Cause: Wrong Health Check Endpoint

The issue is in `src/UmbracoPrism.AppHost/Program.cs` line 30:

```csharp
.WithHttpHealthCheck("/health/ready")
```

**Problem:** Keycloak's `/health/ready` endpoint does NOT validate realm import completion. It only checks that the Keycloak server process is running. This means:

1. Container starts
2. Keycloak process starts  
3. `/health/ready` returns 200 OK
4. Aspire marks service as Ready
5. **But** realm import (`--import-realm`) is still in progress
6. Downstream services try to connect to `/realms/prism-dev/.well-known/openid-configuration`
7. **Connection refused** because realm not yet available

### Historical Context

- **Commit `6b203ec`** (regression): Used `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` ✅ CORRECT for container, but also added circular proxy health check ❌
- **Commit `0497571`** (over-correction): Removed ALL health checks
- **Commit `eb19498`** (incomplete fix): Restored health check but used `/health/ready` instead of realm endpoint ❌
- **Current state:** `/health/ready` does not validate realm availability

### Failure Mode Comparison

**Previous failure (before any health check):** Same symptom — connection refused  
**Current failure (with `/health/ready`):** Same symptom — connection refused

**Conclusion:** The failure mode has **NOT changed**. `/health/ready` is not sufficient; we need the realm-specific endpoint.

## Smallest Next Fix

**Change line 30 in `src/UmbracoPrism.AppHost/Program.cs` from:**
```csharp
.WithHttpHealthCheck("/health/ready")
```

**To:**
```csharp
.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")
```

**Also update the comment (lines 26-27) to accurately reflect what the health check validates:**
```csharp
// HTTP health check probes the realm's OIDC discovery endpoint to ensure both
// Keycloak startup and realm import have completed before dependent services start.
```

### Why This Is Correct

1. **Non-circular:** The health check targets the container's own HTTP port (8080), not the proxy (8443)
2. **Necessary:** Gates readiness on actual realm availability, not just Keycloak process state
3. **Proven:** This was the original working approach in `6b203ec` before the circular proxy check was added
4. **Aligned with tests:** Playwright probes the same endpoint via the proxy: `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration`

### Pattern Validation

✅ **Container resource:** `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` → container's own HTTP port (safe, validates realm)  
❌ **Proxy resource:** `.WithHealthCheck(customCheckName)` → proxy's own HTTPS endpoint (circular, removed in 0497571) 

## Risk Assessment

- **Risk:** Low — this restores the proven working health check from `6b203ec` without the circular proxy dependency
- **Validation:** Local testing should pass 8/8 Playwright tests; CI should pass after this change
- **Rollback:** If this fails, can temporarily increase timeout, but root cause is the wrong health endpoint

## Recommendation

**Priority:** HIGH — blocks all CI runs  
**Complexity:** TRIVIAL — one-line fix + comment update  
**Confidence:** HIGH — root cause clear, fix proven in git history  

**Assigned to:** Blathers (backend orchestration domain)
