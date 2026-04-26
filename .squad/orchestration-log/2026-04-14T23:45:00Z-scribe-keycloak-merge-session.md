# 2026-04-14T23:45:00Z — Scribe: Keycloak CI Investigation Log Merge

## Session Intent

Merge Tangy and Blathers CI investigation findings into canonical decisions record after post-0497571 failure diagnosis.

## Spawn Context

**Assignment:** Scribe to consolidate Keycloak container CI failure diagnosis from two investigations:

1. **Tangy** (2026-04-14T22:29:46Z): QA diagnostic investigation into CI run 24425752344 failure
   - **Finding:** Keycloak Docker container not starting in GitHub Actions ubuntu-24.04 environment
   - **Root Cause:** Aspire's `.WaitFor(keycloak)` only waits for container resource state, not HTTP endpoint availability
   - **Recommendation:** Add HTTP health check to Keycloak container to gate on actual HTTP readiness
   - **Decision Inbox File:** `.squad/decisions/inbox/tangy-keycloak-container-ci.md`

2. **Blathers** (2026-04-14T22:29:46Z): Backend orchestration fix path analysis
   - **Finding:** Commit 0497571 removed Keycloak health check too broadly
   - **Root Cause:** Circular dependency was in keycloakProxy custom check, not container check
   - **Solution:** Restore `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` to Keycloak container only
   - **Implementation:** Commit `933f97f` (file: `src/UmbracoPrism.AppHost/Program.cs`)
   - **Decision Inbox File:** `.squad/decisions/inbox/blathers-keycloak-container-ci.md`
   - **Status:** In progress; agent still running

## Findings Consolidated

### Root Cause Chain

| Layer | Finding | Owner |
|-------|---------|-------|
| **Regression (6b203ec)** | Added circular health check: keycloakProxy custom check probing proxy's own HTTPS endpoint | Previous session |
| **Fix (0497571)** | Removed ALL health checks, but over-corrected: removed Keycloak container check too | Previous session |
| **Current Failure (24425752344)** | Keycloak container never starts HTTP; Aspire marks ready before HTTP available | Tangy + Blathers |

### Smallest Next Action

**Restore non-circular Keycloak container HTTP health check:**

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

## Decision Merge Preparation

**Status:** Awaiting Blathers agent completion (still running, ~353s elapsed).

Once Blathers completes, decisions will be merged into canonical `.squad/decisions.md` entry:

- **Title:** 2026-04-14 (ONGOING): Tangy & Blathers — Post-Deadlock Fix CI Failure Investigation → **RESOLVED**
- **New Entry:** 2026-04-14: Tangy & Blathers — Keycloak Container HTTP Health Check Surgical Restore
- **Implementation:** Via commit `933f97f` (Blathers)
- **Validation:** Re-run `localhost-auth-playwright` CI lane

## Pending Items

- [ ] Blathers agent completes implementation analysis
- [ ] Merge inbox decisions into canonical record
- [ ] CI re-validation with restored health check
- [ ] If tests pass, close investigation; if fail, escalate to next diagnostic layer

## References

- **Investigation Start:** 2026-04-14T22:29:46Z
- **Commit causing regression:** `6b203ec` (health check circular dependency)
- **Regression fix attempt:** `0497571` (over-removal of health checks)
- **New fix commit:** `933f97f` (surgical restore of container check only)
- **GitHub Actions run:** `24425752344` (source of failure diagnosis)
- **Job:** `localhost-auth-playwright`
- **Workflow:** `.github/workflows/ci-tests.yml`
- **Implementation file:** `src/UmbracoPrism.AppHost/Program.cs`

---

**Session Status:** AWAITING AGENT COMPLETION → MERGE → VALIDATION
