# Session Log: Keycloak CI Readiness & Proxy Chain — Investigation Consolidation

**Date:** 2026-04-16  
**Time:** 08:11:04 UTC  
**Context:** Joint investigation summary and decision merge  
**Orchestration Log:** `.squad/orchestration-log/2026-04-16T08:11:04Z-keycloak-ci-resolution.md`

## Participants

1. **Tangy (QA & Test Automation)** — Validated Playwright readiness contract and CI contract correctness
2. **Blathers (Backend & CI Integration)** — Traced AppHost/proxy chain and implemented endpoint injection fix
3. **Scribe** — Session consolidation and decision documentation

## Investigation Summary

### Problem Statement

GitHub Actions CI `localhost-auth-playwright` job consistently timing out at Aspire readiness gate. Keycloak service marked ready in Aspire, but HTTPS proxy endpoint unresponsive. Two competing hypotheses:
1. Playwright readiness contract too strict (Tangy's initial concern)
2. AppHost/proxy endpoint binding unstable in CI (Blathers' investigation)

### Hypothesis Validation

**Tangy's Review:**
- Analyzed CI run `24427460363`: after container health check fix, Playwright probe still timed out
- Confirmed: `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration` (browser-facing) is the correct readiness boundary
- Container HTTP health is not equivalent; must test the actual proxy chain users see
- **Verdict:** Contract is correct; do not weaken to `/health/ready` or `http://localhost:8080`

**Blathers' Trace:**
- Discovered `src/UmbracoPrism.KeycloakProxy/appsettings.json` hardcoded `http://localhost:8080` upstream target
- In CI container environments, port allocation is dynamic; hardcoded port assumption breaks
- AppHost does not guarantee loopback port stability; must query runtime endpoint
- **Fix:** Inject `keycloak.GetEndpoint("http")` from AppHost into proxy configuration

### Resolution

**Changes:**
- `src/UmbracoPrism.AppHost/Program.cs` — Now injects `ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address` at startup
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — Added regression assertion

**Design Preservation:**
- Localhost auth flow remains HTTPS-first (`https://localhost:8443`)
- Browser and tests see proxy contract unchanged
- AppHost now owns the runtime endpoint discovery; proxy just uses injected value

## Decisions Recorded

### Decision 1: Tangy — Playwright Readiness Contract (Strict HTTPS Proxy Boundary)

**Decision:** Keep the browser-facing Keycloak contract on `https://localhost:8443` and do not weaken readiness probes to raw HTTP or generic liveness endpoints.

**Why:**
- Localhost auth flow requires HTTPS proxy visibility for issuer validation
- Generic container health checks can pass while proxy chain is broken
- CI evidence: run `24427460363` proves this boundary

**Implications:**
- Playwright readiness contract is correct as-is
- Never accept `/health/ready` as equivalent to discovery endpoint probe
- Contract is non-negotiable for CI stability

### Decision 2: Blathers — AppHost Endpoint Injection (Dynamic Proxy Binding)

**Decision:** Have `src/UmbracoPrism.AppHost/Program.cs` inject `ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address` from `keycloak.GetEndpoint("http")` into the keycloak-proxy project.

**Why:**
- Preserves local proxy design (browser/tests still talk to `https://localhost:8443`)
- Lets Aspire decide the actual Keycloak HTTP endpoint
- Removes hardcoded port assumptions that break in CI container environments
- Proxy startup no longer depends on specific loopback port

**Implications:**
- keycloak-proxy no longer owns upstream endpoint knowledge
- AppHost owns runtime discovery; proxy is stateless configuration consumer
- HTTPS proxy contract preserved; downstream routing unaffected

## Validation Results

✅ **All checks passed:**
- `dotnet build UmbracoPrism.sln` — OK
- `dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests` — OK
- `npm run test:playwright:localhost-auth` — OK

✅ **No regressions detected**

## Key Outcomes

1. ✅ **Root cause identified:** Dynamic port binding, not contract weakness
2. ✅ **Two-part resolution:** Contract validation + backend fix
3. ✅ **Design preserved:** HTTPS proxy boundary remains user-facing contract
4. ✅ **CI stability improved:** Endpoint injection removes port assumption fragility
5. ✅ **Decisions merged:** Both findings recorded in canonical log

## Next Steps

- Monitor CI runs for sustained Keycloak readiness
- Keycloak investigation closed; ready for downstream task routing
- No further changes needed to Playwright contracts

**Status:** Investigation complete. Team ready for next phase.
