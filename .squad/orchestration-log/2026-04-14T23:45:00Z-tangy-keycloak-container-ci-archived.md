# Tangy — Keycloak Container CI Failure Investigation

**Session:** 2026-04-14T22:30:00Z  
**Run:** GitHub Actions `24425752344`  
**Commit:** `0497571` (health check removal fix)  
**Status:** ❌ Failed — New root cause identified

## Classification

**First Meaningful Failure:** Keycloak Docker container not starting in CI environment

The health check circular dependency (commit `6b203ec`) was correctly removed in commit `0497571`, but the test still fails because the underlying Keycloak container never starts.

## Evidence

### What Works
- ✅ Linux certificate trust setup passes
- ✅ Aspire prerequisites validation passes
- ✅ Aspire AppHost starts
- ✅ Aspire DCP recognizes both Docker and Podman runtimes
- ✅ keycloak-proxy .NET process starts (PID 4931)
- ✅ TestSite starts and serves correctly
- ✅ MockBusinessApp starts and serves correctly
- ✅ Aspire marks `/keycloak` service as "Ready" (reconciliation 23)

### What Fails
- ❌ **No Docker image pull for `quay.io/keycloak/keycloak:26.0.0`** (no pull logs in CI output)
- ❌ **No container startup logs** despite container ID `a20ce8c876b44da2cc31d908e2701a5cca560946df82dbaea71b193612d0512b` being created
- ❌ **Connection refused on `127.0.0.1:32768`** when keycloak-proxy tries to connect
- ❌ **Playwright readiness check fails:** `Keycloak: no response — no HTTP response; body missing "\"issuer\":\"https://localhost:8443/realms/prism-dev\""`

### Key Log Signals

```
[stdout] Added new ContainerNetworkConnection {"Container": {"name":"keycloak-wgzbgqcw"}, ...}
[stdout] service /keycloak is now in state Ready {"ServiceName": {"name":"keycloak"}, "Reconciliation": 23}
[stdout] service /keycloak-proxy is now in state Ready {"ServiceName": {"name":"keycloak-proxy"}, "Reconciliation": 25}
[stdout] fail: Aspire.Hosting.Dcp.dcpctrl.ServiceReconciler.Proxy[0]
[stdout] Error handling TCP connection {"Service": {"name":"keycloak"}, "error": "Could not establish TCP connection to endpoint: dial tcp 127.0.0.1:32768: connect: connection refused"}
```

## Root Cause

**Keycloak Docker container is not actually starting in the GitHub Actions ubuntu-24.04 runner environment**, even though Aspire's container orchestrator (DCP) thinks it's ready.

Aspire's `.WaitFor(keycloak)` dependency only waits for the **container resource state** to be Ready, not for the container's **HTTP endpoints to be available**.

## Possible Causes

1. **Silent image pull failure** — Docker/Podman can't pull `quay.io/keycloak/keycloak:26.0.0` but doesn't surface the error
2. **Container startup crash** — Container starts but immediately exits before binding to port 8080
3. **GitHub Actions Docker socket permissions** — Runner environment restricts container creation
4. **Aspire DCP incompatibility with ubuntu-24.04** — Newer runner image may have changed Docker socket or runtime behavior
5. **Resource constraints** — GitHub Actions runner doesn't allocate enough memory/CPU for Keycloak container
6. **Podman vs Docker runtime mismatch** — Aspire detects both but uses the wrong one or has compatibility issues

## Smallest Next Fix

**Add diagnostic logging and container health verification:**

1. **Option A: Add HTTP health check to Keycloak container** (not the proxy)
   - Use `.WithHttpHealthCheck("/health/ready")` on the Keycloak container resource
   - This will force Aspire to wait for Keycloak's HTTP endpoint to respond before marking it Ready
   
2. **Option B: Add explicit container startup logging**
   - Add verbose logging to see actual Docker/Podman commands Aspire is executing
   - Check if container logs show any startup errors
   
3. **Option C: Pre-pull the Keycloak image in CI**
   - Add workflow step to `docker pull quay.io/keycloak/keycloak:26.0.0` before running tests
   - This isolates whether the issue is image pull vs container startup

## Recommendation

**Try Option C first (pre-pull)** to isolate the failure mode. If pre-pull succeeds but container still doesn't start, the issue is container runtime compatibility. If pre-pull fails, the issue is network/image access.

**Then implement Option A** to add real HTTP health checks to the Keycloak container (targeting its own HTTP port, not the proxy).

## References

- CI run: `https://github.com/jonnymuir/Umbraco.Prism/actions/runs/24425752344`
- Job: `localhost-auth-playwright`
- Commit: `0497571e71c5ebb053386682deb33073615a4694`
- Files:
  - `src/UmbracoPrism.AppHost/Program.cs` (Keycloak container definition)
  - `src/UmbracoPrism.Client/tests/support/live-app-host.ts` (readiness checks)
  - `.github/workflows/ci-tests.yml` (CI configuration)
