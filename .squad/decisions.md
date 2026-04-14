# Decisions

Umbraco.Prism team decisions. Append-only ledger.

## 📌 2026-04-14: Blathers — CI workflow manual auth rerun + Linux cert trust wiring

**Decision:** Keep `.github/workflows/ci-tests.yml` as the single CI workflow, add top-level `workflow_dispatch:` for manual runs, and fix Ubuntu localhost-auth certificate bootstrap by persisting `SSL_CERT_DIR` before `dotnet dev-certs https --trust`.

**Why:** The failure mode was workflow bootstrap on GitHub-hosted Ubuntu, not app/test logic, so the smallest correct change is to wire Linux trust in-place. Adding `workflow_dispatch` makes the existing `localhost-auth-playwright` lane rerunnable from GitHub UI/`gh` without duplicating jobs or changing the established job topology.

**Implications:**
- Manual CI runs now include the existing `localhost-auth-playwright` job.
- Later steps in that job inherit the same OpenSSL trust context via `$GITHUB_ENV`.
- Existing `pull_request` and `push` triggers and job names remain unchanged.

---

## 📌 2026-04-13: Brewster — Dashboard Route Contract

**Decision:** Keep the seeded Umbraco dashboard contract as a direct published route at `/dashboard`, but have browser tests reach it from the signed-in home page CTA while asserting that CTA resolves to `/dashboard`.

**Context:** localhost auth/session Playwright flow for the seeded TestSite dashboard.

**Why:**
- `/api/prism/downstream-demo/seed-contract-ready` already treats `/dashboard` as part of the machine-checked route contract.
- An unauthenticated request to `/dashboard` correctly challenges to `/auth/login?ReturnUrl=%2Fdashboard`, so the app-side route wiring is sound.
- Driving the browser through the `Go to Dashboard` link exercises the same authored Umbraco navigation the user sees and avoids false negatives where the test is still on the home page when it expects dashboard-only UI.

**Implications:**
- Do not weaken the seed contract to allow a home-page fallback for dashboard scenarios.
- Localhost Playwright flows should verify the CTA `href` and then click it before asserting dashboard UI.

**Session Log:** `.squad/log/2026-04-13T23:05:08Z-dashboard-test-investigation.md`

### Tangy — Dashboard navigation trace

**Decision:** Live dashboard Playwright coverage should assert dashboard-only UI after navigation, not shared welcome copy.

**Why:** In the localhost auth/session repro on 2026-04-13, a signed-in member remained on `/` after both direct `page.goto('/dashboard')` and clicking the authored `Go to Dashboard` CTA. The home page still showed `Welcome back, Demo User`, so that heading could not distinguish a successful dashboard navigation from a failed one.

**Contract impact:**
- Keep the desired user contract: signed-in members should reach `/dashboard` and see dashboard-only actions.
- In Playwright helpers, treat `View Workflows` and `Call Mock Business App API` as the readiness signals for the dashboard.
- If those elements never appear, report an app routing break rather than letting the test hang on a later click.

**Evidence:**
- `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml`
- `src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml`

---

## 📌 2026-04-14: Tangy & Blathers — Latest CI Failure Root Cause Classification

**Status:** Investigation complete; consolidated consensus.

**Failure Context:**
- GitHub Actions run: `24420087047` (first completed CI Tests run after Ubuntu SSL_CERT_DIR + workflow_dispatch patch)
- Failing job: `localhost-auth-playwright`
- Failing step: `Run localhost auth/session Playwright lane`

**Classification:** Aspire AppHost readiness/bootstrap contract failure centered on **Keycloak service availability**, not Linux certificate setup and not downstream Playwright auth logic.

**Evidence Summary:**
1. **Workflow setup now passes:**
   - Configure Linux certificate trust: pass
   - Trust .NET development certificate: pass
   - Setup Node, Setup .NET, dependency installs, Playwright browser install, ASP.NET prerequisites: pass

2. **Failure point:**
   - `LiveAppHost.waitForReadiness()` timeout after ~4 minutes
   - All service readiness probes pass except Keycloak
   - Keycloak discovery endpoint returns no response

3. **AppHost log signal:**
   - `fail: Aspire.Hosting.Dcp.dcpctrl.ServiceReconciler.Proxy[0]`
   - `Error: Could not establish TCP connection to endpoint: dial tcp 127.0.0.1:32768: connect: connection refused`
   - Aspire marks `/keycloak` endpoint ready, but upstream is still rejecting connections

**Root Cause:** Keycloak container reaches "service ready" state in Aspire before its HTTP endpoints are actually available. Consumers of Keycloak (test suite, workflow hub, etc.) wait for container readiness but not HTTP health.

**Decision:** Next action is to **harden the AppHost dependency contract**—add real HTTP health/discovery gates to Keycloak-dependent services rather than relying on `.WaitFor(keycloak)` container state alone.

**Smallest Fix Next:**
1. Review `src/UmbracoPrism.AppHost/Program.cs` Keycloak resource definition and readiness logic
2. Add HTTP health check (e.g., `GET /health` or discovery endpoint) to Keycloak readiness gate
3. Ensure dependent services wait for HTTP readiness, not just container readiness
4. Rerun `localhost-auth-playwright` lane to confirm
5. Only if Keycloak becomes healthy but readiness exceeds 240s budget, consider narrow timeout increase

**Canonical References:**
- GitHub Actions run: `24420087047`
- Job logs: `localhost-auth-playwright` step output
- Implementation files:
  - `src/UmbracoPrism.Client/tests/support/live-app-host.ts` (readiness probes)
  - `src/UmbracoPrism.AppHost/Program.cs` (Keycloak resource + readiness gates)
  - `.github/workflows/ci-tests.yml` (workflow definition)

**Session Log:** `.squad/orchestration-log/2026-04-14T21:34:14Z-scribe-ci-failure-session.md`

---

## 📌 2026-04-14: Tangy & Blathers — CI Regression Fix: Remove Custom Health Checks

**Status:** Team consensus; ready for implementation by Blathers.

**Context:**
- GitHub Actions run: `24423772285` (localhost-auth-playwright job timeout after ~4 minutes)
- Regression introduced by commit `6b203ec` which added custom health checks
- Both Tangy and Blathers independently diagnosed the same root cause

**Root Cause — Custom Health Check Circular Dependency:**

1. Commit `6b203ec` added `.WithHttpHealthCheck(...)` to Keycloak container
2. Added `builder.Services.AddHealthChecks()` with custom `KeycloakProxyHealthCheckName` health check
3. Added `.WithHealthCheck(KeycloakProxyHealthCheckName)` to keycloakProxy resource
4. The custom health check probes `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration` (the proxy's own endpoint)
5. **Deadlock:** Aspire waits for health check to pass before marking keycloakProxy ready, but the health check can't succeed until the proxy is serving requests

**Evidence:**
- Aspire log: `Error handling TCP connection ... connect: connection refused`
- Readiness status: Aspire dashboard ready, but TestSite and Keycloak unreachable
- Playwright timeout after 240 seconds waiting for endpoints

**Decision: Remove Custom Health Checks**

1. Delete `builder.Services.AddHealthChecks()` block from `src/UmbracoPrism.AppHost/Program.cs`
2. Delete `.WithHttpHealthCheck(...)` from Keycloak container definition
3. Delete `.WithHealthCheck(KeycloakProxyHealthCheckName)` from keycloakProxy
4. Keep `.WaitFor(keycloak)` dependency chain — Aspire's container-level readiness is sufficient
5. Rely on Playwright's comprehensive readiness probes (already in place, already passing before regression)

**Why This Works:**
- Aspire's built-in container readiness detection works correctly without custom probes
- Playwright test suite already checks all endpoints behaviorally (TestSite home, seed contract, Keycloak discovery, MockBusinessApp)
- 240-second test timeout with app-level probes is sufficient and safe
- Removes the circular dependency that caused the deadlock

**Why This Is Safe:**
- Local testing (per history) showed 8/8 tests passing before `6b203ec`
- Reverting the health check additions doesn't remove any functionality—it restores the working baseline
- No changes to core Aspire orchestration or test readiness logic

**Implementation Notes:**
- Also remove `using Microsoft.Extensions.DependencyInjection` and `using Microsoft.Extensions.Diagnostics.HealthChecks` if no longer needed
- Verify CI passes with `localhost-auth-playwright` job
- If needed, can re-run with `workflow_dispatch` to isolate this fix from other changes

**Assigned To:** Blathers  
**Session Log:** `.squad/orchestration-log/2026-04-14T21:37:00Z-scribe-ci-regression-session.md`  
**Inbox Decisions Merged:** `tangy-ci-failure-followup.md`, `blathers-ci-failure-followup.md`

---

## 📌 2026-04-14 (RESOLVED): Tangy & Blathers — Post-Deadlock Fix CI Failure Investigation

**Status:** Investigation complete; root cause identified and fix ready (see next decision record).

**Context:**  
- Health check deadlock regression fixed via commit `0497571` (removal of `.WithHealthCheck()` from keycloakProxy)
- CI run **post-0497571** still fails with Keycloak container connectivity issues
- Previous run `24425752344` showed: keycloak-proxy starts successfully, but Keycloak container reports "connection refused" on port 32768

**Failure Classification:**
- **Not:** Health check circular dependency (that's resolved in 0497571)
- **Likely:** Keycloak container port binding, networking, or bootstrap sequencing issue
- **Evidence:** TCP connection refused after proxy starts; suggests Keycloak container not listening or mapped ports misconfigured

**Team Assignment:**
- **Tangy:** Diagnose latest CI run failure; trace Keycloak container logs; identify root cause (port binding? networking? startup order?)
- **Blathers:** Trace AppHost Keycloak resource definition and startup path; recommend smallest fix (health gate? environment? networking? retry?)

**Orchestration Logs:**
- `.squad/orchestration-log/2026-04-14T22:29:46Z-tangy-ci-keycloak-investigation.md`
- `.squad/orchestration-log/2026-04-14T22:29:46Z-blathers-apphost-keycloak-fix.md`

**Next Decision Merge Point:** When Tangy and Blathers complete their analysis, merge findings into next canonical decision record.

---

## 📌 2026-04-14: Tangy & Blathers — Keycloak Container HTTP Health Check Surgical Restore

**Status:** Root cause identified and fix ready for validation; Blathers implementation in progress.

**Investigation Summary:**

Commit `0497571` removed ALL health checks to fix the circular dependency introduced in `6b203ec`, but the removal was too broad. Analysis by Tangy and Blathers identified that the Keycloak **container** HTTP health check was non-circular and necessary.

**Root Cause Chain:**

1. **Commit `6b203ec`** (regression): Added `.WithHealthCheck(KeycloakProxyHealthCheckName)` to keycloakProxy, which probed the proxy's own HTTPS endpoint (`https://localhost:8443/realms/prism-dev/.well-known/openid-configuration`). Aspire deadlocked: proxy couldn't become ready until health check passed, but health check couldn't probe an unready proxy.

2. **Commit `0497571`** (over-correction): Removed ALL health checks, including the Keycloak **container** HTTP health check. Result: Aspire marks Keycloak container ready before HTTP endpoints actually start accepting connections.

3. **Current Failure (CI run `24425752344`)**: Keycloak Docker container exists and is running, but Aspire's `.WaitFor(keycloak)` returns before the container's HTTP port (8080) is available. Downstream services (keycloakProxy, tests) immediately fail to connect.

**Evidence:**

- **Tangy Finding:** CI logs show "connection refused on 127.0.0.1:32768" despite container ID being created and Aspire marking service ready
- **Blathers Finding:** The keycloakProxy health check was the deadlock culprit; the container check was necessary and non-circular
- **Keycloak Import Delay:** Keycloak takes additional bootstrap time after container start to import realm and start accepting HTTP connections

**Decision: Restore Non-Circular Keycloak Container Health Check**

Surgical fix to `src/UmbracoPrism.AppHost/Program.cs`:

```csharp
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")  // ← Restored (container check)
    .WithEnvironment(...)
    .WithBindMount(...)
    .WithArgs("start-dev", "--import-realm", "--proxy-headers", "xforwarded");

var keycloakProxy = builder.AddProject(...)
    .WaitFor(keycloak);  // ← No .WithHealthCheck() (proxy check stays removed)
```

**Why This Correct:**

1. **Non-circular:** Container health check targets the container's own HTTP endpoint (port 8080), not a dependent service
2. **Necessary:** Gates Aspire readiness on actual HTTP availability, not just container process state
3. **Safe:** Preserves the circular dependency fix from `0497571` by leaving keycloakProxy without custom health checks

**Pattern for Future:**

- ✅ **Container resource:** `.WithHttpHealthCheck("/path")` → container's own HTTP port (safe)
- ❌ **Proxy resource:** `.WithHealthCheck(customCheckName)` → resource's own HTTPS proxy (deadlock risk)

**Implementation:**

- **Commit:** `933f97f` (Blathers, backend orchestration)
- **File:** `src/UmbracoPrism.AppHost/Program.cs`
- **Change:** Add `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` after `.WithHttpEndpoint()` on keycloak container

**Validation:**

1. Re-run `localhost-auth-playwright` CI lane
2. Monitor Keycloak startup logs for successful health check pass
3. Verify all downstream services connect successfully
4. If tests pass, close investigation; if fail, escalate to next diagnostic layer

**Assigned To:** Blathers (implementation), Tangy (validation)

**Session Logs:**
- Investigation: `.squad/orchestration-log/2026-04-14T22:29:46Z-tangy-ci-keycloak-investigation.md`
- Implementation: `.squad/orchestration-log/2026-04-14T22:29:46Z-blathers-apphost-keycloak-fix.md`
- Merge coordination: `.squad/orchestration-log/2026-04-14T23:45:00Z-scribe-keycloak-merge-session.md`

**Inbox Decisions Merged:**
- `tangy-keycloak-container-ci.md` → consolidated into this record
- `blathers-keycloak-container-ci.md` → consolidated into this record
