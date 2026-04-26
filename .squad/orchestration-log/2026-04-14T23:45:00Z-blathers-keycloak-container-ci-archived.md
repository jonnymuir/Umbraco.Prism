# Blathers — Keycloak Container HTTP Health Check for CI Startup Sequencing

**Date:** 2026-04-14  
**Agent:** Blathers (Backend Dev)  
**Context:** localhost-auth-playwright CI failure investigation (run 24425752344, commit 0497571)

## Decision

Restore `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` to the Keycloak container in `src/UmbracoPrism.AppHost/Program.cs`, but do NOT restore the circular keycloakProxy custom health check that was removed in commit 0497571.

## Root Cause

Commit 0497571 removed ALL health checks to fix a circular dependency, but the removal was too broad:

1. **Correctly removed:** keycloakProxy custom health check (`.WithHealthCheck(KeycloakProxyHealthCheckName)`) that pointed to the proxy's own HTTPS endpoint, creating a deadlock.

2. **Incorrectly removed:** Keycloak container HTTP health check (`.WithHttpHealthCheck(...)`) that pointed to the container's own HTTP endpoint at port 8080.

The Keycloak container health check is non-circular and necessary because:
- Aspire's default container readiness only verifies the container process is running
- Keycloak takes additional time after container start to import the realm and start accepting HTTP connections
- Without the HTTP health check, Aspire marks Keycloak ready while HTTP endpoints are still refusing connections

## Evidence

CI run 24425752344 showed:
```
Error handling TCP connection {"Service":{"name":"keycloak"}, "error": "Could not establish TCP connection to endpoint: dial tcp 127.0.0.1:32768: connect: connection refused"}
```

Playwright tests timed out after 240 seconds waiting for `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration` to respond, even though Aspire had marked Keycloak ready.

## Solution

**Surgical fix:** Add back only the Keycloak container HTTP health check.

```csharp
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")  // ← Restored
    .WithEnvironment(...)
    .WithBindMount(...)
    .WithArgs("start-dev", "--import-realm", "--proxy-headers", "xforwarded");

var keycloakProxy = builder.AddProject(...)
    .WaitFor(keycloak);  // ← No .WithHealthCheck() - avoids circular dependency
```

## Why This Is Correct

1. **Non-circular:** The health check targets the Keycloak container's own HTTP endpoint (port 8080), not a dependent service.
2. **Necessary:** Gates Aspire readiness on actual HTTP availability, not just container process state.
3. **Safe:** Preserves the circular dependency fix from 0497571 by leaving keycloakProxy without custom health checks.

## Pattern for Future

**Container health checks should target the container's own HTTP endpoints, not dependent proxy services.**

- ✅ Container `.WithHttpHealthCheck("/path")` → container's own HTTP port
- ❌ Resource `.WithHealthCheck(customCheckName)` → that resource's own HTTPS proxy

The second pattern creates a deadlock: the resource can't become ready until the health check passes, but the health check probes the resource's own proxy, which can't serve requests until the resource is ready.

## Implementation

Commit: `933f97f`  
Files changed: `src/UmbracoPrism.AppHost/Program.cs`

## Next Steps

1. Push commit to trigger CI
2. Monitor CI run 24425752344's successor to confirm Keycloak startup sequencing
3. If tests pass, archive this decision to main `.squad/decisions.md`
