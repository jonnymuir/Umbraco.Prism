---
name: "aspire-container-health-checks"
description: "Use precise HTTP health checks for containers with import/initialization workflows"
domain: "aspire-orchestration"
confidence: "high"
source: "earned"
tools:
  - name: "view"
    description: "Review Aspire AppHost Program.cs and container resource definitions"
    when: "Use when diagnosing container readiness issues or validating health check endpoints"
  - name: "bash"
    description: "Test container health endpoints and reproduce startup failures"
    when: "Use when verifying health check endpoints actually validate the intended readiness state"
---

## Context

Use this pattern when Aspire AppHost orchestrates containers that perform initialization work (database migrations, realm imports, plugin loading) after the container process starts. Containers may report "running" while their HTTP services are still initializing.

## Patterns

### Health checks must validate initialization, not just process state

- Container state "running" does not mean HTTP endpoints are accepting connections.
- Generic health endpoints like `/health/ready` or `/health/live` may pass before initialization completes.
- Use **application-specific endpoints** that validate the actual resource your services need.

### Keycloak with realm import

**Problem:** Keycloak's `/health/ready` endpoint returns 200 OK as soon as the server process starts, but **before** `--import-realm` completes.

**Solution:** Use the realm's OIDC discovery endpoint for the health check:
```csharp
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")  // ✅ Validates realm import
    .WithArgs("start-dev", "--import-realm", "--proxy-headers", "xforwarded");
```

**Why this works:**
- The discovery endpoint only responds after the realm is imported and available
- Aspire waits for HTTP 200 from this endpoint before marking the service Ready
- Downstream services that need the realm can safely `.WaitFor(keycloak)`

### Distinguish container checks from proxy checks

**Safe pattern (non-circular):**
```csharp
// Container resource: health check targets container's own HTTP port
var keycloak = builder.AddContainer("keycloak", ...)
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration");  // ✅ Non-circular

// Proxy resource: no custom health check, just wait for container
var keycloakProxy = builder.AddProject("keycloak-proxy", ...)
    .WaitFor(keycloak);  // ✅ Safe dependency
```

**Anti-pattern (circular dependency):**
```csharp
// Proxy resource: custom health check probes proxy's own HTTPS endpoint
var keycloakProxy = builder.AddProject("keycloak-proxy", ...)
    .WithHealthCheck(customHealthCheckName)  // ❌ Probes https://localhost:8443/...
    .WaitFor(keycloak);
```

The proxy cannot become ready until its health check passes, but the health check cannot succeed until the proxy is serving requests — deadlock.

### CI symptom: connection refused despite container ready

**Log pattern:**
```
[stdout] service /keycloak is now in state Ready
[stdout] Error handling TCP connection ... dial tcp 127.0.0.1:32768: connect: connection refused
```

This indicates the health check passed too early. The container is "ready" but the HTTP endpoint is not yet accepting connections.

**Fix:** Replace the generic health endpoint with an application-specific endpoint that validates the actual initialization state.

### Validate against test probe endpoints

If Playwright or test harnesses probe specific endpoints for readiness, **use the same endpoint** (or an equivalent on the container's port) for the Aspire health check.

Example: If tests probe `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration` via the proxy, the container health check should probe `http://localhost:8080/realms/prism-dev/.well-known/openid-configuration` directly.

### Do not weaken behavioural probes to match container internals

- **Container/AppHost check:** may probe the container's own HTTP endpoint to gate orchestration startup.
- **Behavioural test check:** should stay on the user-facing/proxy-facing endpoint if that is what browsers and middleware actually use.

For this repo, the live auth contract is `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration` with issuer `https://localhost:8443/realms/prism-dev`. Replacing the Playwright probe with `http://localhost:8080/...` or `/health/ready` would hide proxy, issuer, and forwarded-header failures behind a misleading "ready" signal.

### Proxy upstreams must use AppHost endpoint injection, not hardcoded localhost ports

- If an Aspire-hosted proxy forwards to a container, do **not** assume the upstream is always `http://localhost:<fixed-port>`.
- Keep the user-facing proxy port fixed if needed (for example `https://localhost:8443`), but inject the upstream destination from the resource endpoint that Aspire actually allocates.

```csharp
var keycloakProxy = builder.AddProject("keycloak-proxy", "../UmbracoPrism.KeycloakProxy/UmbracoPrism.KeycloakProxy.csproj", launchProfileName: "https")
    .WithEnvironment(
        "ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address",
        keycloak.GetEndpoint("http"))
    .WaitFor(keycloak);
```

**Why this matters:**
- Local Docker runs may coincidentally expose `localhost:8080`, hiding the bug.
- CI/container-runtime combinations can allocate a different host endpoint even though the Keycloak resource itself is healthy.
- Injecting the AppHost-resolved endpoint keeps the proxy aligned with Aspire's actual runtime wiring without changing the browser contract.

## Examples

### Keycloak container health check evolution

- **Original working (6b203ec):** `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` ✅
- **Regression (6b203ec):** Added circular proxy health check → deadlock ❌
- **Over-correction (0497571):** Removed ALL health checks → connection refused ❌
- **Incomplete fix (eb19498):** Restored with `/health/ready` → connection refused ❌
- **Correct fix:** Restore realm discovery endpoint without proxy check ✅

### Health check validation process

1. Identify the container's initialization workflow (e.g., `--import-realm`)
2. Find an HTTP endpoint that only responds after initialization completes
3. Use `.WithHttpHealthCheck("/path")` with that endpoint
4. Verify in CI logs that the container does not show "connection refused" after marked Ready
5. Ensure downstream service health checks do not create circular dependencies

## Anti-Patterns

- **Using generic health endpoints** — `/health/ready` may pass before initialization completes
- **Trusting container state alone** — `.WaitFor(container)` without HTTP health check only validates process state
- **Proxy health checks** — Custom health checks on proxy resources that probe their own endpoints create circular dependencies
- **Mismatched health vs test endpoints** — If tests expect realm discovery, health check should validate realm availability, not just Keycloak process state
- **Copying health endpoints without validation** — Verify the endpoint actually waits for the initialization step you care about
- **Hardcoded proxy upstream localhost ports** — They can pass locally but break in CI when Aspire allocates a different runtime endpoint

## Related Skills

- `keycloak-localhost-https`: HTTPS proxy setup for Safari/WebKit compatibility
- `aspire-prereq-validation`: Validating Aspire prerequisites before launch
- `ci-loopback-oidc-cert-trust`: Certificate trust setup for CI environments
