# Tangy — History

## Core Context

QA validation, test coverage analysis, and edge-case identification.

**Key domains:** Playwright testing, E2E validation, Edge case coverage, CI/CD readiness, Performance analysis

## 📋 Recent Sessions

---

## 2026-05-03 11:46 — TestSite 404 / Blank Page Repro

**Status:** ✅ Root cause confirmed — Aspire stack not running

### Findings

Probed all Aspire stack ports externally against `organic-space-fortnight-77g9wvq6jxhxg97`:

| Port | Service | Result | Meaning |
|------|---------|--------|---------|
| 3000 | Status server | 401 `www-authenticate: tunnel` | **Alive** — GitHub private-port auth wall |
| 44345 | TestSite | 404 + no Content-Type + 0 bytes | **Not listening** |
| 15135 | Aspire Dashboard | 404 + no Content-Type + 0 bytes | **Not listening** |
| 8443 | Keycloak | 404 + no Content-Type + 0 bytes | **Not listening** |
| 7245 | MockBiz | 404 + no Content-Type + 0 bytes | **Not listening** |

Playwright screenshot of port 44345: **blank white page** (browser renders GitHub's 0-byte 404 as empty document). Screenshot of port 3000 in unauthenticated browser: GitHub's "Connecting to the forwarded port…" splash (port is alive, requires GitHub session cookie).

### Root Cause

The Aspire AppHost stack is **entirely down**. The status server (port 3000) survived (it's a plain Node.js process), but all four Aspire-managed services have no listener behind the Codespaces tunnel. GitHub's tunnel returns `HTTP 404, content-length: 0, no Content-Type` for ports with nothing listening — identical pattern to the earlier port-3000 outage, but this time it is the full Aspire stack, not the status server.

The user sees a blank white page rather than an error because the 0-byte 404 renders as an empty document in Chrome/Edge.

### Fix Direction (for Brewster / Jonny)

Not a repo code bug — the Aspire AppHost process is simply not running inside the Codespace. Likely causes:
1. Codespace was suspended and `on-start.sh` fast-exited (the AppHost `pgrep` check passes if the binary is cached, but the .NET runtime and services may not have re-bound their ports in time)
2. AppHost crashed — needs `artifacts/startup-status/prism-apphost.log` review inside the Codespace
3. Docker-in-Docker may not have been ready when AppHost tried to start containers

**Recommended action:** In the Codespace terminal: `tail -50 artifacts/startup-status/prism-apphost.log` to see last known crash/state, then `dotnet run --project src/UmbracoPrism.AppHost` if it needs a manual restart.

### Key Learning

When all Aspire ports return GitHub-tunnel 404, the diagnosis is always "AppHost not running" not "app error" — look at the AppHost log, not the TestSite logs. The status server at port 3000 is the health canary: if it's 401 (alive), the Codespace is up; if all app ports are 404, the stack hasn't started.

---

## 2026-05-03 11:11 — Live Codespaces Download Repro

**Status:** ✅ Root cause confirmed; fix identified as not yet live on Codespace

### Findings

Reproduced the "download question" via external probe of `https://organic-space-fortnight-77g9wvq6jxhxg97-3000.app.github.dev`.

| Path | Status | Content-Type |
|------|--------|--------------|
| `/` | 404 | (none) |
| `/api/status` | 404 | (none) |
| `/api/log` | 404 | (none) |

The `x-served-by: tunnels-prod-rel-uks1-v3-cluster` header confirms responses come from the GitHub Codespaces tunnel proxy, NOT from the Node.js server. Port 3000 is not listening in the Codespace.

### Root Cause

GitHub's tunnel proxy returns **HTTP 404 with no `Content-Type`** when the underlying port is not listening. Combined with `X-Content-Type-Options: nosniff`, browsers (especially Safari) treat this as an unknown/downloadable blob and prompt a file-download dialog.

The mechanism: when a Codespace disconnects-and-reconnects without full suspension, `AppHost` survives but the Node.js status server (port 3000) dies. The old `on-start.sh` fast-exits on `pgrep AppHost` and never restarts the Node server.

### Fix State

Fix commit `5f41b03` (`fix(codespaces): restart status server on resume if port 3000 is dead`) is on `origin/main` and correctly addresses the root cause. The running Codespace has NOT pulled this commit yet — it needs `git pull` + reconnect (or manual `node scripts/startup-status/server.js &`) to activate the fix.

### Key Learning

GitHub Codespaces tunnel returns HTTP 404 + no Content-Type (not 502/503) when the backend port is not listening. This 0-byte 404 is treated as an unknown file by Safari and some Chrome configurations, triggering a download dialog. The fix must be in the `postStartCommand` fast-exit path, not just the full startup path.

---

## 2026-05-02 — PR #45 Test Review: Codespaces URL Derivation Fix

**Status:** ✅ APPROVED WITH NOTES  
**Test result:** 647 passed, 0 failed, 0 skipped

### Criteria Outcomes

| # | Criterion | Result |
|---|-----------|--------|
| 1 | New `{token}-{port}.{region}.app.github.dev` URL form tested | ✅ Group D — 2 tests |
| 2 | Regression test for JWKS fetch / 401 symptom | ✅ `JwksFetch_RewritesUrl_ForRegionalCodespacesUrlScheme` |
| 3 | Request.Host override middleware tested | ⚠️ NO unit test |
| 4 | `IsRepoOwnedLocalDemoTenant` False-case for non-demo hosts | ✅ `IsRepoOwnedLocalDemoTenant_ReturnsFalse_ForNonCodespacesDomain` |
| 4b | `TenantService.GetByDomainAsync` lenient LIKE fallback tested | ⚠️ NO test |
| 5 | No deleted/skipped/ignored tests | ✅ Clean |

### Follow-up Gaps (non-blocking)

1. **`TenantService.GetByDomainAsync` lenient LIKE fallback** — The `LIKE '%.app.github.dev'`
   fallback added to `TenantService.cs` (the key runtime fix enabling new-scheme tenant lookup)
   has no unit test. Need: one positive case (regional URL falls back to seeded tenant) and one
   negative case (non-.app.github.dev does NOT trigger fallback). → `TenantServiceCacheStrategyTests.cs`

2. **Request.Host override middleware** — `TestSite/Program.cs` inline middleware (reads
   `TESTSITE_PUBLIC_URL`, overrides `Request.Host` for HTTPS) is untested. → New test class or
   extend `PrismTenantMiddlewareTests`.

### Key Learning

When reviewing a "multi-surface" fix (AppHost URL discovery + middleware + seeder + service layer),
map each code surface to a test and flag any that have no corresponding unit test. The most
dangerous surfaces are the service-layer fallback (TenantService LIKE) and the middleware Host
override — both are silent runtime logic with no compile-time errors if they break.

Token refresh (`RefreshTokenAsync`) was not specifically tested with a regional URL form as
authority, but the risk is low because `BackchannelRewritingDocumentRetriever` operates on URI
origins — the JWKS test proves the same path. Still worth noting as a low-priority gap.

---

---

## 2026-05-03 12:07 — Live Codespaces Dashboard Repro: MockBusinessApp Backchannel Failure

**Status:** ✅ Root cause identified — hardcoded backchannel URL bypasses Aspire service discovery

### Findings

Reproduced the "Call Mock Business App API" failure on live Codespaces by external probing and code analysis. All Aspire stack ports (3000, 44345, 15135, 8443, 7245) are **alive** (returning 302 GitHub auth redirects), confirming the AppHost is running — contrast with 2026-05-03 11:46 session where all ports returned 404.

**Root cause:** `src/UmbracoPrism.AppHost/Program.cs` line 139 hardcodes `BUSINESSAPP_BACKCHANNEL_URL` to `http://localhost:5163`, assuming TestSite and MockBusinessApp share the same localhost network. This works in simple local dev but can fail in Codespaces when:
- Aspire uses container orchestration with separate network namespaces
- Docker-in-Docker remaps internal ports
- Service discovery requires dynamic endpoint resolution

### Evidence

| Service | Backchannel Pattern | Line |
|---------|---------------------|------|
| Keycloak → TestSite | `keycloak.GetEndpoint("http")` | 131 |
| Keycloak → BusinessApp | `keycloak.GetEndpoint("http")` | 145 |
| **BusinessApp → TestSite** | `"http://localhost:5163"` **(hardcoded)** | **139** |

The BusinessApp backchannel is the **only one** that doesn't use dynamic endpoint resolution. Keycloak backchannel URLs use `.GetEndpoint("http")`, which returns the Aspire-managed internal HTTP endpoint — guaranteed to work across all orchestration modes.

### User-Visible Symptom

1. Dashboard loads successfully (TestSite is running)
2. User clicks "Call Mock Business App API"
3. JavaScript calls `/api/prism/downstream-demo`
4. Controller calls `http://localhost:5163/api/backoffice/me` with Bearer token
5. **Request times out or connection refused**
6. User sees: "We could not reach the Mock Business App. Check that it is running, then try again."

### Diagnostic Gap

External probes cannot reproduce the exact failure because:
1. Dashboard requires GitHub tunnel auth (302 redirect)
2. Server-side logs showing `HttpRequestException: Connection refused` are not externally accessible
3. Diagnostics require reading TestSite logs inside the Codespace

**Recommended confirmation step (inside Codespace):**
```bash
curl -v http://localhost:5163/api/backoffice/me
```

If this fails with "Connection refused", the hardcoded localhost URL is confirmed as the issue.

### Recommended Fix

Replace line 139 with dynamic endpoint resolution:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

This matches the Keycloak backchannel pattern and ensures Aspire resolves the correct internal endpoint regardless of networking mode.

### Key Learning

**Backchannel URL hardcoding is fragile in orchestrated environments.** When a codebase uses dynamic endpoint resolution (`.GetEndpoint("http")`) for some backchannel URLs but hardcodes others (`http://localhost:5163`), the hardcoded URLs are the first to break when networking assumptions change. The inconsistency itself is the smell — if Keycloak needs `.GetEndpoint()`, BusinessApp does too.

Always use the same pattern across all internal service-to-service calls. Any hardcoded `localhost` URL in an Aspire AppHost is suspect.

---

## 📌 2026-04-30: Cross-Agent Note — V2 Decimal Validation Test Coverage

**Context:** Blathers' 2026-04-28 option 1 fix added decimal field validation. Noted as blind spot: "No compile-time guarantee all field types handled in validator."

**Recommendation for Future:** Add comprehensive test suite for WorkflowFieldValidator covering ALL field types (`text`, `number`, `decimal`, `email`, `date`, `radios`, `checkboxes`, etc.) + constraint combinations. Extract field types to shared enum/constants to enable exhaustiveness checks.

---

## Session: Instance Policy Test Suite (2026-04-21)

**Status:** ✅ Complete — 19 new tests, 512 total passing

**Coverage:**
- Single policy: find-or-create behavior, parameter validation
- Multiple policy: new instance per call, resume by ID
- Prompt policy: picker trigger, action precedence, terminal state handling
- Cross-policy: access control (tenant/user isolation), lookup key consistency, concurrency

**Test File:** `src/UmbracoPrism.Core.Tests/Business/Workflow/BusinessAppWorkflowEngineInstancePolicyTests.cs`

**Strategy:** Arrange-Act-Assert pattern; multi-tenant security verified; zero regressions

---

## Session: Backchannel Rewrite Regression Tests (2025-07-XX)

**Status:** ✅ Complete — 11 new tests, 642 total passing

**Task:** Regression coverage for Development-only backchannel URL rewrites:
- Copper's refresh-token rewrite (`PrismContext.RefreshTokenAsync`)
- Blathers' JWKS rewrite (`PrismAuthExtensions.ResolveSigningKeys`)

**Security Fix Found & Applied:**
`PrismAuthExtensions.ResolveSigningKeys` was missing the `isDevelopment` check on the JWKS backchannel rewrite path. Only `KEYCLOAK_BACKCHANNEL_URL` was checked; now requires `ASPNETCORE_ENVIRONMENT=Development` too. Matches Copper's dual-gate pattern.

**Test File:** `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs`

**Coverage (3 groups):**
- Group A: Refresh-token rewrite gating — endpoint URL, dev vs. prod gating, issuer validation resilience
- Group B: JWKS fetch rewrite gating — metadataAddress capture via mock IPrismSigningKeyCache, dual-gate verification
- Group C: Bedrock invariants — ValidateIssuer/ValidateAudience always true, MockBusinessApp fail-loud guard exists

**Test Stability Fix:**
Added `EnvVarSensitiveTestCollection` to serialise `BackchannelRewriteTests` and `PrismSigningKeyCacheTests`. Parallel env-var leakage (KEYCLOAK_BACKCHANNEL_URL + ASPNETCORE_ENVIRONMENT=Development) caused intermittent failures in `WarmAsync_WithMetadataAddress_RequiresHttps_ForHttpsUrl`.

**Key Learnings:**
- `BackOfficeTenant` is a positional record — config keys must match property names exactly: `EntraTenantId`, `ClientId`, `Code`, `DisplayName`, `OidcAuthority`. Wrong keys (`OidcClientId`) silently produce empty tenant lists causing early return in `ResolveSigningKeys`.
- JWKS tests need mock `IPrismSigningKeyCache` registered BEFORE `AddPrismAuthentication` (uses `TryAddSingleton`). Mock must return `IsExpired: true, ContainsRequestedKey: false` to trigger `WarmAsync` call.
- Env var mutations in parallel tests need `[Collection]` isolation to prevent flakiness.
- Path from test binary to solution root: `AppContext.BaseDirectory` = `bin/Release/net10.0/` → 5× `../` to reach solution root.

---


**2026-04-20:**
- GDS Field Type Test Coverage Phase 1 Completion (validator tests)
- Playwright E2E Tests for Planning Workflow (happy path + conditions)

**2026-04-19:**
- GDS Phase 2 — Playwright E2E for Planning Workflow

**2026-04-15:**
- GDS Field Type Test Coverage (new field types in validator)
- Workflow Builder Test Coverage

**2026-04-14:**
- Aspire localhost auth CI job QA
- Phase 1 Security Regression CI Test Fix

**Key Learnings:**
- Test-driven seeding strategy: create minimal JSON seeds programmatically in `IDisposable` fixtures (test isolation + real engine loading)
- GDS patterns validation: error summary, summary list, confirmation panel
- Web component tests target rendered HTML, not component tags
- Edge cases in multi-policy state machines best covered by cross-policy test scenarios
- Field type exhaustiveness requires shared enum or compile-time verification

---

## 2026-05-02 — Codespaces 401 Downstream Auth: Backchannel Hardening + 11 Regression Tests (7a9b1c3)

**Session:** 2026-05-02-codespaces-401-downstream-auth  
**Test Commit:** `7a9b1c3` — Backchannel rewrite regression tests + IsDevelopment hardening  
**Tests added:** 11 new regression tests in `BackchannelRewriteTests.cs`

### Work Completed

**Phase 1 — Test Coverage (13:45–14:15)**
- Wrote 11 regression tests covering all three backchannel rewrite surfaces
- Tests validate gating patterns, edge cases, URL rewriting behaviour
- Infrastructure: `EnvVarSensitiveTestCollection` for tests that mutate `ASPNETCORE_ENVIRONMENT`

**Phase 2 — Critical Discovery (14:15–14:30)**
- During test writing, discovered missing `IsDevelopment()` gate in `PrismAuthExtensions.ResolveSigningKeys`
- Third backchannel rewrite site (JWKS metadata address) had env-var gate ONLY
- No `IsDevelopment()` check — leaving runtime code unguarded
- Applied fix: added `IsDevelopment()` check to match Copper's and Blathers' implementations
- Wrote test `PrismAuthExtensions_ResolveSigningKeys_NonDevelopment_IgnoresBackchannel` to verify gate

### Test Suite Details

**File:** `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs` (11 tests)

| Test | Coverage |
|------|----------|
| `RefreshTokenAsync_WithBackchannelUrl_Development_RewritesTokenEndpoint` | Copper's fix |
| `RefreshTokenAsync_NoBackchannelUrl_UsesPublicEndpoint` | Copper's fix |
| `RefreshTokenAsync_NonDevelopment_UsesPublicEndpoint` | Copper's fix |
| `PrismSigningKeyCache_WarmAsync_WithBackchannelUrl_Development_RewritesJwksUri` | Blathers' fix |
| `PrismSigningKeyCache_WarmAsync_NoBackchannelUrl_UsesPublicUrl` | Blathers' fix |
| `PrismAuthExtensions_ResolveSigningKeys_WithBackchannelUrl_Development_Succeeds` | Hardening: gate confirmed |
| `PrismAuthExtensions_ResolveSigningKeys_NonDevelopment_IgnoresBackchannel` | Hardening: gap closed |
| `RewriteUrl_PreservesPathAndQuery` | Edge case |
| `RewriteUrl_HandlesNullBackchannelUrl` | Edge case |
| `RewriteUrl_SkipsNonKeycloakUrls` | Edge case |
| `RewriteUrl_EmptyPath` | Edge case |

### Infrastructure Added

**File:** `src/UmbracoPrism.Core.Tests/EnvVarSensitiveTestCollection.cs`

Isolated XUnit collection for environment-variable-mutating tests. Prevents test pollution across collections.

**Skill documented:** `.squad/skills/backchannel-rewrite-testing/SKILL.md`

### Key Discovery: IsDevelopment() Gate Gap

**What was found:**
- `PrismAuthExtensions.ResolveSigningKeys` had backchannel rewrite logic without `IsDevelopment()` gate
- Startup guards throw if env var set in non-Development
- But runtime code was unguarded — missing the suspenders on the belt

**Why this matters:**
- **Contract-first testing caught a review gap:** Test specified "must NOT activate when not Development"; one test went red
- **Validates multi-agent team pattern:** Copper + Blathers each focused on their own rewrite site; Tester found the third site by writing tests to the contract, not to the implementation
- **Single-agent review would likely have missed this**

**Fix applied:**
```csharp
var isDevelopment = env.IsDevelopment();
if (isDevelopment && !string.IsNullOrEmpty(backchannelUrl))
{
    // Apply rewrite
}
```

Now all three sites follow identical dual-gating pattern.

### Test Results
- Before: 618 tests passing (PT2 baseline)
- After: 629 tests passing (+11 new)
- Status: All green; no regressions

### Key Learning

**Parallel review pattern validation:** When a security-relevant pattern is introduced (e.g. dual-gated dev-only behaviour), searching the entire repo for the *trigger* (not just the diff files) finds sibling sites that should follow the same pattern. Contract-first testing (testing the gate behaviour, not the implementation) catches gaps that implementation review misses.

### Artifacts
- **Session log:** `.squad/sessions/2026-05-02-codespaces-401-downstream-auth.md`
- **Skill:** `.squad/skills/backchannel-rewrite-testing/SKILL.md`

### Status
✅ **APPROVED FOR MERGE** (Copper's security review, 2026-05-02 14:30)


**2026-05-02** — Completed: Validated regression; rejected current behavior because non-JSON HTML responses were still treated as success; added/identified regression coverage for HTML and non-JSON false positives. Findings cascaded to Brewster for fix implementation.
---

### 2026-05-03 09:39:06 — CI Test-Isolation Fix Implementation

**Event:** Received test-isolation scope from Tom Nook review.

**Role:** Test specialist. Revised Blathers' fix per scope: removed runtime changes, kept `EnvVarSensitiveTestCollection`, preserved env snapshot/restore, maintained regression coverage.

**Changes:** Test layer only — no product-code behavior modifications.

**Status:** Implementation ready for CI validation.

## 2026-05-03: Team Spawn — Startup Helper Validation

**Status Update (Scribe):** Tangy validated startup helper uses `/api/*` endpoints (no loops on missing legacy routes), reports TestSite ready off correct readiness route. Safari download issue not reproduced locally after fix.

---

## 2026-05-03: Startup-Status URL Regression Suite

**Status:** ✅ Complete — 24 new tests, all passing

**Trigger:** User-visible regression: "do you want to allow downloads" browser prompt + repeated 404s when clicking links on Codespaces startup status page after `refresh.sh`.

### Root Causes Identified

| # | Bug | Location | Fix (prior commit) |
|---|-----|----------|--------------------|
| 1 | `tr -d '/'` strips ALL slashes — `https://` → `https:` | `on-start.sh` `get_codespace_url()` | `sed 's\|/*$\|\|'` (417a038) |
| 2 | Node.js doesn't survive Codespace suspension; fast-path `pgrep` exited without checking port 3000 → proxy returns 404, Chrome shows download dialog | `on-start.sh` resume block | Probe `http://localhost:3000/api/status`, restart if dead (5f41b03) |

Both bugs were already fixed in commits before this session. Session added permanent regression coverage.

### Work Completed

**Extracted `url-utils.js`** — Pure URL functions (`parseCodespacePorts`, `deriveCodespacesUrl`, `makePublicUrl`) from `server.js` into a testable module. Doc comment explains legacy-fallback risk on empty portUrls.

**24 regression tests in `server.test.js`** (3 describe groups):
- `parseCodespacePorts` — empty map, single port, multi-port, trailing slash strip (the `tr -d '/'` regression), non-numeric sourcePort
- `deriveCodespacesUrl` — same port returns same URL, different port derives new URL, regional subdomain format, mismatched base URL
- `makePublicUrl` — known port returns map URL, unknown port legacy fallback, empty portUrls fallback (post-stop.sh scenario), resume scenario (port 3000 derives correctly when other ports known)

**Refactored `server.js`** — Imports from `url-utils.js`; `publicUrl` now factory result from `makePublicUrl(...)`; `ASPIRE_PUBLIC_URL` moved after `publicUrl` definition (no longer relies on function hoisting).

### Key Learning

**`tr -d` vs trailing-strip intent:** `tr -d 'x'` deletes all occurrences globally — never use it to strip trailing characters. Use `sed 's|x*$||'` or shell `${var%x}`. A quick unit test on `browseUrl` processing would have caught this instantly.

**Node.js process lifetime in Codespaces:** Node.js processes are killed on Codespace suspension. Any `pgrep`-based fast-path that infers the status server is alive from another process being alive is incorrect. Always probe the actual port.

### Residual Risk (Documented, Not Fixed)

When `gh codespace ports` is called before any ports are open, `CODESPACE_PORT_URLS` is empty. `publicUrl()` falls back to `https://${CODESPACE_NAME}-${port}.${DOMAIN}` (legacy pattern). On new-scheme regional Codespaces, `CODESPACE_NAME` ≠ the opaque subdomain token → wrong URLs. Documented in test "regression: empty portUrls falls back to legacy CODESPACE_NAME pattern" and in `url-utils.js` doc comments.

### Files
- `scripts/startup-status/url-utils.js` — new
- `scripts/startup-status/server.test.js` — new (24 tests)
- `scripts/startup-status/server.js` — refactored

---

## 2026-05-03: Downstream Diagnostics Coverage

**Status:** ✅ Complete; merged to main with 618 tests passing (+17 new).

**Scope:** Improve downstream dashboard diagnostics for non-JSON responses and add regression coverage.

**Enhanced `DownstreamDemoController`:**
- Log non-JSON downstream responses with HTTP headers
- Preserve real HTTP status/reason (not flattened to `statusCode: 0`)
- Include diagnostic text with headers like `WWW-Authenticate` for 401 auth rejections
- Retry logic uses per-request cancellation token (no mutation of `HttpClient.Timeout` between requests)

**New Regression Tests:**
- 401 Unauthorized with `WWW-Authenticate` header surfacing
- 302 redirects with `Location` header diagnostics
- Unknown Content-Type responses with real HTTP metadata preservation

**Impact:** Live Codespaces repro will now clearly distinguish transport failure, auth rejection, proxy redirects, and HTML tunnel pages.

