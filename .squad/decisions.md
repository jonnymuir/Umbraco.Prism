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

---

## 📌 2026-04-14 (FINAL): Tangy & Blathers — Keycloak Health Check Endpoint Consensus

**Status:** Investigation complete; team consensus achieved; smallest fix identified.

**Context:**
- GitHub Actions run: `24426777068` (localhost-auth-playwright timeout after ~240 seconds)
- Both Tangy (QA) and Blathers (Backend) investigated independently and reached identical root cause
- Keycloak marked "Ready" by Aspire, but HTTP connections refused by downstream services

**Team Consensus:**

Both agents identified the same issue independently:

| Agent | Finding | Evidence |
|-------|---------|----------|
| **Tangy (QA)** | `/health/ready` doesn't validate realm import; only checks process state | Run 24426777068: "service ready" yet "connection refused" on realm endpoint |
| **Blathers (Backend)** | Commit eb19498 used wrong endpoint; `/health/ready` insufficient for realm-dependent services | Container check needs realm discovery endpoint validation |

**Root Cause Chain:**

1. **Commit `6b203ec`**: Correct container endpoint (`/realms/.../openid-configuration`) ✅ but circular proxy check ❌
2. **Commit `0497571`**: Removed ALL health checks (over-correction) ❌
3. **Commit `eb19498`**: Restored container check but wrong endpoint (`/health/ready`) ❌
4. **Current failure**: Process started, realm import still in progress when Aspire marks ready ❌

**Smallest Correct Fix:**

**File:** `src/UmbracoPrism.AppHost/Program.cs` **Line:** 30

```csharp
// FROM:
.WithHttpHealthCheck("/health/ready")

// TO:
.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")
```

**Why Correct:**

1. **Non-circular:** Container's own HTTP port (8080), not proxy (8443)
2. **Validates realm:** Discovery endpoint requires realm import to complete
3. **Proven:** This exact endpoint was correct in `6b203ec`
4. **Aligned:** Playwright also probes this same endpoint

**Pattern for Future:**

- ✅ Container `.WithHttpHealthCheck("/realms/.../openid-configuration")` → validates realm availability
- ✅ Container `.WithHttpHealthCheck("/health")` → basic liveness
- ❌ Container `.WithHttpHealthCheck("/health/ready")` → insufficient for realm-dependent services
- ❌ Resource `.WithHealthCheck(customCheckName)` → resource's own HTTPS proxy (circular deadlock)

**Risk:** LOW — restores proven working endpoint from `6b203ec` without circular proxy dependency

**Assigned To:** Blathers (implementation)

**Session Logs:**
- Tangy Investigation: `.squad/orchestration-log/2026-04-14T22:58:28Z-tangy-latest-keycloak-followup-archived.md`
- Blathers Analysis: `.squad/orchestration-log/2026-04-14T22:58:28Z-blathers-keycloak-health-check-archived.md`
- Merge & Consensus: `.squad/orchestration-log/2026-04-14T23:00:00Z-scribe-decision-merge-final.md`

**Inbox Decisions Merged:**
- `tangy-latest-keycloak-followup.md` → archived
- `blathers-keycloak-health-check.md` → archived

---

## 📌 2026-04-16: Tangy — Playwright Readiness Contract (Strict HTTPS Proxy Boundary)

**Decision:** Keep the browser-facing Keycloak contract on `https://localhost:8443` and do not weaken readiness probes to raw HTTP or generic liveness endpoints.

**Why:**
- Localhost auth flow requires HTTPS proxy visibility for issuer validation
- Generic container health checks can pass while proxy chain is broken
- CI evidence: run `24427460363` passed container health but failed browser-facing proxy, proving this boundary is not optional

**Implications:**
- Playwright readiness contract is correct as-is; no weakening allowed
- Never accept `/health/ready` as equivalent to discovery endpoint probe
- Contract is non-negotiable for CI stability

**Session Log:** `.squad/log/2026-04-16T08:11:04Z-keycloak-ci-resolution-session.md`

---

## 📌 2026-04-16: Blathers — AppHost Endpoint Injection (Dynamic Proxy Binding)

**Decision:** Have `src/UmbracoPrism.AppHost/Program.cs` inject `ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address` from `keycloak.GetEndpoint("http")` into the keycloak-proxy project.

**Why:**
- Preserves local proxy design (browser/tests still talk to `https://localhost:8443`)
- Lets Aspire decide the actual Keycloak HTTP endpoint; removes hardcoded port assumptions
- Hardcoded `http://localhost:8080` in `appsettings.json` is unstable in CI container environments where port allocation is dynamic

**Implications:**
- keycloak-proxy no longer owns upstream endpoint knowledge; AppHost owns runtime discovery
- Proxy is stateless configuration consumer; startup no longer depends on specific loopback port
- HTTPS proxy contract preserved; downstream routing and browser-facing contracts unaffected

**Changes:**
- `src/UmbracoPrism.AppHost/Program.cs` — Injects endpoint at startup
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — Added regression assertion

**Validation:**
- ✅ `dotnet build UmbracoPrism.sln`
- ✅ `dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests`
- ✅ `npm run test:playwright:localhost-auth`

**Session Log:** `.squad/log/2026-04-16T08:11:04Z-keycloak-ci-resolution-session.md`

---

## 📌 2026-04-16: Blathers — Timeout Diagnostics & Readiness Probes

**Decision:** For the localhost auth readiness timeout class:
1. Keep existing browser-facing readiness contract
2. Change readiness loop to poll every 10 seconds with structured state-change/checkpoint logs
3. Fail after 3 minutes instead of 4

**Why:**
- Improved diagnostic signal distinguishes TestSite, proxy, and upstream failures without log flooding
- 10-second poll interval balances feedback speed with noise reduction
- Reduces timeout from 240s to 180s for faster feedback while preserving headroom for observed cold-start paths (~20–30s locally)

**Implications:**
- CI timeout failures now identify missing HTTP response vs. wrong status vs. bad redirect vs. unexpected body content
- If CI shows legitimate cold starts approaching 3 minutes, review whether budget is too aggressive

---

## 📌 2026-04-16: Tangy — Localhost Auth Readiness Diagnostics Direction

**Decision:** Move to implementation of AppHost/Keycloak startup fix rather than continuing broad diagnostic passes. The current evidence already isolates the break to the Keycloak upstream/proxy hop.

**Why:**
- Failure repeats at same boundary: `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration` never returns while surrounding ports listen
- Another diagnostic pass would likely reproduce the same symptom without narrowing root cause materially
- Highest-value logging improvement is actual Keycloak container stdout/stderr, best done alongside the fix

**Single recommended logging addition:**
- On readiness timeout, append actual Keycloak container stdout/stderr log
- This distinguishes "container never finished booting" vs. "realm import failed" vs. "proxy not serving"

**Implication for team:**
- Prioritize AppHost/Keycloak startup-path fix first
- Treat additional logging as surgical support, not replacement for fix

---

## 📌 2026-04-16: Tangy — Readiness Log Analysis (TestSite/Keycloak Split)

**Decision:** Reclassify current `localhost-auth-playwright` CI blocker from **Keycloak-only** to broader **browser-facing readiness failure**: Aspire and MockBusinessApp come up, but both TestSite (`https://localhost:44345`) and browser-facing Keycloak discovery endpoint remain non-responsive.

**Why:**
- Improved readiness harness proves two dependencies become ready (Aspire dashboard, MockBusinessApp) while four others never do
- TestSite home marker times out repeatedly; lane fails on auth/discovery AND Umbraco/TestSite surface itself
- Failing endpoints share same symptom (request timeout after 5000ms), stronger evidence of hanging requests or upstream waits

**Implications:**
- Do not describe lane as blocked on Keycloak alone
- Next investigation: why TestSite requests hang while MockBusinessApp is healthy; whether TestSite startup/auth middleware stalls on Keycloak metadata
- Current Playwright readiness contract is valuable and should stay strict—it exposes real browser-facing failure boundary

---

## 📌 2026-04-16: Tangy — Timeout Diagnostics Review

**Decision:** For localhost auth readiness timeout class, best QA direction is:
1. Keep AppHost fix direction (Program.cs realm discovery health check + dynamic proxy upstream injection)
2. Add readiness diagnostics: log **service-ready transitions with elapsed time** plus **expected-vs-actual probe output**
3. Keep polling **finer than 10 seconds** while allowing modestly shorter timeout if CI data supports it

**Why:**
- This failure mode is about *which dependency is still lagging* or *what it returned when lagged*; transition timestamps and probe values are missing signal
- 10-second poll interval too coarse for CI triage; hides ordering, flapping, near-ready behavior
- Keycloak readiness endpoint is repeated regression point; tiny regression assertion on Program.cs worth keeping close

**QA recommendation:**
- Record when each readiness probe first becomes healthy; include elapsed times in timeout output
- On failure, print expected vs. actual status/header/body snippets instead of only "missing X"
- Prefer **2-second polling** with roughly **90-120 seconds** total timeout for auth lane once current startup path is stable; keep 240 seconds only if CI shows legitimate cold-start variance above that band
- Preserve browser-facing Keycloak probe on `https://localhost:8443/.../.well-known/openid-configuration`

**Tightly coupled guard:**
- Add/keep small regression assertion that `src/UmbracoPrism.AppHost/Program.cs` uses `.WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")` and NOT `.WithHttpHealthCheck("/health/ready")`

---

## 📌 2026-04-16: Blathers — Keycloak Container Log Capture at Readiness Timeout

**Decision:** Added `captureDockerContainerLogs(namePattern: string): string` helper to `src/UmbracoPrism.Client/tests/support/live-app-host.ts`. At readiness timeout, runs `docker ps` and `docker logs --tail 100` for any container matching pattern (case-insensitive), includes output in timeout error message.

**Why:**
- Zero-cost at normal run time (only executes on timeout)
- Uses already-imported `spawnSync`—no new imports
- Surfaces Keycloak's realm import outcome and HTTP bind status directly in CI logs
- Self-contained and safe: handles Docker-unavailable and no-match cases gracefully

**Initial usage:** Captures Keycloak container logs via `captureDockerContainerLogs('keycloak')`

---

## 📌 2026-04-16: Blathers — Keycloak CI Pull Fix

**Root Cause:** `localhost-auth-playwright` CI timeout caused by Docker image pull time for `quay.io/keycloak/keycloak:26.0.0` exceeding readiness budget.

**Evidence:**
- `docker ps` at timeout returned zero matching containers—container still being pulled, not yet created
- Port 8080 listening but connection attempts failed with `connection refused`
- keycloakProxy and testsite have `.WaitFor(keycloak)` dependencies and never received process-start log lines
- Keycloak health check (realm discovery endpoint) never passed

**Timeline estimate:**
- Docker pull: ~90-120s
- Keycloak startup: ~60s
- Realm import: ~30s
- Total: 3-4 minutes (previous timeout was 180s)

**Fix (Commit `778ef48`) — Three changes:**

1. **Pre-pull Keycloak image in CI** (`.github/workflows/ci-tests.yml`)
   - Added step before test run: `docker pull quay.io/keycloak/keycloak:26.0.0`
   - Why: Eliminates Docker pull time from hot path during Aspire startup

2. **Increase readiness timeout** (`src/UmbracoPrism.Client/tests/support/live-app-host.ts`)
   - Changed `readinessTimeoutMs` from `180_000` (3 min) to `300_000` (5 min)
   - Why: Provides safety headroom if Docker pull not pre-pulled

3. **Fix container log capture** (`src/UmbracoPrism.Client/tests/support/live-app-host.ts`)
   - Updated `captureDockerContainerLogs()`: add `-a` flag to include stopped containers, fall back to `podman ps -a` if docker unavailable
   - Why: Aspire on GitHub Actions may use Podman; both installed on Ubuntu runners

**Pattern for Future:**
- Always pre-pull container images in CI before starting Aspire AppHost
- Set readiness timeout with headroom beyond typical startup time
- Account for: pull time + startup time + application-specific bootstrap (realm import, etc.)
- Readiness timeout budget: 3 min local dev (images cached), 5 min CI (first-run scenarios), never rely on Aspire container readiness alone

---

## 📌 2026-04-16: Blathers — Next Step for Localhost Auth Timeout

**Decision:** Take one more targeted logging pass before next code fix.

**Why:** Failing revision already contains previously agreed AppHost changes (WithHttpHealthCheck on Keycloak discovery + dynamic proxy endpoint injection), yet CI still shows `/keycloak` marked Ready immediately before `connect: connection refused` on Aspire's resolved endpoint. Log never shows keycloak-proxy or testsite process-start lines.

**Missing fact to capture:** At moment Aspire marks `/keycloak` Ready, did Keycloak container itself finish realm import and bind HTTP endpoint, or only DCP/service proxy becoming routable?

**Next logging target:** Extend `src/UmbracoPrism.Client/tests/support/live-app-host.ts` timeout diagnostics to include Keycloak container's own startup/output (and explicitly resource-start lines for keycloak-proxy/testsite) so next CI failure tells whether break is in Keycloak bootstrap, Aspire readiness, or downstream process launch.

---

## 📌 2026-04-19: Copilot — GDS Extensibility Model Directive

**User Input:** Jonny Muir (2026-04-19T07:57:49Z)

**Direction:** Custom/override GDS components should be definable as Umbraco 17 element types in backoffice, with HTML template provided on element type — think Block Grid / Block List pattern. If new component needed, describe in backoffice and supply template; workflow renderer picks up automatically.

**Rationale:** User request for extensibility/override story for GDS workflow engine.

**Implementation delegate:** Brewster to design formal element type extensibility spec; Isabelle to prototype component binding.

---

## 📌 2026-04-19: Tom Nook & Brewster — GDS Step Descriptor Protocol & Element Type Extensibility

**Status:** Proposed architecture; two background design sessions completed.

**Decision:** Establish BA-as-brain pattern with Step Descriptor Protocol as single JSON contract and element type extensibility for new field/component types.

**Core Components:**

### 1. Workflow Engine Architecture
- **Business App owns:** workflow logic, routing, validation, state machines
- **Umbraco is:** component renderer, descriptor consumer, UI input collector
- **Key benefit:** Zero workflow knowledge in UI; enables multiple UI consumers to use same BA contract

### 2. Step Descriptor Protocol
JSON response returned by BA for every workflow interaction. Contains all rendering requirements for one page.

**Envelope:**
- Session management: workflowId, instanceId, sessionToken, stateVersion
- Step identity: stepId, stepType, progress
- Content: varies by stepType (question, task-list, check-answers, confirmation, error)
- Actions: dynamic button/link set (continue, save-and-return, change, start-section, etc.)

**Content Variants:**
- QuestionContent: fieldId, fieldType, label, hint, validation, defaultValue, required
- TaskListContent: tasks with status (todo, in-progress, completed), descriptions, links
- CheckAnswersContent: sections with question-answer pairs for review
- ConfirmationContent: title, message, referenceNumber, nextSteps
- ErrorContent: errorCode, message, userMessage, recoveryPath

### 3. Extensibility via Element Types
New question types, task list variants, confirmation patterns added via pluggable element type system:
- BA returns new fieldType in descriptor (e.g., "custom-widget")
- Umbraco element type system renders fieldType via registered handler
- HTML template provided on element type definition
- No BA/Umbraco coordination required for new types

**Why Correct:**
1. **Non-circular:** Container's own HTTP port, not dependent service
2. **Extensible:** fieldType enum expansion adds new types without API changes
3. **Proven:** Block Grid/Block List pattern in Umbraco 17 validates approach
4. **Safe:** Umbraco owns UI component binding; BA owns workflow logic

**Pattern for Future:**
- Container `.WithHttpHealthCheck("/realms/.../openid-configuration")` → validates realm availability
- Container `.WithHttpHealthCheck("/health")` → basic liveness
- ❌ Container `.WithHttpHealthCheck("/health/ready")` → insufficient for realm-dependent services
- ❌ Resource `.WithHealthCheck(customCheckName)` → resource's own HTTPS proxy (circular deadlock)

**Implementation Plan:**
1. Backend API contract alignment (Blathers)
2. Element type registration spec (Brewster)
3. GDS component rendering prototype (Isabelle)
4. Test contract and fixtures (Tangy)

**Session Logs:**
- Design: `.squad/log/2026-04-19T07:59:21Z-gds-workflow-engine-design.md`
- Orchestration: `.squad/orchestration-log/2026-04-19T07:59:21Z-tom-nook-gds-workflow-design.md`
- Orchestration: `.squad/orchestration-log/2026-04-19T07:59:21Z-tom-nook-gds-protocol-design.md`

---

## 📌 2026-04-20: Blathers — GDS Models Evolution

**Date:** 2026-04-20  
**Agent:** Blathers (Backend Dev)  
**Context:** Evolving C# workflow models and Business App engine to support full GDS (GOV.UK Design System) step types and field types

### Changes Made

#### 1. Renamed `Archetype` → `StepType` Throughout Stack

**Models Updated:**
- `WorkflowRenderPayload.Archetype` → `WorkflowRenderPayload.StepType` (UmbracoPrism.Shared)
- `WorkflowStateFile.Archetype` → `WorkflowStateFile.StepType` (MockBusinessApp)
- Updated `WorkflowInstanceSummary` default from `"Collect"` → `"question"`

**Step Type Values (Old → New):**
- `"Collect"` → `"question"` — form fields collection
- `"Review"` → `"check-answers"` — review submitted answers
- `"Completion"` → `"confirmation"` — final confirmation page
- `"StatusTimeline"` → `"status-timeline"` — read-only status display
- Added: `"task-list"` — GOV.UK task list pattern (not yet wired in engine)

**Engine Logic Updated:**
- `BusinessAppWorkflowEngine.BuildEnvelope`: uses `state.StepType` for response state mapping
- `BusinessAppWorkflowEngine.GetInstances`: uses `state.StepType` for completion checks

**Controller Bridge Updated:**
- `WorkflowPageController.BuildViewModel`: maps `render?.StepType` to ViewModel's `Archetype` property (preserves front-end contract)

#### 2. Extended Field Models with GDS Properties

**Added to `FieldFile` and `FieldRenderPayload`:**

```csharp
/// <summary>Currency/unit prefix displayed before the input (e.g., "£").</summary>
public string? Prefix { get; init; }

/// <summary>
/// For radios/checkboxes: sub-fields revealed when the parent option is selected.
/// Key is the option value; value is the list of fields shown when that option is active.
/// </summary>
public IReadOnlyDictionary<string, IReadOnlyList<FieldFile>>? ConditionalFields { get; init; }
```

#### 3. Updated `WorkflowFieldValidator` for New Field Types

**New Field Types Supported:**

1. **`radios`** — alias for `radio` (backward compatible)
2. **`checkboxes`** — alias for `checkboxlist` (backward compatible)
3. **`date-input`** — 3-part GDS date input (`{key}-day`, `{key}-month`, `{key}-year`)
   - Validates each part as integer in correct range
   - Reconstructs as ISO date string (`YYYY-MM-DD`)
   - Partial submissions flagged with `"PARTIAL"` marker
4. **`currency`** — decimal validation with InvariantCulture (no commas/currency symbols)
   - Rejects `"1,234.56"` ✅
   - Rejects `"£100"` ✅
   - Accepts `"1234.56"` ✅
5. **`file`** — whitelisted for now (no validation logic yet)

**Backward Compatibility Preserved:**
- `"date"` — single `<input type="date">` still supported
- `"radio"` — still works (treated as `radios`)
- `"checkboxlist"` — still works (treated as `checkboxes`)

**Culture-Safe Decimal Parsing:**
- Used `NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign` with `CultureInfo.InvariantCulture`
- Ensures commas are never accepted in currency/number fields

#### 4. Updated Workflow Seeds

**Existing Seeds Updated:**
- `information-request-v1.json`: `archetype` → `stepType` with new values
- `community-enquiry-v1.json`: `archetype` → `stepType` with new values
- `personal-details-v1.json`: `dateOfBirth` changed from `"date"` to `"date-input"`
- `request-details-v1.json`: `urgency` changed from `"radio"` to `"radios"`

**New Seed Created: `planning-notification-v1.json`**

A realistic GOV.UK-style planning permission application demonstrating all new field types:

**Workflow Steps:**
1. `project-details` (question) — project description
2. `work-type` (question) — radios with conditional reveal
3. `timeline-cost` (question) — date-input + currency
4. `affected-parties` (question) — checkboxes
5. `check-answers` (check-answers) — review page
6. `complete` (confirmation) — success page

**Field Groups Created:**
- `project-info-v1.json` — text + textarea fields
- `work-type-info-v1.json` — **radios with `conditionalFields`** (reveals textarea when "Other" selected)
- `timeline-cost-info-v1.json` — **date-input** + **currency** (with `prefix: "£"`)
- `affected-parties-info-v1.json` — **checkboxes** + radios

### Test Results

✅ **All 416 Core tests passing**

Key validations:
- Currency field rejects commas: `"1,234.56"` → invalid ✅
- Currency field accepts plain decimals: `"1234.56"` → valid ✅
- Currency field rejects prefixes: `"£100"` → invalid ✅
- Date-input validation added (no existing tests to break)

### Breaking Changes

⚠️ **API Contract Change:**

- `WorkflowRenderPayload.Archetype` is now `WorkflowRenderPayload.StepType`
- Front-end consumers (Razor views, Storybook) must update to use `StepType`
- Controller bridge updated to map `StepType` → ViewModel `Archetype` for transition period

### Status

✅ Complete — All changes validated, tests passing, build clean

---

## 📌 2026-04-20: Isabelle — GDS View Layer for Workflow Engine

**Date:** 2026-04-19  
**Author:** Isabelle (Frontend Dev)  
**Type:** Major UI refactor

### Decision

Replaced workflow views with GOV.UK Design System (GDS) patterns:
- Installed `govuk-frontend` 5.9.0 via npm in TestSite
- Renamed `_WorkflowStep-Collect.cshtml` → `_WorkflowStep-Question.cshtml`
- Updated all workflow step partials to use `govuk-*` CSS classes
- Updated `PrismFieldTagHelper` to emit GDS-compliant HTML markup
- Updated `PrismErrorSummaryTagHelper` to match GDS error summary pattern
- Added MSBuild target to install govuk-frontend before build

### Why

The GDS provides:
- Proven, accessible UI patterns used across UK government services
- Mobile-first responsive design
- WCAG 2.1 AA compliance out of the box
- Familiar patterns for UK public sector users

The workflow engine is the primary user-facing interaction surface, so using GDS strengthens accessibility and user trust.

### Changes Made

#### npm/Build Infrastructure
- `src/UmbracoPrism.TestSite/package.json`: Added `govuk-frontend: ^5.9.0`
- `src/UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj`: Added `InstallGovukFrontend` target to run `npm ci` and copy CSS/JS to wwwroot before build
- `src/UmbracoPrism.TestSite/wwwroot/css/govuk-frontend.min.css`: Copied from node_modules (129KB)
- `src/UmbracoPrism.TestSite/wwwroot/js/govuk-frontend.min.js`: Copied from node_modules (47KB)

#### Master.cshtml
- Added `class="govuk-template"` to `<html>`
- Added `class="govuk-template__body"` to `<body>`
- Added `<link>` to `govuk-frontend.min.css` (before site CSS)
- Added `<script>` for `govuk-frontend.min.js` with `GOVUKFrontend.initAll()`
- Kept existing Prism CSS for non-workflow areas (header, nav, etc.)

#### Workflow Step Partials

**_WorkflowStep-Question.cshtml** (NEW, replaces Collect):
- GDS one-thing-per-page pattern
- Single-field groups: unwrapped with h1-level labels
- Multi-field groups: `govuk-fieldset` with legend
- Buttons use `govuk-button`, `govuk-button--secondary`, `govuk-button--warning`

**_WorkflowStep-Review.cshtml**:
- GDS check-answers pattern with `govuk-summary-list`
- "Change" links with visually-hidden context
- Two-column grid layout

**_WorkflowStep-Completion.cshtml**:
- GDS confirmation panel (`govuk-panel govuk-panel--confirmation`)
- Reference number in panel body
- "What happens next" section

**_WorkflowStep-StatusTimeline.cshtml**:
- Updated to use `govuk-heading-l`, `govuk-body`, `govuk-button-group`

**_WorkflowStep-TaskList.cshtml** (NEW):
- GDS task list pattern
- Status tags: `govuk-tag`, `govuk-tag--blue` (in progress), `govuk-tag--grey` (not started)
- Section completion tracking

#### WorkflowPage.cshtml Dispatch
- Added mapping logic: `Archetype.ToLowerInvariant() switch { "collect" => "Question", "review" => "Review", ... }`
- Routes lowercase archetype values to appropriate partials

#### TagHelpers

**PrismFieldTagHelper.cs**:
- Updated all CSS classes from `prism-*` to `govuk-*`:
  - `prism-form-group` → `govuk-form-group`
  - `prism-label` → `govuk-label`
  - `prism-hint` → `govuk-hint`
  - `prism-field-error` → `govuk-error-message` (with `<span class="govuk-visually-hidden">Error:</span>` prefix)
  - `prism-input` → `govuk-input`
  - `prism-textarea` → `govuk-textarea`
  - `prism-select` → `govuk-select`
  - `prism-fieldset` → `govuk-fieldset`
  - `prism-legend` → `govuk-fieldset__legend`
  - `prism-radio-item` → `govuk-radios__item`
  - `prism-radio` → `govuk-radios__input`
  - `prism-checkbox-item` → `govuk-checkboxes__item`
  - `prism-checkbox` → `govuk-checkboxes__input`
- Changed required indicator from `<span class="prism-required">*</span>` to `<span class="govuk-visually-hidden">(required)</span>`
- Added `data-module="govuk-radios"` and `data-module="govuk-checkboxes"` for GDS JS enhancement
- Wrapped radio/checkbox options in `govuk-radios` / `govuk-checkboxes` containers

**PrismErrorSummaryTagHelper.cs**:
- Changed wrapper class from `prism-error-summary` to `govuk-error-summary`
- Added `data-module="govuk-error-summary"`
- Wrapped content in `<div role="alert">` with nested structure:
  - `govuk-error-summary__title`
  - `govuk-error-summary__body` containing `govuk-list govuk-error-summary__list`

### Backward Compatibility

- **Prism CSS still loaded**: Non-workflow pages (home, vinyl catalog, etc.) still use `prism-*` classes
- **Conditional fields**: Kept `prism-field--conditional` class and `data-conditional-on`/`data-visible-when` attributes for backward compat with existing `prism-conditional-fields.js`
- **Form class**: `PrismWorkflowFormTagHelper` still emits `class="prism-workflow"` — this is fine as it doesn't conflict with GDS
- **Old field types**: `radio` and `checkboxlist` still work (rendered identically to GDS `radios`/`checkboxes`)

### Status

✅ Complete — All views rebuilt with GDS patterns, tests passing, build clean

---

## 📌 2026-04-20: Tangy — GDS Field Type Test Coverage

**Date:** 2026-04-15  
**Author:** Tangy (Tester)  
**Status:** Tests added and passing

### Summary

Added comprehensive test coverage for new GDS-style field types to `WorkflowFieldValidatorTests.cs`:
- `radios` (alias for `radio`)
- `checkboxes` (alias for `checkboxlist`)  
- `date-input` (3-part date input: day/month/year)
- `currency` (decimal validation)
- `file` (whitelisted, no additional validation)

### Test Coverage Added

#### Integration into Existing Theory Test
Extended the `GivenFieldType_WhenValidValue_ThenIsValidTrue` theory test to include:
- `radios` with options whitelist
- `checkboxes` with options whitelist
- `currency` with decimal values
- `file` as a whitelisted type

#### New date-input Tests
Added 4 dedicated tests for the complex 3-part date-input field:
1. `GivenDateInputField_WhenAllPartsProvided_ThenIsValidTrue` — validates complete date (day/month/year)
2. `GivenDateInputField_WhenDayPartMissing_WhenRequired_ThenIsValidFalse` — tests required validation
3. `GivenDateInputField_WhenYearInvalid_ThenIsValidFalse` — tests 2-digit year rejection
4. `GivenDateInputField_WhenNotRequired_WhenAllEmpty_ThenIsValidTrue` — tests optional date fields

#### New currency Test
Added parameterized test `GivenCurrencyField_WhenValueSubmitted_ThenValidatesCorrectly`:
- Valid: `1234.56`, `999`, `0`
- Invalid: `1,234.56` (commas), `£100` (currency symbol), `abc` (non-numeric)

### Validation Implementation Status

✅ **All field type logic implemented by Blathers:**
- **currency:** Validated as `decimal` (same logic as `number`)
- **radios:** Options whitelist validation (same as `radio`)
- **checkboxes:** Options whitelist + comma-separated values (same as `checkboxlist`)
- **date-input:** Complex 3-part validation with ISO date reconstruction
- **file:** Whitelisted in field key allowlist

### Test Results

- **Before:** 406 tests passing
- **After:** 416 tests (406 + 10 new GDS field type tests)
- ✅ All 416 tests passing

### Status

✅ Complete — All GDS field type tests passing, build clean

---

## 📌 2026-04-19: Copilot — Ubiquitous Language Directive

**By:** Jonny Muir  
**What:** Use clear, ubiquitous language throughout the codebase. Avoid jargon or abstract terminology when plain terms exist. Specific example: `StepType` is preferred over `Archetype` — "Archetype" is opaque domain jargon; "StepType" communicates exactly what it is. Apply this principle to all naming: models, methods, properties, seed files, comments.

**Why:** User request — captured for team memory. Affects all future naming decisions across Blathers, Isabelle, Brewster, and Tangy work.

**Applied to GDS Phase 1:**
- Renamed `Archetype` → `StepType` (step naming now: `question`, `check-answers`, `confirmation`, `task-list`, `status-timeline`)
- Renamed `_WorkflowStep-Collect.cshtml` → `_WorkflowStep-Question.cshtml`
- Field types: `radios`, `checkboxes`, `date-input`, `currency` (plain names, not `SelectRadio`, `NumericCurrencyField`, etc.)

**Pattern for team:** When choosing a name, ask: "Does this term clearly communicate what it is to someone reading the codebase for the first time?" If not, use a clearer term.

---

## 🔒 2026-04-20: Copper — Aspire OTLP Telemetry Endpoint Security

**By:** Copper (Security Engineer)  
**Decision:** Allow unsecured OTLP transport for local development environments

### Context

Aspire AppHost was displaying a security warning:
> "Telemetry endpoint is unsecured. Untrusted apps can send telemetry to the dashboard."

The OTLP (OpenTelemetry Protocol) endpoint was accepting telemetry without authentication.

### Investigation

- Root cause: Missing `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` environment variable
- Existing `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` only controls dashboard UI access, not the telemetry endpoint
- Aspire 9.x requires explicit acknowledgment of unsecured transport via environment variable

### Decision Rationale

For **local development only**, explicitly allow unsecured OTLP transport:
1. **Development-only context:** All services run on localhost under developer control
2. **Acceptable risk:** No sensitive data in dev telemetry; risk of untrusted telemetry is acceptable locally
3. **Explicit acknowledgment:** Variable documents reviewed and accepted security posture
4. **Reduces noise:** Suppresses warning so real security issues remain visible

### Implementation

- Added `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` to `https` launch profile in `src/UmbracoPrism.AppHost/Properties/launchSettings.json`
- Build validation: passed

### Production Guidance ⚠️

For production or shared environments, use authenticated OTLP endpoints:
- Set `Dashboard:Otlp:AuthMode=ApiKey` in production config
- Distribute API keys securely
- Consider network-level isolation (firewall, private networks)

### Alternatives Considered

**Option A: API Key Auth for Dev** — Rejected (adds complexity without security benefit in single-developer localhost)  
**Option B: Keep the warning** — Rejected (warnings should be addressed or explicitly suppressed)

---

## 📌 2026-04-20: Copper — Aspire OTLP AuthMode Configuration

**Decision:** Set `Dashboard__Otlp__AuthMode=Unsecured` explicitly in development launch configuration

**Context:**
- Aspire Dashboard showed persistent warning: "Telemetry endpoint is unsecured. Untrusted apps can send telemetry to the dashboard."
- Previous fix attempts used `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` and `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`, but neither suppressed the warning.

**Root Cause Analysis:**
- `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` controls dashboard **UI** authentication, not OTLP endpoint security
- `ASPIRE_ALLOW_UNSECURED_TRANSPORT` controls whether HTTP (non-HTTPS) transport is allowed; irrelevant since OTLP endpoint is already HTTPS
- The warning is about **missing API key authentication** on the OTLP endpoint itself — any process can push telemetry without credentials

**Correct Fix:**
The Aspire Dashboard reads `Dashboard:Otlp:AuthMode` (environment variable: `Dashboard__Otlp__AuthMode`) to determine OTLP authentication mode:
- `Unsecured` — No API key required (development mode). Explicitly setting this value suppresses the warning because it signals intentional choice rather than accidental misconfiguration.
- `ApiKey` — Requires API key authentication (production/non-shared environments)

**Implementation:**
Added `"Dashboard__Otlp__AuthMode": "Unsecured"` to `src/UmbracoPrism.AppHost/Properties/launchSettings.json` environment variables.

**Security Posture:**

### Development (Current)
- **Mode:** `Unsecured`
- **Risk:** Any local process can push telemetry to the dashboard
- **Accepted:** Development environment on localhost with no sensitive telemetry data
- **Boundary:** Machine-local only; dashboard and OTLP endpoints not exposed externally

### Production Guidance
For non-development environments (staging, production, shared dev):
1. **Set `Dashboard__Otlp__AuthMode=ApiKey`**
2. Configure API key via `Dashboard__Otlp__PrimaryApiKey` environment variable
3. Distribute the API key to authorized telemetry sources via secure configuration management
4. Consider mutual TLS for additional transport-layer authentication
5. Ensure dashboard and OTLP endpoints are not publicly accessible

**Cleanup:**
Removed `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` from launch config — it was added in a previous fix attempt but does not affect OTLP authentication mode.

**References:**
- [Aspire Dashboard Configuration](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/configuration)
- ASP.NET Core environment variable double-underscore convention: `Section__Subsection__Key`

**Author:** Copper (Security Engineer)  
**Date:** 2026-04-20  
**Files Modified:** `src/UmbracoPrism.AppHost/Properties/launchSettings.json`

---
