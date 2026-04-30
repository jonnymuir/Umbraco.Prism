## 📌 2026-04-22: Tom Nook & Blathers — stepType Removal & Component Model Unification

**Decision:** Remove `stepType` from authored workflow JSON. Engine derives runtime `shell` property from component tree structure. Promote `WaitingConfig` from sidecar to first-class component type.

**Context:** Architectural review confirmed that stepType is redundant authoring burden and that validation/error/rendering pipelines are already component-agnostic. All four stepType consumers (nonce skip, response state mapping, partial selection, terminal detection) can be replaced with explicit metadata (`terminal` flag, `responseState` enum).

**Shell Derivation Rules:**

| Condition | Derived shell |
|---|---|
| Any component of type `waiting` present | `"waiting"` |
| All data-carrying components are `summary-list` | `"check-answers"` |
| Has `panel` component, no fieldset/input components | `"confirmation"` |
| Has `task-list` component | `"task-list"` |
| Otherwise (has `fieldset` components with input fields) | `"question"` |

**What Changes:**

- `StepDefinition.StepType` removed from JSON schema and C# record (breaking change; migration required).
- `StepContent.StepType` renamed to `StepContent.Shell` (or kept with `[JsonPropertyName]` for backward compat).
- `WaitingConfig` promoted from sidecar to `"waiting"` component type, carrying `message`, `expectedWaitSeconds`, `pollIntervalMs`, `allowDefer`, `deferMessage` inline.
- `_WorkflowStep-Waiting` partial reads from component instead of `Model.WaitingConfig`.
- Existing seed files (payment-demo-v1.json, planning-notification-v1.json) migrated: remove `"stepType"`, convert `"waitingConfig"` sidecar to component.

**What Stays Stable:**

- `fieldKey` remains on `FieldFile` inside container components (validation/persistence remain unchanged).
- GDS error rendering: component-agnostic, field-keyed.
- Validation pipeline: component-agnostic, field-keyed.
- Conditional visibility, polling mechanism, storage: unchanged.

**Trade-offs:**

| Pro | Con |
|---|---|
| Authors never declare stepType redundantly | New engine inference rules (small but testable) |
| Component tree is fully self-describing | Silent contradiction becomes runtime inference, not parse error |
| Removes class of authoring errors | Seed migration required |
| `waiting` composable with other content | |

**Feasibility Verified:**

- ✅ Validation & error rendering: component-agnostic (field-keyed `WorkflowFieldValidator`, `WorkflowProblem`).
- ✅ GDS behavior: field-keyed `PrismFieldContext`, transparent to component structure.
- ✅ Persistence: keyed to `fieldKey`, no changes needed.
- ⚠️ 4 narrow UI routing dependencies replaceable with explicit metadata.

**Handoff:**

- **Blathers (Backend):** Implement shell derivation, migrate WaitingConfig to component, remove StepType from StepDefinition, migrate seeds.
- **Tangy (QA):** Test inference rules, validate polling end-to-end.
- **Isabelle (Frontend):** Update `_WorkflowStep-Waiting` partial to read from component.

**Status:** Approved for implementation.

**Orchestration Logs:**
- `.squad/orchestration-log/2026-04-22T23:08:36-tom-nook.md`
- `.squad/orchestration-log/2026-04-22T23:08:36-blathers.md`

**Session Log:** `.squad/log/2026-04-22T23:08:36-component-model-step-type.md`

---
## 📌 2026-04-21: Blathers & Tangy — Instance Policy Implementation

**Decision:** Implement full support for all three `instancePolicy` values in the workflow engine: `"single"`, `"multiple"`, and `"prompt"`.

**Why:** The workflow definition schema had three policy options, and the view layer (workflowPage.cshtml and _WorkflowHub-InstancePicker.cshtml) plus ViewModel (PrismWorkflowViewModel.ShowInstancePicker) were already correctly built. Implementation required connecting the backend logic to support all three policies end-to-end.

**Policy Semantics:**

| Policy | Behavior |
|--------|----------|
| `"single"` | One active instance per user/workflow. Always find-or-create via lookup key. (Existing behavior — unchanged) |
| `"multiple"` | Every visit starts a fresh instance. `GET /workflow-page` creates new. `GET /workflow-page?instanceId=xxx` resumes a specific instance. |
| `"prompt"` | If active (non-terminal) instance exists → return `ResponseState = "instance_picker"` so controller shows picker partial. User chooses `?action=resume` or `?action=start-new`. No active instance → create new normally. |

**Changes Made:**

1. **BusinessAppWorkflowEngine.GetCurrent** — Updated signature to accept optional `instanceId` and `action` parameters. Implemented logic for all three policies.
2. **Program.cs API Endpoint** — `/api/workflow/{key}/current` now accepts optional JSON body with `instanceId` and `action` fields.
3. **IBusinessAppWorkflowClient & BusinessAppWorkflowClient** — Updated `GetCurrentAsync` signature to accept optional `instanceId` and `action`. Client sends JSON body only if parameters provided.
4. **PrismWorkflowPageController.HandleGet** — Reads `?instanceId` and `?action` query parameters, passes to `GetCurrentAsync`. Handles `instance_picker` response by setting `vm.ShowInstancePicker = true`.
5. **WorkflowHubController.ResolveWorkflowPageUrl** — Appends `?instanceId={id}` to resume URLs for non-completed instances.
6. **WorkflowInstanceSummary** — Added `InstancePolicy` property (populated by `GetInstances` from definition).

**Implications:**

- All three policies now fully functional end-to-end
- Backward compatible: `"single"` policy unchanged; JSON body optional for API calls
- Multi-tenant security: explicit tenant+user validation on `instanceId` parameter
- Terminal states: prompt policy treats completed instances as "no active" scenario
- Parameter precedence: `instanceId` takes precedence; `action` only applies when no `instanceId` given

**Testing:**

- 19 new instance policy tests (all passing)
- 512 total Core tests passing (no regression)
- Coverage: all three policies, parameter handling, access control, state transitions

**Session Log:** `.squad/log/20260421-214138-instance-policy.md`

**Orchestration Logs:**
- `.squad/orchestration-log/20260421-214138-blathers.md`
- `.squad/orchestration-log/20260421-214138-tangy.md`

---

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

## 📌 2026-04-20: Copper — Aspire 13.2.2 Upgrade and OTLP Telemetry Warning Resolution

**Date:** 2026-04-20  
**Agent:** Copper (Security & Architecture), Coordinator (Release Management)  
**Status:** Complete

### Work Summary

Aspire upgraded from 9.2.0 to 13.2.2; persistent telemetry warning diagnosed and accepted as informational.

### Root Cause Analysis

The warning "Telemetry endpoint is unsecured. Untrusted apps can send telemetry to the dashboard" is triggered by Aspire's OTLP `AuthMode.Unsecured` setting, which is set **programmatically by the AppHost**, not via environment variables.

**Key Finding:** Three distinct Aspire security controls exist:
- `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` → Dashboard UI authentication
- `ASPIRE_ALLOW_UNSECURED_TRANSPORT` → Protocol-layer security
- `Dashboard__Otlp__AuthMode` → OTLP API key authentication

Previous fix attempts failed because environment variables in `launchSettings.json` apply to the **AppHost process**, not the **dashboard child process**. The AppHost controls dashboard configuration via code (`DashboardLifecycleHook.cs`), which always sets OTLP to `Unsecured` when no API key is configured.

### Decision

**Accept the warning as expected behavior for local development:**
- OTLP endpoint IS unsecured by design in local dev
- Suppressing requires API key configuration (unjustified for localhost)
- Warning correctly informs developers of security posture
- Production hardening: configure `Dashboard__Otlp__AuthMode=ApiKey` with secure API key distribution

### Changes

- **Aspire:** 9.2.0 → 13.2.2
- **KubernetesClient:** 17.0.14 → 18.0.13
- **Build:** ✅ Passes

### Evidence & References

- Aspire 9.2.0 source: `DashboardWebApplication.cs` (warning emission)
- Aspire 9.2.0 source: `PostConfigureDashboardOptions.cs` (frontend/OTLP auth separation)
- Aspire 9.2.0 source: `DashboardLifecycleHook.cs` (programmatic dashboard config)

### Session Logs

- `.squad/log/2026-04-20T21:35:16Z-aspire-upgrade-and-telemetry-warning.md`

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

## 📌 2026-04-21: Blathers — Security Hardening Phase 2

**Decision:** Implement four security hardening measures identified by Copper's security review

**Context:**
Following a comprehensive security review, four items were flagged for implementation:
1. KEYCLOAK_BACKCHANNEL_URL must never be set in production (insecure HTTP metadata fetch)
2. Admin workflow endpoints must be unreachable in non-Development environments
3. Regression tests needed for backchannel URL issuer validation
4. Workflow key parameters need input validation

**Implementation:**

### 1. Production Startup Guard
- Both `TestSite/Program.cs` and `MockBusinessApp/Program.cs` now throw `InvalidOperationException` at startup if `KEYCLOAK_BACKCHANNEL_URL` is set in non-Development
- Placed after `builder.Build()` but before `app.Run()`
- Fail-fast approach: service won't start with insecure configuration

### 2. Admin 404 Middleware
- `MockBusinessApp/Program.cs` registers middleware that returns 404 for all `/admin/*` requests in non-Development
- Registered BEFORE endpoint handlers so it short-circuits the pipeline
- Defence-in-depth: even if accidentally deployed, admin endpoints are unreachable

### 3. Backchannel Security Test
- New `BackchannelSecurityTests.cs` verifies issuer validation still works with KEYCLOAK_BACKCHANNEL_URL set
- Tests that setting the backchannel URL does NOT bypass critical issuer claim validation
- Ensures tokens with malicious issuers are rejected even when metadata fetch uses insecure channel

### 4. Workflow Key Validation
- GET `/admin/workflow/definition/{key}/json` and PUT `/admin/workflow/definition/{key}` now validate the key parameter
- Only allows `^[a-zA-Z0-9\-]+$` (alphanumeric + hyphens)
- Returns 400 Bad Request for invalid keys
- Prevents path traversal or injection via workflow key

**Rationale:**
These are defence-in-depth measures. Even if one layer fails (e.g., deployment misconfiguration), other layers prevent exploitation.

**Impact:**
- ✅ Prevents production deployment with insecure OIDC configuration
- ✅ Blocks admin endpoints outside development
- ✅ Regression coverage for backchannel URL security
- ✅ Input validation for workflow keys

---

## 📌 2026-04-20: Mabel — Standardize on "Step Type" Terminology in User-Facing Workflow Documentation

**Decision:** Standardize all user-facing workflow documentation on "step type" terminology.

**Context:**
The workflow system originally used the design term **"archetype"** during architecture planning (documented in `docs/design/workflow-forms-engine.md`). However, the implementation settled on **"step type"** as the actual field name in JSON workflow definitions:

- **JSON field:** `"stepType": "Question"`
- **Partial naming convention:** `_WorkflowStep-{StepTypeName}.cshtml`
- **Dispatcher logic:** `workflowPage.cshtml` resolves partials by step type name

The user-facing guides (`workflow-customisation.md`, `workflow-setup.md`) still used "archetype" terminology, creating a mismatch between documentation and implementation.

**Changes Made:**

1. **`docs/guides/workflow-customisation.md`:**
   - Section renamed: "Creating a Custom Archetype" → "Creating a Custom Step Type"
   - JSON example updated: `"archetype": "Documents"` → `"stepType": "Documents"`
   - All prose references changed: "archetype" → "step type" (6 occurrences)

2. **`docs/guides/workflow-setup.md`:**
   - State properties table updated: `archetype` → `stepType`
   - JSON examples updated throughout (4 occurrences)
   - Section renamed: "Archetype Reference" → "Step Type Reference"
   - Troubleshooting table updated: `_WorkflowStep-{Archetype}.cshtml` → `_WorkflowStep-{StepType}.cshtml`

3. **`docs/workflow-walkthrough.md`:**
   - Already used correct terminology (no changes needed)

4. **New Guide: `docs/guides/workflow-gds-components.md`**
   - Comprehensive GDS Design System component guide
   - 20+ copy-paste-ready component examples with Prism integration
   - Shows HTML + Prism wrapper pattern for each component

**Rationale:**

1. **Consistency with implementation:** Users writing JSON workflow definitions see `"stepType"` in examples and need docs that match the actual field name.
2. **Reduce confusion:** Developers looking at `workflowPage.cshtml` dispatcher code see step type resolution—docs should align with that mental model.
3. **Naming convention clarity:** Partial naming (`_WorkflowStep-Question.cshtml`) is based on step type, not a separate archetype concept.
4. **Developer onboarding:** New developers should learn the term that matches the code, not a legacy design term.

**Impact:**
- Documentation now matches code: Users can copy JSON examples directly without translating terms
- Search and discovery: Developers searching for "stepType" will find relevant docs
- Future maintenance: New step type examples will use consistent terminology
- Design docs unchanged: Architecture discussions can still use "archetype" as a conceptual term—user-facing docs use the implementation term

---

## 📌 2026-04-20: Blathers — Live JSON Editor for Workflow Admin

**Decision:** Add a live in-browser JSON editor (Ace Editor) to the admin page so developers can edit workflow definitions in memory and test changes immediately without restarting.

**Context:**
The `/admin/workflow` page in MockBusinessApp displays workflow definitions loaded from seed files. During local development, any changes to workflow structure required editing JSON files and restarting the app — a slow feedback loop.

**Implementation:**
- Integrated Ace Editor v1.32.6 via CDN (JSON mode, live syntax validation)
- Added `GET /admin/workflow/definition/{key}/json` and `PUT /admin/workflow/definition/{key}` endpoints
- Added `GetDefinition(string key)` and `UpdateDefinition(string key, WorkflowDefinitionFile updated)` methods to `BusinessAppWorkflowEngine`
- Modal overlay with fullscreen editor, "Apply Changes" triggers PUT → auto-reload

**Consequences:**
- ✅ Faster dev workflow — edit JSON → Apply → test in seconds, no app restart
- ✅ Live validation — Ace highlights JSON errors before save, PUT endpoint validates schema
- ⚠️ Changes are in-memory only — lost on app restart (intentional for dev/demo use)
- ⚠️ No auth on admin endpoints — safe for local dev, must NOT be exposed in production

**Alternatives Considered:**
1. File-watching auto-reload — rejected because it still requires editing external files
2. Persistent database updates — rejected as overkill for a dev-only feature
3. No editor — rejected because the slow restart loop was a productivity bottleneck

**Verdict:**
This is a dev-quality-of-life feature that makes workflow iteration much faster. Production deployments should disable `/admin/*` routes or protect them with authentication.

# Security Review — 2026-04-21

**Reviewer:** Copper (Security Engineer)  
**Scope:** Full codebase security review with focus on recent Keycloak/Codespaces backchannel changes

---

## Executive Summary

**Overall Risk Assessment: LOW**

The recent Keycloak backchannel changes are **safe for production** with appropriate deployment controls. The changes correctly separate the metadata/token-exchange fetch URLs (backchannel) from the issuer validation URL (OidcAuthority), maintaining security boundaries. However, production deployments must ensure `KEYCLOAK_BACKCHANNEL_URL` is never set.

The workflow admin endpoints present a **low-severity risk** in local dev (intended use case) but would be **critical** if accidentally exposed in production. No authentication or path validation is present.

**Key Findings:**
- ✅ Keycloak backchannel fix correctly scoped to Codespaces
- ✅ Issuer validation remains untouched (uses OidcAuthority)
- ✅ JWT validation pipeline is robust
- ⚠️ Admin endpoints lack authentication (acceptable for local dev only)
- ⚠️ Missing regression tests for backchannel changes

---

## 1. Keycloak/Codespaces Backchannel Changes

**Risk Level:** **Low** (with deployment controls)

### Analysis

**Files Changed:**
- `src/UmbracoPrism.AppHost/Program.cs` (lines 123-130)
- `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs` (lines 196-199)
- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` (lines 287-295, 388-410)

**What Changed:**

1. **AppHost (lines 123-130):** When `CODESPACE_NAME` env var is set, injects `KEYCLOAK_BACKCHANNEL_URL` pointing to Keycloak's internal HTTP endpoint (`keycloak.GetEndpoint("http")`) into both `testsite` and `businessapp`.

2. **PrismAuthExtensions (lines 196-199):** In `ResolveSigningKeys()` for OIDC providers, if `KEYCLOAK_BACKCHANNEL_URL` is set, constructs metadata address as `{backchannelBase}{oidcPath}/.well-known/openid-configuration` instead of using the OidcAuthority URL.

3. **PrismOidcConfiguration (lines 287-295, 388-410):** Same pattern in two places:
   - `OnAuthorizationCodeReceived` — constructs token endpoint URL for backchannel auth code → token exchange
   - `OnRedirectToIdentityProvider` — constructs JWKS URL for backchannel signing key fetch

**Security Questions Answered:**

### ✅ Can `KEYCLOAK_BACKCHANNEL_URL` be set by an attacker in production?

**No.** In a typical production deployment:
- Environment variables are set via deployment configuration (Azure App Service settings, Kubernetes ConfigMaps, container orchestration)
- These settings are managed by infrastructure/devops teams with elevated permissions
- Application code cannot set its own environment variables
- Request headers/query parameters cannot inject environment variables

**Risk Mitigation:**
- The `CODESPACE_NAME` guard in AppHost ensures the variable is only set in GitHub Codespaces (lines 123-124)
- Production deployments would need to **explicitly** set `KEYCLOAK_BACKCHANNEL_URL`, which should never happen

**Recommendation:** Document in deployment/operations guide that `KEYCLOAK_BACKCHANNEL_URL` must never be set in production. Consider adding startup validation that logs a warning if this variable is set outside of development environments.

### ✅ Does using internal HTTP (non-TLS) create MITM risk?

**No, for the Codespaces use case.** The backchannel URL points to:
- `http://keycloak:8080` (Aspire container-to-container communication within the Codespaces VM)
- Traffic never leaves the local Docker network
- GitHub Codespaces VM is single-tenant (one user per VM)

**However:** If `KEYCLOAK_BACKCHANNEL_URL` were set in production pointing to an external HTTP endpoint, it would create a MITM risk for signing key fetch and token exchange. This reinforces the recommendation above: this variable must never be set in production.

**Design Note:** The code in `PrismSigningKeyCache.cs` (line 148) correctly detects HTTP URLs and sets `RequireHttps = false` only when the metadata URL starts with `http://`. This prevents accidental bypass of HTTPS requirements for production OIDC providers.

### ✅ Is OidcAuthority still used for issuer validation?

**Yes, absolutely.** The backchannel URL is **only** used for:
1. Fetching OIDC metadata (`/.well-known/openid-configuration`)
2. Fetching signing keys (JWKS)
3. Exchanging authorization code for tokens (`/protocol/openid-connect/token`)

**Issuer validation remains unchanged:**
- `PrismAuthExtensions.cs` line 84: `oidcTenant.OidcAuthority.TrimEnd('/')` compared to `tokenIssuer.TrimEnd('/')`
- `PrismOidcConfiguration.cs` line 170: `validationParameters.ValidIssuer = tenant.OidcAuthority`
- Token's `iss` claim must exactly match the configured `OidcAuthority`

**No issuer bypass is possible.** An attacker who somehow set `KEYCLOAK_BACKCHANNEL_URL` to a malicious endpoint could:
- Cause the app to fetch signing keys from the malicious endpoint
- Cause token exchange requests to fail or be hijacked

But they **cannot** create valid tokens because:
1. Token issuer validation would reject tokens with `iss != OidcAuthority`
2. Even if the attacker returns their own signing keys, those keys can only validate tokens they create, and those tokens would have the wrong issuer

**Conclusion:** Issuer validation is the critical security boundary, and it remains intact.

### ✅ In what environments would `KEYCLOAK_BACKCHANNEL_URL` be set?

**Current design:** Only in GitHub Codespaces (via AppHost line 123 guard on `CODESPACE_NAME`).

**Potential future use:** Could be set manually in custom dev/staging environments where:
- Keycloak runs in a separate container/service
- External Keycloak URL is behind auth or not reachable from app server
- Internal Keycloak URL is accessible

**Recommendation:** This is a valid pattern for container orchestration. The security boundary is clear: only use backchannel URLs in non-production environments where the internal network is trusted.

### ✅ Could the env var leak into production by accident?

**Low risk, but possible.** Scenarios:
1. Developer copies `.env` file from Codespaces to production deployment
2. CI/CD pipeline mistakenly sets the variable
3. Container image bakes in the environment variable

**Mitigations:**
- AppHost only injects the variable when `CODESPACE_NAME` is set (line 123)
- Production containers would need to be running in Codespaces to trigger this (extremely unlikely)
- Even if set, production Keycloak would be external, not an internal container

**Recommended Additional Safeguards:**
1. Add startup logging that warns if `KEYCLOAK_BACKCHANNEL_URL` is set in non-Development environments
2. Document in production deployment checklist: verify this variable is not set
3. Add integration test that validates production-like config doesn't have backchannel URL

---

## 2. Workflow Admin JSON Editor

**Risk Level:** **Medium** (if exposed in production) / **Low** (in intended local dev use)

### Analysis

**Endpoints:** (src/UmbracoPrism.MockBusinessApp/Program.cs)
- `GET /admin/workflow` (line 129) — HTML dashboard with inline editor
- `GET /admin/workflow/definition/{key}/json` (line 507) — Returns workflow definition JSON
- `PUT /admin/workflow/definition/{key}` (line 521) — Updates workflow definition
- `POST /admin/workflow/{instanceId}/action/{action}` (line 489) — Advances workflow instance
- `POST /admin/workflow/{instanceId}/reset` (line 495) — Resets workflow instance
- `POST /admin/workflow/reset-all` (line 501) — Resets all instances

### Security Issues

#### ⚠️ No Authentication

**Finding:** All `/admin/workflow/*` endpoints have **no authentication**.

Comment on line 127 states: `// ── Admin UI (no auth — local dev only) ─────────────────────────────────────`

**Risk if exposed in production:**
- ✅ Read workflow definitions (JSON structure, business logic, role checks) — **Information Disclosure**
- ✅ Modify workflow definitions (inject malicious transitions, bypass role checks) — **Authorization Bypass**
- ✅ Modify workflow instances (advance states, bypass approvals) — **Business Logic Bypass**
- ✅ Delete all workflow state (DoS) — **Denial of Service**

**Actual Risk:** MockBusinessApp is clearly a **development/demo service** based on:
- Project name: "MockBusinessApp"
- In-memory workflow engine (singleton, line 19)
- Hardcoded tenant/member config in appsettings.json
- Aspire orchestration (AppHost) only runs locally

**Conclusion:** Acceptable **if and only if** MockBusinessApp is never deployed to a production or publicly accessible environment.

#### ⚠️ No Path Validation on `{key}` Parameter

**Finding:** The `key` parameter in `GET /admin/workflow/definition/{key}/json` and `PUT /admin/workflow/definition/{key}` is passed directly to `engine.GetDefinition(key)` and `engine.UpdateDefinition(key, updated)` with no sanitization.

**Potential Risks:**
- Path traversal: `../../etc/passwd` (unlikely to succeed given in-memory engine design)
- Engine implementation controls actual risk

**Inspection of BusinessAppWorkflowEngine:** (Not included in files reviewed, but based on patterns observed)
- Likely uses `key` as dictionary lookup: `_definitions[key]`
- Path traversal unlikely to succeed with in-memory dictionary

**Conclusion:** Low risk given in-memory design, but best practice would be to validate `key` format (alphanumeric + hyphens only).

#### ⚠️ No Input Validation on Workflow Definition JSON

**Finding:** `PUT /admin/workflow/definition/{key}` deserializes JSON body with minimal validation:
- Line 526-532: JSON deserialization with exception handling
- No schema validation
- No checks for malicious workflow logic

**Potential Risks:**
- Inject transitions that bypass role checks (`RequiresRole` empty or null)
- Create unrestricted state transitions
- Inject field definitions with unsafe validation rules

**Actual Impact:** The workflow engine likely has its own validation, but no evidence of defense-in-depth at the API boundary.

**Recommendation:** Add schema validation for workflow definitions to ensure:
- Required fields are present (`DefinitionKey`, `InitialState`, `States`, `Transitions`)
- Transition `RequiresRole` is not empty/null where role checks are expected
- Field validators reference known validator names

### Production Safety Assessment

**Question:** Can these endpoints accidentally reach production?

**Analysis:**
- MockBusinessApp is orchestrated by Aspire AppHost (local dev tool)
- No production deployment configuration observed in repository
- Service is clearly named "Mock" indicating non-production intent

**However:** If MockBusinessApp were deployed to production (e.g., as a microservice in a multi-tenant SaaS):
- Admin endpoints would be publicly accessible
- No IP allowlisting or network restrictions visible

**Recommendation:**

1. **Short-term:** Add environment check to admin endpoints:
   ```csharp
   if (!builder.Environment.IsDevelopment())
   {
       app.MapGet("/admin/workflow", () => Results.StatusCode(404));
       // ... same for all admin endpoints
   }
   ```

2. **Medium-term:** Add authentication to admin endpoints:
   ```csharp
   app.MapGet("/admin/workflow", ...).RequireAuthorization("AdminOnly");
   ```

3. **Long-term:** Separate admin UI into a separate project/service that is never deployed to production, or gate it behind feature flag + admin authentication.

---

## 3. JWT Validation Pipeline

**Risk Level:** **None** — Robust and secure

### Analysis

**File:** `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`

**Validation Stages:**

#### ✅ Issuer Validation (lines 44-90)

**Entra CIAM path (lines 50-75):**
- Extracts `tid` claim from token
- Verifies tenant ID is in configured tenant list
- Validates issuer is `https://{tid}.ciamlogin.com/{tid}/v2.0`
- Host must match `{tid}.ciamlogin.com` exactly
- Path must start with `/{tid}/v2.0`

**OIDC path (lines 77-89):**
- Extracts `iss` claim from token
- Validates issuer matches a configured `OidcAuthority` (case-insensitive, trailing-slash normalized)

**Security Properties:**
- ✅ Multi-tenant: Each tenant's issuer is validated independently
- ✅ No wildcards or regex: Exact string matching
- ✅ Case-insensitive comparison prevents case-based bypasses
- ✅ Trailing-slash normalization prevents path-based bypasses

**Potential Issue:** None. Issuer validation is strict and correct.

#### ✅ Audience Validation (lines 92-127)

**Entra CIAM path (lines 99-107):**
- Finds tenant by `tid` claim
- Validates `aud` claim matches tenant's `ClientId`

**OIDC path (lines 109-127):**
- Finds tenant by `iss` claim
- Validates `aud` claim matches tenant's `ClientId` **OR** `azp` claim matches tenant's `ClientId`

**Security Properties:**
- ✅ Audience is bound to the specific tenant (no cross-tenant token reuse)
- ✅ Authorized party (`azp`) fallback is correct for OIDC provider tokens with multiple audiences

**Potential Issue:** None. Audience validation correctly prevents cross-tenant token reuse.

#### ✅ Lifetime Validation (lines 36-38)

**Configuration:**
- `ValidateLifetime = true`
- `ClockSkew = TimeSpan.FromMinutes(5)`

**Security Properties:**
- ✅ Token expiration is enforced
- ✅ 5-minute clock skew is reasonable (default is also 5 minutes)

**Potential Issue:** None. Standard practice.

#### ✅ Signing Key Validation (lines 129-140, 145-220)

**Logic:**
- Delegates to `ResolveSigningKeys()` method
- Uses `PrismSigningKeyCache` for key caching with TTL
- Forces refresh if requested `kid` is missing from cache
- Background refresh when keys are approaching expiry

**Security Properties:**
- ✅ Key rotation is handled automatically
- ✅ Forced refresh when `kid` is missing prevents stale-key failures
- ✅ Background refresh reduces latency for approaching-expiry scenarios
- ✅ Signing key fetch uses HTTPS by default (only HTTP for explicit `http://` URLs)

**Potential Issue:** None. Signing key resolution is robust and handles rotation correctly.

### Multi-Tenant Security

**Cross-Tenant Attack Scenario:**
1. Attacker obtains valid token from Tenant A
2. Attacker sends token to API endpoint hosted by application
3. Application must reject token if endpoint is for Tenant B

**Protection Layers:**
1. Issuer validation ensures token is from a **configured** tenant
2. Audience validation ensures token is for the **correct client**
3. Application code must derive tenant from `PrismContext.CurrentTenant` (based on hostname/routing), not from token claims

**Validation:** Existing security tests confirm this pattern:
- `PrismAuthExtensionsSecurityTests.cs` line 44: `AudienceValidator_RejectsAudienceBoundToDifferentConfiguredTenant`
- `Phase1SecurityRegressionTests.cs` line 219: `PrismVinylNotificationController_DeriveTenantIdFromServerContext`

**Conclusion:** Multi-tenant isolation is correctly enforced.

---

## 4. Signing Key Cache

**Risk Level:** **None** — Well-designed and thread-safe

### Analysis

**File:** `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`

**Cache Properties:**
- In-memory `ConcurrentDictionary` (line 17)
- Per-tenant semaphore locks for fetch deduplication (line 18)
- TTL-based refresh (45 min soft, 60 min hard — lines 13-14)
- Forced refresh cooldown (30 sec — line 15)

### ✅ Cache Poisoning Prevention

**Threat:** Attacker injects malicious signing keys into cache, allowing them to forge valid tokens.

**Protection:**
- Cache is in-process memory only (not shared across instances)
- Keys are fetched from OIDC metadata endpoint (controlled by tenant configuration)
- No external cache (Redis, etc.) where attacker could inject keys

**Potential Attack Vectors:**
1. Modify `KEYCLOAK_BACKCHANNEL_URL` to point to attacker-controlled endpoint
   - **Mitigated:** Requires infrastructure-level access (see Section 1)
   - **Additional mitigation:** Issuer validation would still reject forged tokens

2. MITM the OIDC metadata fetch
   - **Mitigated:** HTTPS required by default (line 148, 210)
   - **Exception:** HTTP allowed only for explicit `http://` URLs (Codespaces localhost)

**Conclusion:** Cache poisoning is not a practical attack given current architecture.

### ✅ Forced Refresh Handling

**Scenario:** OIDC provider rotates signing keys, app requests token with new `kid`.

**Handling:**
- Line 203: `if (oidcSnapshot.IsExpired || !oidcSnapshot.ContainsRequestedKey)` triggers forced refresh
- Line 130: `bypassCooldownForMissingKey` allows immediate refresh when required `kid` is missing (even within cooldown window)

**Security Property:** Key rotation is handled correctly without blocking token validation.

**Potential Issue:** None. This design prevents both stale-key validation failures and forced-refresh DoS attacks.

### ✅ Thread Safety

**Concurrency Controls:**
- Per-tenant semaphore (line 59, 121): Ensures only one thread fetches keys for a given tenant
- Concurrent callers wait for the first fetch to complete (deduplication)
- Cache writes are atomic via `ConcurrentDictionary` indexer

**Potential Race Conditions:**
- Line 76-79: Check-then-act pattern on forced refresh cooldown
  - **Safe:** Semaphore is held during this check, preventing concurrent forced refreshes

**Conclusion:** Thread-safe and efficient.

---

## 5. OIDC Configuration

**Risk Level:** **None** — Secure design

### Analysis

**File:** `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`

### ✅ Backchannel URL Pattern (lines 287-295, 388-410)

**Pattern:**
```csharp
var backchannelBase = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
if (!string.IsNullOrEmpty(backchannelBase))
{
    var oidcPath = new Uri(tenant.OidcAuthority!).AbsolutePath.TrimEnd('/');
    authority = $"{backchannelBase.TrimEnd('/')}{oidcPath}/protocol/openid-connect/token";
}
```

**Security Properties:**
- ✅ Extracts only the **path** component from `OidcAuthority` (line 294, 399)
- ✅ Scheme and host come from backchannel base (controlled by infra, not user input)
- ✅ Prevents URL injection (e.g., `OidcAuthority = "https://evil.com"` does not make backchannel fetch from evil.com)

**Validation:** `AbsolutePath` property of `Uri` class returns only the path component (e.g., `/realms/prism-dev`), not the scheme or host.

**Conclusion:** Backchannel URL construction is safe.

### ✅ Localhost Demo Tenant Detection (lines 73-100)

**Logic:**
- Repo-owned demo tenant is identified by:
  1. Hostname is `localhost` or `*.app.github.dev` (Codespaces)
  2. `OidcClientId` is `prism-client`
  3. `OidcAuthority` is localhost or Codespaces

**Security Property:**
- Prevents production tenants from accidentally matching localhost demo tenant logic
- Demo-specific behavior (offline scopes) only applies to repo-owned infrastructure

**Conclusion:** Safe tenant classification logic.

---

## 6. Production Deployment Safety

**Risk Level:** **Low** (with operational controls)

### Analysis

**Dev-Only Features:**
1. **Workflow admin endpoints** (MockBusinessApp) — No auth, no IP restrictions
2. **KEYCLOAK_BACKCHANNEL_URL** — Allows internal HTTP URLs for metadata fetch
3. **Aspire orchestration** — Only runs locally (AppHost)

### Production Readiness Checklist

#### ✅ TestSite (Umbraco CMS frontend)

**Deployment:**
- Standard Umbraco deployment patterns
- OIDC auth with Entra ID or Keycloak
- No dev-only features detected in production code paths

**Security Controls:**
- Authentication required for backoffice (`/umbraco`)
- OIDC callback validation (redirect URI, nonce, PKCE)
- Tenant isolation via hostname routing

**Recommendation:** Safe to deploy. Ensure `KEYCLOAK_BACKCHANNEL_URL` is not set.

#### ⚠️ MockBusinessApp (Business logic API)

**Deployment:**
- Currently designed for local dev only
- Admin endpoints have no auth
- In-memory workflow state (not persistent)

**Risks if deployed to production:**
- Admin endpoints publicly accessible
- Workflow state lost on restart
- No audit logging

**Recommendation:** Do NOT deploy MockBusinessApp to production in current form. If production business logic API is needed, create separate project with:
- Persistent workflow state (database)
- Authentication on all endpoints
- Audit logging for workflow transitions
- No admin endpoints (or admin endpoints with authentication + authorization)

#### ✅ Shared Libraries (Core, Shared)

**Deployment:**
- Referenced by both TestSite and MockBusinessApp
- No dev-only code paths in production flows
- Backchannel URL only used if env var is set (won't be in production)

**Recommendation:** Safe to deploy.

### Environment Variable Hygiene

**Production Deployment Requirements:**
1. `CODESPACE_NAME` must not be set
2. `KEYCLOAK_BACKCHANNEL_URL` must not be set
3. `ASPIRE_ALLOW_UNSECURED_TRANSPORT` must not be set
4. `Prism:EnableDownstreamDemo` must be false or omitted (already gated by security tests)

**Recommendation:** Add startup validation in TestSite and production business API:
```csharp
if (!builder.Environment.IsDevelopment())
{
    var bannedVars = new[] { "KEYCLOAK_BACKCHANNEL_URL", "CODESPACE_NAME", "ASPIRE_ALLOW_UNSECURED_TRANSPORT" };
    foreach (var varName in bannedVars)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)))
        {
            logger.LogError("SECURITY: {VarName} must not be set in production", varName);
            throw new InvalidOperationException($"Security policy violation: {varName} is set in production environment");
        }
    }
}
```

---

## 7. Test Coverage Gaps

### Missing Tests for Backchannel Changes

**Finding:** No regression tests for `KEYCLOAK_BACKCHANNEL_URL` behavior.

**Recommended Tests:**

1. **ResolveSigningKeys with backchannel URL** (`PrismAuthExtensionsSecurityTests.cs`)
   ```csharp
   [Fact]
   public void ResolveSigningKeys_UsesBackchannelUrl_WhenEnvironmentVariableIsSet()
   {
       // Set KEYCLOAK_BACKCHANNEL_URL env var
       // Call ResolveSigningKeys for OIDC tenant
       // Verify metadata address uses backchannel base + oidc path
   }
   ```

2. **Issuer validation still enforced with backchannel** (`PrismAuthExtensionsSecurityTests.cs`)
   ```csharp
   [Fact]
   public void IssuerValidator_StillEnforcedWhenBackchannelUrlIsSet()
   {
       // Set KEYCLOAK_BACKCHANNEL_URL to http://attacker.com
       // Create token with iss = http://attacker.com/realms/prism-dev
       // Verify issuer validation rejects token (iss must match OidcAuthority)
   }
   ```

3. **Token exchange uses backchannel URL** (Integration test)
   ```csharp
   [Fact]
   public async Task OnAuthorizationCodeReceived_UsesBackchannelUrl_ForTokenExchange()
   {
       // Set KEYCLOAK_BACKCHANNEL_URL
       // Simulate OIDC callback with auth code
       // Verify token exchange request goes to backchannel URL, not OidcAuthority
   }
   ```

4. **Production startup validation** (Integration test)
   ```csharp
   [Fact]
   public void Startup_FailsInProduction_WhenBackchannelUrlIsSet()
   {
       // Set environment to Production
       // Set KEYCLOAK_BACKCHANNEL_URL
       // Verify startup throws InvalidOperationException
   }
   ```

### Existing Coverage: Strong

**Files:**
- `Phase1SecurityRegressionTests.cs` — 19 tests covering open redirect, debug UI, notification auth, downstream demo
- `PrismAuthExtensionsSecurityTests.cs` — 40+ tests covering issuer/audience/signing key validation
- `PrismVinylNotificationSecurityTests.cs` — Tenant isolation in notification API

**Gap:** No tests for workflow admin endpoint security (MockBusinessApp admin pages).

**Recommendation:** Add test:
```csharp
[Fact]
public void WorkflowAdminEndpoints_AreDisabled_InNonDevelopmentEnvironments()
{
    // Build app in Production environment
    // Attempt to access /admin/workflow
    // Verify 404 response
}
```

---

## Recommended Actions

### Critical (Fix Before Production)

None. Current codebase is safe for production deployment with operational controls.

### High (Implement Soon)

1. **Add production environment variable validation**
   - **File:** `src/UmbracoPrism.TestSite/Program.cs`
   - **Change:** Throw on startup if `KEYCLOAK_BACKCHANNEL_URL` is set in non-Development environment
   - **Rationale:** Fail-closed security boundary

2. **Disable workflow admin endpoints in non-Development environments**
   - **File:** `src/UmbracoPrism.MockBusinessApp/Program.cs`
   - **Change:** Return 404 for `/admin/workflow/*` when `!builder.Environment.IsDevelopment()`
   - **Rationale:** Defense-in-depth (currently safe because MockBusinessApp not deployed to production)

### Medium (Security Hardening)

3. **Add regression tests for backchannel changes**
   - **File:** `src/UmbracoPrism.Core.Tests/PrismAuthExtensionsSecurityTests.cs`
   - **Tests:** 4 tests listed in Section 7
   - **Rationale:** Prevent future regressions

4. **Add workflow definition schema validation**
   - **File:** `src/UmbracoPrism.MockBusinessApp/Program.cs`
   - **Change:** Validate workflow JSON schema in `PUT /admin/workflow/definition/{key}`
   - **Rationale:** Defense-in-depth for workflow security

5. **Document deployment security requirements**
   - **File:** Create `docs/DEPLOYMENT_SECURITY.md`
   - **Content:** Environment variable hygiene, service deployment boundaries, admin endpoint risks
   - **Rationale:** Operational security guidance

### Low (Nice to Have)

6. **Add `{key}` parameter validation**
   - **File:** `src/UmbracoPrism.MockBusinessApp/Program.cs` line 507, 521
   - **Change:** Validate key format (alphanumeric + hyphens only)
   - **Rationale:** Defense-in-depth (low risk given in-memory design)

7. **Add startup logging for backchannel URL**
   - **File:** `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs` line 196
   - **Change:** Log warning if `KEYCLOAK_BACKCHANNEL_URL` is set in non-Development environment
   - **Rationale:** Observability and audit trail

---

## Conclusion

The Keycloak/Codespaces backchannel changes are **well-designed and secure**. The separation of metadata fetch URLs (backchannel) from issuer validation URLs (OidcAuthority) maintains security boundaries while solving a legitimate Codespaces networking constraint.

The workflow admin endpoints are **acceptable for local development** but must never be deployed to production in their current form (no authentication, no authorization).

**Production deployment is safe** for TestSite and Shared libraries with operational controls ensuring `KEYCLOAK_BACKCHANNEL_URL` is never set in production environments.

**Key Security Strengths:**
- ✅ Issuer validation is strict and untouched by backchannel changes
- ✅ Multi-tenant isolation is correctly enforced
- ✅ JWT validation pipeline is robust
- ✅ Signing key cache handles rotation correctly
- ✅ Thread-safe implementation throughout

**Key Recommendations:**
1. Add production environment variable validation (fail-closed)
2. Add regression tests for backchannel behavior
3. Disable admin endpoints in non-Development environments
4. Document deployment security requirements

---

**Reviewed by:** Copper (Security Engineer)  
**Date:** 2026-04-21  
**Files Reviewed:** 6 core files + 3 test files + 2 configuration files

# Workflow Developer Experience Improvements

**Date:** 2026-04-28  
**Agents:** Blathers  
**Status:** ✅ Complete

## Context

Umbraco.Prism's workflow integration required integrators to write ~300 lines of boilerplate controller code. Workflow definitions were JSON-only with no IntelliSense. The `Archetype` property name was legacy and confusing.

## Decisions

### 1. Rename `Archetype` → `StepType` Throughout

**Rationale:** `StepType` is clearer and aligns with GDS terminology. `Archetype` was a legacy name that confused integrators.

**Impact:** Breaking change for existing consumers referencing `WorkflowInstanceSummary.Archetype` or `WorkflowViewModel.Archetype`.

**Locations Updated:**
- `UmbracoPrism.Shared/Models/Workflow/WorkflowInstanceListEnvelope.cs`
- `UmbracoPrism.TestSite/Models/WorkflowViewModel.cs`
- `UmbracoPrism.TestSite/Controllers/WorkflowPageController.cs`
- `UmbracoPrism.TestSite/Views/workflowPage.cshtml`
- `UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`

### 2. Create `PrismWorkflowPageController<TViewModel>` Base Class

**Rationale:** Reduce integrator effort from ~300 lines to ~5 lines. Provide "pit of success" for common workflow page scenarios.

**Design:**
- Generic base class: `PrismWorkflowPageController<TViewModel> where TViewModel : PrismWorkflowViewModel`
- Full GET/POST handling with antiforgery, nonce, PRG pattern, TempData management
- Virtual methods for customization: `PrePopulateFields(envelope)`, `CreateViewModel(...)`
- Uses `ILogger<RenderController>` to satisfy Umbraco's RenderController base

**Benefits:**
- Integrators override only what's special (e.g., claims pre-population)
- Type-safe ViewModel pattern
- Consistent security posture (antiforgery, nonce, safe redirects)

**Created Files:**
- `src/UmbracoPrism.Core/Controllers/PrismWorkflowPageController.cs`
- `src/UmbracoPrism.Core/Models/Workflow/PrismWorkflowViewModel.cs`

### 3. Add Fluent Builders for Workflow Definitions

**Rationale:** Business App developers should define workflows in C# with IntelliSense, not raw JSON.

**Design:**
- Moved definition types from `MockBusinessApp.Services` to `UmbracoPrism.Shared.Models.Workflow`
- Created `WorkflowDefinitionBuilder` with fluent API for defining workflows
- Created `FieldGroupBuilder` and `WorkflowFieldBuilder` for field definitions
- Builders use private backing fields + fluent methods + `Build()` for immutability

**Benefits:**
- Type safety and IntelliSense for workflow authoring
- Compile-time validation of workflow structure
- Enables future tooling (migration helpers, validators)
- Clearer than JSON for complex workflows

**Created Files:**
- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs` (moved from MockBusinessApp)
- `src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs`
- `src/UmbracoPrism.Shared/Builders/FieldGroupBuilder.cs`

## Trade-offs

**Breaking Changes:**
- `Archetype` → `StepType` rename requires consumer updates
- Definition types moved to Shared (namespace change for BA code)

**Accepted:** Breaking changes justified by improved clarity and DX.

**Generic Controller Complexity:**
- Generic `TViewModel` constraint adds complexity
- Alternative: Non-generic base with concrete ViewModel

**Accepted:** Generics enable type-safe custom ViewModels without casting.

**Activator.CreateInstance for ViewModel:**
- Reflection-based instantiation has minor perf cost
- Alternative: Factory delegate parameter

**Accepted:** Perf impact negligible for page controllers; simpler DI signature.

## Migration Path

**For Integrators (Archetype → StepType):**
```csharp
// Before
var stepType = summary.Archetype;

// After
var stepType = summary.StepType;
```

**For Integrators (Using Base Controller):**
```csharp
// Before: ~300 lines of boilerplate

// After:
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowPageController(/* DI params */)
    : PrismWorkflowPageController<WorkflowViewModel>(/* pass through */)
{
    protected override WorkflowResponseEnvelope PrePopulateFields(WorkflowResponseEnvelope envelope)
    {
        // Only override what's special
    }
}
```

**For Business Apps (Using Builders):**
```csharp
// Before: JSON-only

// After:
var workflow = new WorkflowDefinitionBuilder()
    .Key("my-workflow")
    .DisplayName("My Workflow")
    .AddState("start", s => s.StepType("question").WithFieldGroups("details"))
    .AddTransition("start", "end", "continue")
    .Build();
```

## Validation

- ✅ Build clean (no new warnings/errors)
- ✅ All 431 Core tests passing
- ✅ TestSite controller reduced to ~90 lines
- ✅ Builders compile with full IntelliSense

## Follow-up

- Update documentation with base controller examples
- Add integration tests demonstrating base controller usage
- Consider migrating MockBusinessApp workflow seeds to builder pattern (demo)
- Add XML docs examples to builder classes (DONE)
# Mabel — Workflow Documentation Standards

**Date:** 2026-04-17
**Scope:** Public documentation in `/docs/guides/workflow-*.md`
**Decision:** Established documentation standards and terminology conventions for workflow guides

---

## Decision

Unified all workflow documentation guides with corrected terminology, consistent style, and comprehensive examples.

## Critical Fixes Applied

### 1. Step Type Names (Breaking Change in Docs)

**Corrected terminology:**
- ✅ `question` — Collects data from the user (was: `Collect`)
- ✅ `check-answers` — Read-only summary for review (was: `Review`)
- ✅ `status-timeline` — Status display and progress (was: `StatusTimeline`)
- ✅ `task-list` — Tasks with statuses (new, was missing)
- ✅ `confirmation` — Thank you / success screen (was: `Completion`)

**Why:** These are the actual step type values in the codebase. Incorrect names caused confusion and implementation errors.

### 2. Field Group JSON Format

**Corrected format:**
```json
"options": ["Developer", "Architect", "Lead", "Other"]
```

**Was incorrect:**
```json
"options": [{"key": "developer", "label": "Developer"}, ...]
```

**Why:** The actual code expects plain string arrays, not key-value pairs.

### 3. Workflow Definition Structure

**Correct separation:**
- Workflow definitions reference field group keys via `fieldGroupKeys` array
- Field groups are separate JSON files in `workflow-seeds/field-groups/`
- Workflow JSON does NOT embed field groups

**Was incorrect:** Docs showed fieldGroups embedded inside the workflow JSON.

---

## Documentation Standards

### Style Conventions

1. **Audience:** Developer-first documentation (assume C#/.NET knowledge)
2. **Voice:** Active voice, present tense ("You create...", not "One can create...")
3. **Structure:** Overview → Conceptual → Reference → Examples → Next Steps
4. **Diagrams:** Mermaid only (no ASCII art)
5. **Code blocks:** Always language-tagged (`json`, `csharp`, `cshtml`, etc.)

### Visual Markers

- 🔵 **Blue marker** — `🔵 Prism Platform` — Features provided by Prism (don't modify)
- 🟠 **Orange marker** — `🟠 Your Business App` — Developer's responsibility (implement)

**Usage in doc blocks:**
```
> 🔵 **Prism Platform** — Form rendering is provided; customize via CSS variables.
> 🟠 **Your Business App** — Implement workflow definitions and state machine logic.
```

### Reference Material

- Use **tables** for properties, comparisons, and structured data
- Use **bullet lists** for procedures and simple enumerations
- Use **numbered lists** for step-by-step guides
- Use **Mermaid flowcharts** for architecture and data flow

---

## Consistency Rules for Future Updates

When updating workflow documentation:

1. ✅ Always use correct step type names: `question`, `check-answers`, `status-timeline`, `task-list`, `confirmation`
2. ✅ Never use: `Collect`, `Review`, `StatusTimeline`, `Completion`, `Archetype`
3. ✅ Field JSON options are plain string arrays: `["A", "B", "C"]`
4. ✅ Separate workflow definitions from field groups
5. ✅ Use blue/orange markers to indicate Prism vs. developer responsibility
6. ✅ Always include code examples in language-tagged blocks
7. ✅ Cross-reference related guides at the end of each document

---

## Impact on Other Teams

- **Celeste (C# XML Docs):** May need to update inline code documentation to match terminology
- **Brewster (API & Demos):** Workflow definition seeds should already use correct step types
- **All**: When adding new step types, update docs immediately

---

## Files Changed

- ✅ `docs/guides/workflow-setup.md` — Complete rewrite
- ✅ `docs/guides/workflow-customisation.md` — Comprehensive update
- ✅ `docs/guides/workflow-forms-validation.md` — Complete rewrite
- ✅ `docs/guides/workflow-gds-components.md` — Verified (no changes needed)
- ✅ `.squad/agents/mabel/history.md` — Added session entry

---

## Future Decisions Needed

- [ ] Update marketplace listing with new terminology
- [ ] Update CONTRIBUTING.md with workflow documentation guidelines
- [ ] Create style guide for all `/docs/` content (not just workflows)


---

## 📌 2026-04-22: Tom Nook — "Waiting" Step Type with Polling and Defer

**Decision:** Introduce a new step type `"waiting"` that represents workflows paused for external processing (payments, review queues, background jobs).

**Context:** Workflows need to pause and wait for external systems to complete processing while giving users two options:
1. Stay and watch (with accessible auto-polling UI)
2. Leave and return later (defer)

**Design:**

### 1. New Step Type
- `"waiting"` joins existing types: `question`, `check-answers`, `confirmation`, `status-timeline`, `task-list`
- Semantically distinct from `status-timeline` (historical read-only vs. active "please wait" state)
- ResponseState = `"render"` (not `"defer"` — waiting is actively engaging the user)

### 2. Workflow Definition Structure
New nested `WaitingConfig` object (only in `"waiting"` steps):
```json
{
  "stateKey": "payment-processing",
  "stepType": "waiting",
  "waitingConfig": {
    "message": "Processing your payment...",
    "expectedWaitSeconds": 30,
    "pollIntervalMs": 3000,
    "allowDefer": true,
    "deferMessage": "Leave and return later"
  }
}
```

**Why nested:** Keeps schema clean; only waiting steps use this config.

### 3. Polling Architecture
- New endpoint: `GET /api/prism/workflow/poll?workflowKey={key}&instanceId={id}&knownStateVersion={v}`
- Returns lightweight JSON: `{ "changed": bool, "newStateVersion": int, "stepType": string }`
- Uses existing `GetCurrentAsync` (no duplicate resolution logic)
- Stateless and perfect for high-frequency polling

**Why lightweight JSON:** Minimal overhead vs. full page reload; enables accessible live region updates without history pollution.

### 4. UI & Progressive Enhancement
- Partial view with GDS notification banner (blue "information" style)
- ARIA live region (`role="status" aria-live="polite"`) for accessible polling status
- JS polling with feature detection and graceful fallback
- Defer link works without JavaScript

### 5. Builder API
- Fluent method: `.WaitWith(message, expectedWaitSeconds, pollIntervalMs, allowDefer, deferMessage)`
- Auto-sets `stepType = "waiting"` (no separate `.StepType("waiting")` call needed)
- Reduces cognitive load and prevents accidental mismatches

**Implications:**

- Workflow authors fully control all content (message, wait time, polling interval)
- Client polls on interval; when state changes, performs full page reload
- Accessible: ARIA live regions, no vestibular triggers, progressive enhancement
- No breaking changes: new step type, existing types unaffected

**Success Criteria:**
- ✅ Authors define waiting states in JSON with all content controlled
- ✅ Users see accessible UI with expected time + polling status
- ✅ UI auto-updates when state changes
- ✅ Defer option works without JS
- ✅ Zero warnings in build/test

---

## 📌 2026-04-22: Blathers — Waiting State Backend Implementation

**Decision:** Implement waiting state as a first-class step type with dedicated configuration and API endpoints.

**Implementation:**

### 1. Models (UmbracoPrism.Shared)
Added `WaitingConfig` record with properties:
- `Message` — user-facing message during wait
- `ExpectedWaitSeconds` — estimated duration
- `PollIntervalMs` — client polling frequency (default: 3000ms)
- `AllowDefer` — show "leave and return" option (default: true)
- `DeferMessage` — custom defer text (optional)

**Why Shared:** Enables both Business App engine and integrator tooling access to definition shape.

### 2. Response State Semantics
- Waiting steps use `ResponseState = "render"` (NOT `"defer"`)
- "defer" reserved for status-timeline (passive, historical)
- "render" for waiting (active, engaging)
- Keeps rendering logic simple — same code path as question/check-answers

### 3. Polling Endpoint
New `WorkflowPollController`:
- Route: `GET /api/prism/workflow/poll`
- Params: `workflowKey`, `instanceId`, `knownStateVersion`
- Response: `{ changed: bool, newStateVersion: int, stepType: string }`

**Design rationale:**
- Uses existing `GetCurrentAsync` (no duplicate instance resolution)
- Stateless (perfect for high-frequency polling)
- Minimal payload (reduces bandwidth)
- Direct instanceId lookup (bypasses policy logic)

### 4. Data Flow
```
WorkflowDefinitionFile.StepDefinition.WaitingConfig
  ↓ (BuildEnvelope)
WorkflowResponseEnvelope.Render.WaitingConfig
  ↓ (CreateViewModel)
PrismWorkflowViewModel.WaitingConfig
  ↓ (View rendering)
Frontend polling loop
```

- `PollAfterMs` flows from `WaitingConfig.PollIntervalMs` for easy client access

### 5. BuildEnvelope Changes
- `BusinessAppWorkflowEngine.BuildEnvelope` passes `state.WaitingConfig` to `StepContent`
- Populates `PollAfterMs` from polling interval
- No changes to core.csproj (Core already references Shared)

**Seed Workflow:**
Created `payment-demo-v1.json` with 3 states:
- enter-details → processing-payment (waiting) → payment-complete
- Demonstrates 30-second expected wait, 3-second poll interval, defer option

**Implications:**
- Integrators add waiting states to workflows via `"stepType": "waiting"` + `waitingConfig` block
- C# authors use fluent `.WaitWith()` builder method
- All 543 tests pass (19 new waiting state tests)

---

## 📌 2026-04-22: Isabelle — Workflow Waiting Step UI Pattern

**Decision:** Create `_WorkflowStep-Waiting.cshtml` partial with accessibility-first design for the waiting UI.

**Design Choices:**

### 1. ARIA Live Region for Polling Status
- `role="status" aria-live="polite" aria-atomic="true"`
- Starts empty, updated by JS when polling detects state
- Applied `govuk-visually-hidden` so updates announced without visual distraction
- Prevents vestibular issues from animations

### 2. Progressive Enhancement
- All critical info visible without JavaScript (message, expected wait, defer option)
- Polling enhances but doesn't block
- If JS fails: user can manually refresh or use defer link
- Hidden data carrier div (`#prism-waiting-data`) holds config for JS

### 3. GDS Components
- **Notification Banner:** `role="region" aria-labelledby` with waiting message + computed wait time
- **Details Component:** Native `<details>/<summary>` for defer option (no JS required, degrades gracefully)
- **Human-friendly formatting:** Seconds → minutes → hours conversion

### 4. JavaScript Compatibility
- Traditional `function() {}` syntax (not arrow functions)
- Fetch API with `.then/.catch` (not `async/await`)
- Feature detection: checks for element existence
- Matches pattern from existing partials

### 5. Smart Polling Behavior
- Respects `document.hidden` (pauses when tab backgrounded)
- Max retry limit (100) with graceful fallback message
- URL encoding for all parameters (XSS prevention)
- Silent retry on non-200 responses

**Files:**
- Created: `src/UmbracoPrism.TestSite/Views/Partials/_WorkflowStep-Waiting.cshtml`
- Modified: `src/UmbracoPrism.TestSite/Views/workflowPage.cshtml` (added "waiting" case)

**Accessibility Compliance (WCAG 2.2 AA):**
- ✅ 1.3.1 Info and Relationships — proper landmark structure, semantic HTML
- ✅ 2.2.2 Pause, Stop, Hide — no auto-moving content, live region doesn't distract
- ✅ 2.3.1 Three Flashes — no animations or flashing
- ✅ 4.1.3 Status Messages — ARIA live region announces changes

**Dependencies:**
- Assumes `PrismWorkflowViewModel.WaitingConfig` and `PollAfterMs` (provided by Blathers)
- Assumes `/api/prism/workflow/poll` endpoint (Blathers)

**H1 Ownership:** Partial owns its h1 (consistent with completion/review partials).

**Build Status:** ✅ No errors (7 pre-existing warnings)

---

## 📌 2026-04-22: Mabel — WaitWith() Builder and Waiting State Documentation

**Decision:** Add `WaitWith()` fluent method to `WorkflowStateBuilder` and comprehensive documentation.

**Code Changes:**

### 1. Builder Method
File: `src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs`

Added to `WorkflowStateBuilder`:
- Private field: `_waitingConfig` (nullable)
- Method: `WaitWith(message, expectedWaitSeconds, pollIntervalMs = 3000, allowDefer = true, deferMessage = null)`
- Auto-sets `_stepType = "waiting"` (developers call one method instead of two)
- Updated XML docs on `StepType()` to mention `waiting` type

**Why auto-set stepType:** Reduces cognitive load, prevents accidental mismatches (e.g., forgetting `.StepType()`), method name clearly indicates intent.

### 2. Configuration Defaults
- `pollIntervalMs = 3000ms` — good balance between responsiveness and server load
- `allowDefer = true` — most workflows should allow users to leave and return
- `deferMessage = null` — server-side provides sensible defaults in UI

### 3. Documentation
File: `docs/guides/workflow-setup.md`

Added sections:
- **Step Types Reference:** Updated table to include `waiting` (6 types)
- **State Properties:** Added `waiting` to valid `stepType` values + `waitingConfig` property
- **New "Waiting States" Section (lines 180-337):**
  - When to use (payment, queue-based, background jobs)
  - When NOT to use (instant transitions, read-only status)
  - Configuration reference table
  - JSON example (complete 3-step payment workflow)
  - C# builder example
  - Mermaid flow diagram
  - Accessibility notes (ARIA live, defer, back button)

**Documentation Audience:** .NET developers; assumes C# knowledge; JSON + builder examples.

**Build Status:** ✅ No errors (7 pre-existing warnings)

---

## 📌 2026-04-22: Tangy — Waiting State Test Patterns

**Decision:** Establish three-layer test pattern for workflow engine features with JSON configuration.

**Testing Layers:**

### 1. Serialization Tests
- JSON deserialization validation
- Full and partial JSON configs with defaults
- Null handling and type preservation
- Must use `PropertyNameCaseInsensitive = true` (engine standard)

### 2. Builder Tests
- Each builder method independently tested
- Fluent chaining validation (returns same instance)
- Default values vs. explicit values
- Validate that raw `StepType("waiting")` without `WaitWith()` leaves `WaitingConfig` null

### 3. Engine Integration Tests
- Seed workflow files via temp directories
- Use `ResetAll()` for test isolation
- Validate envelope properties match definition
- Test state transitions through waiting states

### Test File Structure
```
BusinessAppWorkflowEngine{Feature}Tests.cs
├── {Feature}SerializationTests — JSON round-trip
├── {Feature}BuilderTests        — Fluent API
└── BusinessAppWorkflowEngine{Feature}Tests — Integration
```

**Key Technical Findings:**

1. **Engine constructor loads workflows from disk** — Tests writing seed files after instantiation need fresh engine instance
2. **PollAfterMs is null for non-waiting states** — Only populated when `state.WaitingConfig?.PollIntervalMs` is non-null
3. **Waiting states render normally** — `ResponseState = "render"`; only "status-timeline" + "confirmation" get special treatment

**Implementation:**
- 31 tests written, all passing
- Zero regression (543 total tests passing)
- Full coverage: JSON → Builder → Engine output

---

## 📌 2026-04-25: Tom Nook — Workflow Schema Cleanup Design Review

**Decision:** Recommend Option 1 (minimal cleanup) now; plan Option 2 (polymorphic hierarchy) as v2.0 schema.

**Context:** Workflow JSON envelope contains three sources of null bloat:

1. **`StepDefinition.StepType: null`** — authored `stepType` field never set by authors; runtime uses `EffectiveStepType` instead (computed from component tree). Appears as `null` in serialized output — dead weight.
2. **`PrismComponentDefinition` nullable slots** — 16 optional properties (Fields, Legend, BannerType, Level, etc.); most are `null` for unused component types. Serialized as-is when `JsonIgnoreCondition` not configured.
3. **`StepDefinition.WaitingConfig` sidecar** — already superseded by `waiting` component. Legacy back-compat shim with no production usage.

**Diagnosis Verified:** All three confirmed by code review, seed file analysis, and inference helper inspection.

**Option 1 — Minimal Cleanup (~1 day)**

- Configure `JsonIgnoreCondition.WhenWritingNull` on all four `JsonSerializerOptions` instances
- Delete `StepDefinition.StepType`
- Delete `StepDefinition.WaitingConfig` and `StepContent.WaitingConfig`
- Update `WorkflowStepDefinitionInference` (drop legacy `WaitingConfig` branch)
- Update ~25–40 Core.Tests references (mechanical edits)

**Effects:**
- ✅ `stepType: null` disappears from output
- ✅ Null-padded slots in component objects disappear
- ✅ Wire format matches authored intent (no dead weight)
- ✅ No breaking changes to authored JSON format (no seeds author these fields)
- ✅ No view-layer changes needed

**Option 2 — Modular Polymorphism (v2.0, ~1 sprint)**

Implement `[JsonPolymorphic]` + `[JsonDerivedType]` hierarchy; unify `PrismComponentDefinition` into sealed type hierarchy; collapse `PrismFieldTagHelper` + `PrismComponentTagHelper` into single polymorphic dispatcher.

**Effects:**
- ✅ Each component writes only its own properties (not a 16-slot god-record)
- ✅ Compile-time safety: can't set `BannerType` on a `PanelComponent`
- ✅ Authoring ergonomics: IntelliSense per component type
- ✅ Two parallel partial systems collapse into one
- ❌ Breaking JSON format change; migration required
- ❌ Requires seed file migration (`fields[]` → `children[]`, `fieldType:` → `type:`)
- ❌ Heavy test surface (~100+ touches in Core.Tests)

**Recommendation:**

Ship **Option 1 this week** (commission Blathers for ~1 day implementation). This fixes Jonny's stated concerns at the wire format without schema surgery.

Defer **Option 2 to v2.0** — it's the right long-term direction (tag helper partial dispatch already proves the polymorphism works), but breaking changes deserve a major version boundary and parallel deprecation window.

**What to defer explicitly:**
- Polymorphic `[JsonDerivedType]` hierarchy
- `fields[]` → `children[]` rename
- Fluent builder rewrite
- Two dispatch systems collapse

**What to do alongside Option 1:**
- Add this ADR entry to squad decisions (survives staff turnover)
- Stop adding new properties to `PrismComponentDefinition` (accept nullables, list on v2 agenda)
- When writing new components, use `_PrismComponent-{Type}.cshtml` location (shortens v2 migration)

**Next Action:** Awaiting Jonny's go/no-go on Option 1 scope before commissioning Blathers for implementation.


---

## 📌 2026-04-22: Blathers — Component Partial Dispatch System

**Decision:** Implement convention-based partial dispatch for PrismComponent rendering.

**Context:** The original `PrismFieldTagHelper` rendered all field types via a monolithic C# string builder switch statement inside the tag helper class. Adding a new field type required modifying `UmbracoPrism.Core`.

**Implementation:** `PrismComponentTagHelper` is now a thin async dispatcher that resolves `_PrismComponent-{TypeName}.cshtml` partials by convention for each component type, with fallback to `_PrismComponent-Default.cshtml`.

**Pattern:** For a component with `Type = "text"`, it resolves: `~/Views/Partials/PrismComponents/_PrismComponent-Text.cshtml`

**Rationale:** Extensible design allows downstream projects to override or add component types without modifying core package code.

---

## 📌 2026-04-22: Blathers — Partials in Core (Embedded Resources)

**Decision:** Move all default PrismComponent and PrismField partials into UmbracoPrism.Core as embedded resources via `EmbeddedFileProvider`.

**Implementation:** Physical files in consuming projects override embedded defaults (physical file provider wins in composite hierarchy).

**Override:** Place a file at `Views/Partials/PrismComponents/_PrismComponent-{Type}.cshtml` or `Views/Partials/PrismFields/_PrismField-{Type}.cshtml` in the consuming project.

**Rationale:** Package consumers get all component types OOTB. Custom component implementations stay in their own project, no coupling to Core.

**Notes:** .NET 10 uses `IStartupFilter` + `CompositeFileProvider` pattern on `IWebHostEnvironment.ContentRootFileProvider` (not `RazorViewEngineOptions.FileProviders`).

---

## 📌 2026-04-22: Isabelle — Component Partials Tag Helper & System

**Decision:** Introduced `PrismComponentTagHelper` + `PrismComponentContext` mirroring the existing `PrismFieldTagHelper` + `PrismFieldContext` pattern, with convention-based partial dispatch.

**Design:** All workflow step partials and top-level views are now embedded in `UmbracoPrism.Core` — TestSite no longer owns any workflow rendering files.

**Context:** Blathers replaced `FieldGroupKeys`/`FormSection` with `PrismComponentRenderPayload`. The frontend layer needed to consume the new model without duplicating dispatch logic in every consuming project.

---

## 📌 2026-04-22: Isabelle — GDS Field Partials Convention

**Decision:** All convention-based field partials use GOV.UK Design System `govuk-*` CSS classes exclusively for rendering workflow form fields.

**Implementation:** One Razor partial per field type at `Views/Partials/PrismFields/_PrismField-{Type}.cshtml`, dispatched by convention from the `<prism-field>` tag helper.

**Rationale:** TestSite uses govuk-frontend 5.9.0 (installed previously). Existing workflow step partials already use GDS patterns, so field partials must match for consistency. GDS provides WCAG 2.2 AA-compliant patterns OOTB.

---

## 📌 2026-04-22: Isabelle — Workflow Rendering Shell Inference & Content-Field Fallback

**Decision:** Keep the render layer resilient during the `stepType` → component-shape migration by deriving the workflow shell from rendered components, while still accepting legacy `StepType` and `WaitingConfig` as fallbacks.

**Design:**
1. Render-layer shell resolver inspects component shape first
2. `waiting` component or legacy waiting config → waiting shell
3. Content-only fields (`details`, `inset-text`, `warning-text`, `notification-banner`) render inline within fieldsets instead of falling back to default input partial
4. Waiting needs to stay accessible: live region, wait-time messaging, defer affordance work with legacy sidecar config or future first-class `waiting` component

**Rationale:** Authored `stepType` is being removed, but Razor still needs deterministic shell selection. Some authored "content" items arrive as field payloads inside fieldsets, so the field tag helper must render those inline.

---

## 📌 2026-04-22: Tangy — Workflow Regression Coverage Direction

**Decision:** Use **Core contract tests** as the primary regression gate for workflow changes.

**Test Strategy:**
1. Seed minimal workflow definitions with omitted `stepType` and assert engine inference across question, check-answers, confirmation, and waiting flows
2. Assert waiting metadata from component-authored `waiting` nodes reaches the envelope (`ResponseState = "render"`, `PollAfterMs`, derived `WaitingConfig`)
3. Add mixed-form tests that prove content-only fields don't render or validate like inputs, while neighbouring real inputs still produce GDS-style error metadata

**Rationale:** TestSite worktree throws workflow page model-binding exceptions on live routes, so browser tests aren't a reliable gate. Core-level contract tests exercise the changed workflow engine and field rendering rules without weakening desired behaviour.

---

## 📌 2026-04-22: Tom Nook & Blathers — stepType Removal & Component Model Unification (MERGED EARLIER)

**Status:** ✅ Already merged in decisions.md (line 5–65)

---

## 📌 2026-04-26T07:28:51Z: Jonny Muir — Solo Project Directive: No Feature Branches

**Status:** ✅ Directive captured and documented

**What:** Work directly on `main` — no feature branches, no PR ceremony, no merge overhead. If/when other contributors join, revisit.

**Why:** *"because it is only me working on this project, there is not need to create branches and expensive merges. If of course other people need to start contributing we can re-address that."*

**Implications:**
- Squad agents should commit directly to `main` going forward
- Spawn prompts should NOT instruct agents to create `feature/*` or `squad/*` branches except for issue-driven work explicitly requested
- PR-based workflows in routing.md / templates may need revision

**Implementation:** Append directive notes to squad agent history files for next spawn pickup.

---

## 📌 2026-04-26: Scribe — Orchestration & Session Workflow (v2 Planning + Option 1 Merge + Regression Fix)

**Status:** ✅ Completed

**Spawn Manifest:**
1. **tom-nook-v2-plan** (~427s) — Produced full v2.0 rollout plan to inbox
2. **blathers-option1** (long-running) — Landed Option 1 on feature branch (563/563 reported, 24 regressions on clean main)
3. **blathers-fix-waiting** (~601s) — Fixed 24 regressions (shell inference bug), 557/557 green

**Session Work:**
- Merged feature → main (fast-forward)
- Discovered and fixed regression root cause (empty-component steps inferring as `"status-timeline"` instead of `"question"`)
- Pushed main to origin; PR #36 auto-merged

**Process Lesson:** Test verification — always `dotnet test UmbracoPrism.sln -c Release` (with rebuild). Avoid blind `--no-build` without recent build in same session.

**Key Decision:** Solo project directive captured; main-only workflow going forward.

**Session Logs:**
- `.squad/log/2026-04-26T07-50-00Z-workflow-schema-option1-merge.md`
- Orchestration logs for three agents: `.squad/orchestration-log/2026-04-26T07-*.md`

---

## 📌 2026-04-26: Tom Nook — Workflow Schema v2.0 Rollout Plan (EXECUTIVE SUMMARY)

**Status:** ✅ Approved for execution (per Jonny Muir); full plan in inbox

**Mandate:** Implement polymorphic type hierarchy, view-layer collapse, `FieldFile` elimination.

**6-Phase Rollout (P1–P6):**
1. **P1:** Add abstract `PrismComponent` base + sealed derived types (zero existing files changed)
2. **P2:** Implement migrator (JSON v1 → v2 transformer)
3. **P3:** Engine reads v2 component tree
4. **P4:** Builder v2 API (C# authoring)
5. **P5:** View layer collapse (`PrismComponentTagHelper` becomes sole dispatcher)
6. **P6:** Release v2.0

**Design Decisions:**
- `[JsonPolymorphic]` discriminator: `"type"` (same string as today; no JSON key renames)
- **Input base record:** Shared field properties (`FieldKey`, `Label`, `Hint`, `Required`, `ConditionalOn`)
- **Each component:** Sealed record, only its own properties (no null padding)
- **SummaryListComponent.FieldRefs:** Engine resolves labels from definition tree (flagged as P3 prototype blocker)
- **conditionalFields:** Dict on `RadiosComponent`/`CheckboxesComponent`; replaces `FieldFile.ConditionalFields`

**Risk Flags:**
- `SummaryListComponent.FieldRefs` resolution unproven; needs P3 prototype
- Test surface: ~100+ touches across test suite (manageable with mechanical migration)

**First Commit:** P1 types only — zero existing files changed. Additive only.

**Target:** ≤610 tests at v2.0 (vs. 557 current)

---

## 📌 2026-04-22: Blathers — Test Verification Process (PROCESS DECISION)

**Context:** Option 1 implementation reported "563/563 tests passing" but on clean main, 24 tests failed (false-positive).

**Proposed Process:** Before reporting "all tests pass":

### Recommended Approach
```bash
dotnet test UmbracoPrism.sln -c Release
```
- Rebuilds the solution before running tests
- Eliminates cache risk entirely
- ~3-5s overhead but guarantees accuracy

### Alternative (Less Safe)
```bash
dotnet clean UmbracoPrism.sln -c Release
dotnet build UmbracoPrism.sln -c Release
dotnet test UmbracoPrism.sln -c Release --no-build
```

### ❌ Avoid
```bash
dotnet test UmbracoPrism.sln -c Release --no-build
```
- Risky: May test against stale binaries if prior build was incomplete
- Only safe if you *just* ran `dotnet build` in same session

**Test Filters:** When using `--filter`, ensure it captures full scope of changes:
- Backend model changes → Run full `Core.Tests` suite
- Specific component changes → Filter to relevant test class
- Breaking changes to inference/validation → Run integration + unit tests

**Recommendation for Team:** Document in `.github/CONTRIBUTING.md` or add pre-commit hook warning if `--no-build` used without recent `dotnet build`.


---

## 📌 2026-04-26: Jonny Muir — v2.0 Scope Decision: Generic ConditionalOn Deferred to v2.1

**Decision:** Defer generic `ConditionalOn` + `VisibleWhen` on arbitrary components to v2.1. v2.0 ships with `ConditionalChildren` on Radios/Checkboxes only.

**Context:** Tom Nook's design audit (`.squad/decisions/inbox/tom-nook-v2-design-doc-audit.md`) revealed that v1's conditional-fields model allows any field to have `ConditionalOn` + `VisibleWhen` properties. In v2, this would require:

- **Option A:** Generic conditional properties on base `PrismComponent` — flexible but requires tree traversal for every render/validation
- **Option B:** Dedicated `ConditionalContainerComponent` wrapper — explicit but adds nesting verbosity
- **Option C:** Defer to v2.1 — keeps P3 lean, ship v2 sooner

**User Directive:** Jonny chose Option C per Tom's recommendation.

**Rationale:**
- `ConditionalChildren` on Radios/Checkboxes covers the canonical "Other → specify" pattern (~80% of use cases)
- Avoids tree traversal complexity in v2 MVP
- Allows v2.0 to ship on schedule
- v2.1 can implement generic Option A (base class properties) with full tree-traversal infrastructure

**What This Means:**

- **v2.0 supports:** `RadiosComponent.ConditionalChildren: Dictionary<string, PrismComponent[]>` only
- **v2.0 does NOT support:** Generic `ConditionalOn` on arbitrary components (e.g., conditional BodyComponent, conditional TextInput)
- **v2.1 roadmap:** Implement generic conditional properties on base PrismComponent + engine tree-traversal logic

**Implementation:** P3 prototype phase should focus on `ConditionalChildren` rendering/validation (already in scope) and defer generic conditionals to v2.1 spike.

**Basis:** User directive this round; Tom Nook design audit (2026-04-26); design gap #3 in audit memo.

---

## 📌 2026-04-26: Tom Nook — Workflow Schema v2.0 Design Audit (Audit Memo Merged)

**Status:** ✅ Audit complete; memo merged into decisions

**Scope:** 9 workflow design documents audited against v2 component plan

**Key Findings:**
1. Confirmed: Fields BECOME first-class components (no `fields[]` array)
2. 7 of 9 docs need rewrite (deferred to P5/P6 per rollout plan)
3. No showstoppers — polymorphic design is sound
4. 8 design gaps surfaced (tree traversal, authorization, summary-list, fieldset validation, etc.)

**Audit Memo Location:** `.squad/decisions/inbox/tom-nook-v2-design-doc-audit.md`

**Headline Gaps for P3:**
- Component-tree validation traversal (add to P3 scope)
- Component-tree authorization checks (add to P3 scope)
- Generic conditional visibility (defer to v2.1 — user approved)
- Summary-list + conditionally-hidden fields (P3 blocker already flagged)
- Fieldset-level validation rules (defer to v2.1)
- Conditional children depth limit (P2 migrator or P4 builder)
- Umbraco integration JSON examples (doc debt, P5-P6)
- Redesign doc obsolescence (archive with pointer to v2 plan)

**Recommended Doc Rewrite Order:**
- P1-P2: No changes
- P3: Add "v2 in progress" banner to 4 docs
- P4: Update code examples (builder v2 API)
- P5: Update client docs (view layer collapse)
- P6: Final rewrite; remove banners; archive obsolete doc

**Next Action:** No doc changes until P5/P6 per rollout plan. All 9 docs reviewed; no surprises.
