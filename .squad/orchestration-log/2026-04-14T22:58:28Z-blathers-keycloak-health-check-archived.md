# Blathers — Keycloak Container HTTP Health Check for CI Startup Sequencing

**Date:** 2026-04-14  
**Agent:** Blathers (Backend Dev)  
**Context:** localhost-auth-playwright CI failure investigation (runs 24425752344, 24426243314, commit 0497571)

## Decision

Add `.WithHttpHealthCheck("/health/ready")` to the Keycloak container in `src/UmbracoPrism.AppHost/Program.cs`, using Keycloak's built-in health endpoint instead of the discovery endpoint. Do NOT restore the circular keycloakProxy custom health check.

## Root Cause

Commit 0497571 removed ALL health checks to fix a circular dependency, but the removal was too broad:

1. **Correctly removed:** keycloakProxy custom health check (`.WithHealthCheck(KeycloakProxyHealthCheckName)`) that pointed to the proxy's own HTTPS endpoint, creating a deadlock.

2. **Incorrectly removed:** Keycloak container HTTP health check (`.WithHttpHealthCheck(...)`) that gates readiness on the container's own HTTP service.

Without a health check, Aspire marks Keycloak ready based solely on container process state, not HTTP service availability. This caused:
- CI run 24425752344: "connection refused" when Playwright tried to reach Keycloak
- CI run 24426243314: Timeout with all services except Aspire dashboard and MockBusinessApp failing to respond

## Investigation Steps

1. First attempt used `/realms/prism-dev/.well-known/openid-configuration` as health endpoint
   - Too strict: requires full realm configuration and OIDC metadata generation
   - Still timed out in CI run 24426243314

2. Second attempt uses `/health/ready` (Keycloak's built-in health endpoint)
   - Lighter-weight check that still validates realm import via KC_HEALTH_ENABLED
   - Aligned with Keycloak's standard health check contract

## Solution

```csharp
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/health/ready")  // ← Using built-in health endpoint
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")  // ← Enables /health endpoints
    // ...
```

## Why /health/ready Instead of Discovery Endpoint

- `/health/ready`: Keycloak's standard readiness probe, validates realm import is complete
- `/realms/prism-dev/.well-known/openid-configuration`: Requires full OIDC metadata generation, may be too heavy for startup health check
- Both should eventually succeed, but `/health/ready` is the canonical readiness signal

## Pattern for Future

**Container health checks should use the container's built-in health endpoints when available.**

- ✅ Container `.WithHttpHealthCheck("/health/ready")` → standard health endpoint
- ✅ Container `.WithHttpHealthCheck("/health")` → basic liveness
- ⚠️ Container `.WithHttpHealthCheck("/app/specific/endpoint")` → only if no standard health endpoint exists
- ❌ Resource `.WithHealthCheck(customCheckName)` → that resource's own HTTPS proxy (circular dependency)

## Implementation

Commits: `933f97f` (discovery endpoint), `eb19498` (health endpoint)  
Files changed: `src/UmbracoPrism.AppHost/Program.cs`

## Next Steps

1. Push commit eb19498 to trigger CI
2. Monitor for successful Keycloak startup and test passage
3. If tests pass, archive this decision to main `.squad/decisions.md`
4. If tests still timeout, investigate Keycloak container startup time in GitHub Actions environment
