# Orchestration Log: Keycloak CI Resolution — Investigation Complete (2026-04-16 08:11:04)

**Status:** ✅ Completed  
**Agents:** Tangy (QA Lead), Blathers (Backend/CI)  
**Task:** Root cause analysis and fix validation for Keycloak proxy CI failure

## Summary

Tangy and Blathers jointly investigated the Keycloak HTTPS proxy failure in GitHub Actions CI. Tangy validated the correctness of the Playwright readiness contract (real HTTPS proxy path), while Blathers traced the backend/AppHost chain and identified the actual failure point: the keycloak-proxy's hardcoded localhost:8080 upstream dependency was unstable across CI runtimes.

**Resolution:** Blathers modified `src/UmbracoPrism.AppHost/Program.cs` to inject `ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address` from Aspire's real `keycloak.GetEndpoint("http")`, preserving the local HTTPS proxy design while binding the proxy to dynamic container endpoints.

## Investigation Phases

### Phase 1: Playwright Contract Review (Tangy)

**Verdict:** Current readiness contract is correct and must not be weakened.

- ✅ Keep probing `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration`
- ✅ Keep asserting issuer `https://localhost:8443/realms/prism-dev`
- ❌ Do NOT accept `/health/ready` or `http://localhost:8080` as proxy health equivalent
- **Rationale:** Localhost auth flow requires HTTPS proxy visibility; generic container health checks miss proxy chain breaks

**Evidence:** CI run `24427460363` passed container health but failed browser-facing proxy, proving the contract boundary.

### Phase 2: Backend Proxy Chain Analysis (Blathers)

**Finding:** AppHost/keycloak-proxy integration relied on hardcoded `http://localhost:8080` upstream target, which is unstable in CI container environments.

**Root Cause Chain:**
1. `src/UmbracoPrism.KeycloakProxy/appsettings.json` hardcoded `ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address` to `http://localhost:8080`
2. Aspire's Keycloak container does not guarantee HTTP endpoint availability on that loopback port
3. When port allocation differs (CI vs. local), proxy startup succeeded but requests to `https://localhost:8443` hung

**Fix:** Inject endpoint from AppHost runtime:
- `src/UmbracoPrism.AppHost/Program.cs` now reads `keycloak.GetEndpoint("http")`
- Passes resolved address to keycloak-proxy via `ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address`
- Proxy now binds to Aspire's actual container endpoint
- HTTPS proxy design preserved; browser and tests still see `https://localhost:8443`

## Validation Results

✅ **Build:** `dotnet build UmbracoPrism.sln` — passed  
✅ **Unit Tests:** `dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests` — passed  
✅ **Regression Tests:** Added assertion in `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — green  
✅ **E2E Tests:** `npm run test:playwright:localhost-auth` — passed

## Decisions Merged

1. **Playwright Readiness Contract:** Strict HTTPS proxy boundary required (Tangy)
2. **AppHost Keycloak Binding:** Inject dynamic endpoint from container service (Blathers)

See `.squad/decisions.md` for full merge.

## Next Steps

- Monitor CI runs for sustained stability
- Keycloak CI failure classified as resolved
- Team ready for downstream routing/workflow assignments
