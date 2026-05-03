# Tangy — History (Summary)

**Agent:** Tester specializing in browser contracts, diagnostics, and API validation for Codespaces environments.

**Recent focus (2026-05-03):** Downstream API timeout diagnosis, diagnostics script operator workflows, Python runtime hardening, no-Python rewrite validation, browser-to-backend testing.

---

## 2026-05-03: Session Summary

- 🔍 **Diagnosed** downstream API timeout (backchannel port hardcoding vs runtime discovery) → handed to Blathers
- ✅ **Validated** no-Python diagnostics script rewrite with regression contract
- ⏳ **Reduced** operator diagnostic flow to three checks (per Blathers findings)
- 📝 **Decision:** Codespaces Downstream Diagnostics Must Not Require Python
- �� **Decision:** Browser-Facing API Responses Must Not Expose Internal Backchannel URLs
- 📝 **Decision:** Diagnostics Script Landing: Product vs. Bookkeeping Separation

**Current state:** Operator flow ready for manual validation; await Blathers backchannel port fix completion.

---

## 2026-05-03: Downstream Timeout URL-Choice vs BusinessApp Diagnosis — Shortest Operator Sequence

**Timestamp:** 2026-05-03T22:27:45.244+01:00  
**Status:** ✅ Complete

### Context

User has already established:
- Browser session-contract healthy (cookie authenticated, tokens present, tenant resolved, authorizationHeaderReady=true)
- Internal BusinessApp `/debug/auth` returns 200
- TestSite session-contract healthy
- Keycloak backchannel healthy
- BUT: Browser call to `/api/prism/downstream-demo` times out after 10s with status 0, targeting public Codespaces URL `https://jubilant-space-tribble-vpxvw645763pr74-7245.app.github.dev/api/backoffice/me`

**Question:** Is this a browser-visible forwarded URL choice problem or BusinessApp itself timing out?

### Shortest Operator Sequence (2-3 checks)

**Check 1: Internal backchannel bypass (10 seconds)**

```bash
# From Codespace terminal, call the internal endpoint that TestSite uses server-side:
PRISM_BEARER_TOKEN='<access-token-from-session-contract>' bash scripts/codespaces/diagnose-downstream.sh
```

Watch the `[AUTHENTICATED] Internal backchannel (http://localhost:5163)` section:
- **200 OK** → Internal path works; timeout is specific to public forwarded URL
- **Timeout** → BusinessApp itself is hanging regardless of URL choice

**Check 2: Browser DevTools Network tab — copy as cURL (30 seconds)**

In browser:
1. F12 → Network tab → Clear → Click "Call Mock Business App API"
2. Right-click the failed request → Copy as cURL
3. Run the copied cURL in terminal (it uses the public Codespaces URL)
4. Replace URL with internal: change `https://jubilant-space-tribble...7245.app.github.dev` to `http://localhost:7245` and rerun

**Outcomes:**
- Public URL times out, localhost:7245 succeeds → GitHub tunnel/forwarding issue
- Both timeout → BusinessApp or Keycloak validation is hung
- Both succeed → TestSite isn't calling what you think it's calling (check controller logs)

**Check 3 (if both timeout): Keycloak JWKS reachability (5 seconds)**

```bash
# BusinessApp validates tokens by fetching signing keys from Keycloak:
curl -v http://localhost:8080/realms/prism-dev/protocol/openid-connect/certs
```

- **200 OK with `{ "keys": [...] }`** → Keycloak healthy; BusinessApp issue
- **Connection refused / timeout** → Keycloak unreachable; token validation hangs

### Outcome

These three checks isolate:
1. Whether internal backchannel succeeds where public URL fails (forwarding problem)
2. Whether BusinessApp is actually reachable on both paths (BusinessApp vs tunnel)
3. Whether Keycloak backchannel is responsive (common hung-validation cause)

### Learnings

**Browser DevTools "Copy as cURL" is the fastest URL-path comparison tool:** Copy once, run twice (public URL, then localhost equivalent). 10-second divergence is immediate evidence of tunnel vs app failure.

**Keycloak JWKS endpoint is the most common backchannel hang:** If BusinessApp can't fetch signing keys, every authenticated request will wait until HTTP client timeout (default 100s in ASP.NET Core, reduced to 10s in DownstreamDemoController).

**Diagnostics script with bearer token closes the full path in one pass:** `PRISM_BEARER_TOKEN='...' bash scripts/codespaces/diagnose-downstream.sh` runs both internal and public probes with authentication in a single command.

---
## 2026-05-03T22:27:45Z: Spawn Manifest — Operator Flow Reduction

**Status:** ✅ Complete (operator workflow)

**Execution:** Reduced operator diagnostic flow to three checks per Blathers findings on backchannel URL selection.

**Check Sequence:**
1. **Run diagnostics script** with real bearer token from live environment
   - `bash scripts/codespaces/diagnose-downstream.sh --token <bearer>`
2. **Compare public-vs-localhost cURL** for copied request
   - Public: `curl https://v7ldkc4c-7245.uks1.app.github.dev/...`
   - Localhost: `curl http://localhost:5163/...`
   - Identify which hangs, which succeeds, timeout profiles
3. **Probe Keycloak JWKS** only if both still hang
   - Rules out token validity as first cause
   - Narrows to infrastructure/DNS/routing

**Impact:** Operator can triage within 3–5 minutes using manual testing + diagnostics script.

**Artifact:** `.squad/orchestration-log/2026-05-03T21-27-45Z-tangy.md`

---

## Learnings

### 2026-05-03T23:46:52.875+01:00: Arrival Logging Needs Validation More Than Contract Tests

**Context:** Blathers proposed arrival logging in `src/UmbracoPrism.MockBusinessApp/Program.cs` to prove whether downstream timeout requests reached MockBusinessApp before auth or `/api/backoffice/me` execution. The change is observability-only and does not alter the browser-visible contract already covered in `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`.

**Pattern:** When a change adds server-side arrival logs without changing response payloads, prefer validating the existing build plus the focused downstream diagnostics test lane over brittle assertions on exact log strings or middleware implementation details. Only add automated tests if the logging introduces a stable operator-facing contract (for example a structured response field, documented event ID, or reusable diagnostics surface).

**Key file paths:** `src/UmbracoPrism.MockBusinessApp/Program.cs`, `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`, `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`

### 2026-05-03T23:26:29.163+01:00: Timeout Diagnostics Need Structured Cause + Masked Transport Context

**Context:** `DownstreamDemoController` now emits richer timeout diagnostics for `/api/prism/downstream-demo`, including a `timeout` payload, `summary`, `nextCheck`, and transport metadata for both public-tunnel and internal-backchannel paths.

**Pattern:** Behavioural tests should prove three things together:
1. **Timeout vs cancellation is explicit** — `statusText`, `timeout.timedOutByUs`, and `timeout.cancellationSource` must distinguish the controller's 10-second deadline from external cancellations.
2. **Internal-backchannel diagnostics stay safe** — browser-visible payloads may name the transport and target path, but the transport base URL must remain masked as `http://localhost:****`.
3. **Operator guidance is actionable** — timeout responses should include `summary`/`nextCheck` text that points to AppHost wiring (`BUSINESSAPP_BACKCHANNEL_URL`) or downstream health checks instead of raw port leakage.

**Key file paths:** `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`, `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`, `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`

### 2026-05-03T22:49:38+01:00: Fastest Environment Variable Configuration Check

**Context:** When downstream API timeouts have already ruled out BusinessApp itself (via diagnostics script internal probe), but public URL still hangs, the fastest triage is checking the exact environment variable TestSite reads at runtime.

**Pattern:** `echo "VAR_NAME = ${VAR_NAME:-not set}"` in the Codespace terminal immediately shows whether configuration is wrong (public URL when internal required) or runtime is stalled.

**Signal:** If `BUSINESSAPP_BACKCHANNEL_URL` is unset or contains a public tunnel URL, that's the fix—no further diagnosis needed. If it's correctly set to `http://localhost:5163`, then the timeout is a deeper Keycloak JWKS or BusinessApp validation issue.

### 2026-05-03T23:00:12+01:00: Safe Transport Diagnostics in API Responses

**Context:** Blathers added structured transport diagnostics to the DownstreamDemo controller responses, exposing whether backchannel was used and whether it was configured, without leaking secrets.

**Pattern:** Test coverage guards three behavioral contracts:
1. **Transport classification**: Responses include `transport.transport` field ("internal-backchannel", "public-tunnel", "public-url") so operators can distinguish failure modes without raw URL inspection
2. **Backchannel wiring signal**: `transport.backchannelPresent` boolean indicates whether `BUSINESSAPP_BACKCHANNEL_URL` env var was configured, independent of whether the request succeeded
3. **Masked internal URLs**: Internal backchannel URLs are rendered as `http://localhost:****` in `transport.transportBaseUrl` to avoid exposing actual ports in browser-visible diagnostics

**Verification:** Added 5 tests in `DashboardLocalEndpointsValidationTests.cs`:
- `DownstreamDemo_ExposesTransportDiagnostics_WhenBackchannelIsConfigured`
- `DownstreamDemo_ExposesTransportDiagnostics_WhenBackchannelIsNotConfigured`
- `DownstreamDemo_IncludesTransportDiagnostics_InErrorResponses`
- `DownstreamDemo_IncludesTransportDiagnostics_OnTimeout`
- `DownstreamDemo_DoesNotExposeRawBackchannelPortInDiagnostics`

All 680 tests pass. The new diagnostics provide enough signal for Codespaces operators to triage timeout root causes (backchannel vs public tunnel) without exposing tokens, ports, or other secrets.


---

## Cross-Agent Update: 2026-05-03T23:08:07Z Scribe Coordination

**Spawn manifest consolidated:** Tangy added 5 behavioural contract tests for transport diagnostics. Blathers implemented response-visible transport path metadata.

**Orchestration record logged:**
- `.squad/orchestration-log/2026-05-03T22:08:07Z-tangy.md`

**Test coverage:**
- Backchannel/public tunnel classification validated
- Timeout/error transport metadata validated
- All 680 Core tests passing
- Masking behavior (localhost:****) verified

**Team coordination complete.** Decisions merged to main registry.


## 2026-05-03 · Transport Diagnostics Validation Spawn

**Spawn outcome:** Defined highest-confidence proof step: rerun diagnostics with fresh PRISM_BEARER_TOKEN and read authenticated backchannel result.

**Session:** transport-diagnostics-landing | Coordinator spawned to validate transport diagnostics feature post-landing (commit 17edf9c).

**Coordination:** Blathers (DevOps) in parallel spawn identified root cause of timeout (backchannel initialization) and recommended refresh.sh.

## Cross-Agent Update: 2026-05-03T22:46:14Z Scribe Consolidation

**Spawn outcomes merged:** Test contracts for backchannel vs. public-tunnel timeout paths and masking behavior validated. All 680 tests passing.

**Orchestration record logged:**
- `.squad/log/2026-05-03T22-46-14Z-timeout-diagnostics.md`

**Decisions merged to main registry:**
- Timeout Diagnostics Must Distinguish Deadline vs Cancellation Without Leaking Backchannel Ports (status: decision, 2026-05-03T23:26:29+01:00)

**Coordination:** Blathers' arrival logging recommendation noted in orchestration log for potential follow-up work.

**Team bookkeeping:** Complete. Product feature (commit 442c5e9) with full test coverage now linked to decision history.

---

## 2026-05-04T00:01:43.530+01:00: Business API Arrival Instrumentation — Test Contract Validation

**Status:** ✅ Documented

**Decision Recorded:** "Business API Arrival Logging Should Carry Safe Cross-Service Correlation" (2026-05-03T23:46:52.875+01:00, PROPOSED)

**Validation:** Test contract in DashboardLocalEndpointsValidationTests.cs validates:
- Trace ID capture in pre-auth and handler logs
- `X-Prism-Caller-TraceId` header forwarding
- Correlation matching without exposing tokens or internal URLs

**Safety Model:** Read-only diagnostic headers, no auth/PII exposure. Follows existing pattern of safe operator instrumentation.

---

## 2026-05-03T23:20:46Z: Workflow-Start vs Direct API Auth Coverage

**Status:** 🔍 Spawned (background)

**Outcome:** Reproducing and covering authentication differential between workflow-start and direct API endpoints.

**Orchestration Log:** `.squad/orchestration-log/2026-05-03T23:20:46Z-tangy.md`

**Scope:** Build test contracts for workflow-start auth path and validate coverage gaps vs direct API access patterns.

---

## 2026-05-04: Workflow 401 Regression Investigation

**Timestamp:** 2026-05-04  
**Status:** ✅ Complete — regression tests added

### Context

Commit 0904810 fixed `PrismSigningKeyCache.BackchannelRewritingDocumentRetriever` to use host-based URI matching (instead of origin `StartsWith`). This makes the Business App correctly fetch JWKS when Keycloak returns a `jwks_uri` using the public hostname with the backchannel port (e.g. `http://codespace-8443.app.github.dev:39517/...`). After the fix, the TestSite's "Call Mock Business App API" button works in Codespaces, but the workflow pages (e.g. "Start Payment Demo") still returned "Business App error (HTTP 401)".

### Behavioral Gap Found

Two layered failure modes produce the same "Business App error (HTTP 401)" surface in `BusinessAppWorkflowClient`:

**Mode 1 — Null auth header silently dropped:**  
`BusinessAppWorkflowClient.CreateClientAsync` only attaches the Authorization header when `GetAuthorizationHeaderAsync` returns non-null. If `PrismContext.GetAuthorizationHeaderAsync` returns null (e.g. `CurrentTenant` not resolved by `PrismTenantMiddleware`, tenant/principal mismatch), the request is sent unauthenticated. Business App JWT middleware rejects it with 401. On the retry (`forceRefresh: true`), `RefreshTokenAsync` also returns null because `CurrentTenant` is null. Second request also has no auth header → 401 → error envelope.

**Mode 2 — Application-level `Results.Unauthorized()` in workflow handlers:**  
`/api/workflow/{key}/current` and `/api/workflow/{key}/advance` return `Results.Unauthorized()` (HTTP 401) when `GetPrismTenant` returns null or email is empty. This is the *same HTTP 401* as JWT middleware rejection. `/api/backoffice/me` uses `Results.Problem()` (HTTP 500) for the same condition. From the TestSite client's perspective these two failure modes are indistinguishable: both surface as "Business App error (HTTP 401)" in `ReadEnvelopeAsync`.

### Tests Added

Added 3 regression tests to `BusinessAppWorkflowClientTests.cs`:

1. **`GetCurrentAsync_SurfacesErrorEnvelope_WhenAuthHeaderIsNull`** — verifies that when `GetAuthorizationHeaderAsync` returns null, no Authorization header is sent on either the initial request or the retry, and the Business App 401 surfaces as a `BUSINESS_APP_ERROR` error envelope (not thrown).

2. **`GetCurrentAsync_AttemptsTokenRefreshOnce_WhenBusinessAppReturns401`** — verifies that on Business App 401, `GetAuthorizationHeaderAsync(forceRefresh: true)` is called exactly once (no infinite retry loop).

3. **`GetCurrentAsync_SurfacesErrorEnvelope_NotExceptionThrown_WhenBothRequestsReturn401`** — verifies exactly 2 HTTP attempts (initial + 1 retry), and that double-401 surfaces as error envelope, never thrown.

Refactored `BuildClient` helper into `BuildClientWithContextMock` to allow Moq verification of `IPrismContext` interactions.

### Comment Added

Added a TODO comment to `MockBusinessApp/Program.cs` lines 127-134 documenting the `Results.Unauthorized()` vs `Results.Problem()` inconsistency across workflow vs backoffice endpoints, flagging it for Blathers to resolve.

### Key Files

- `src/UmbracoPrism.Core.Tests/BusinessAppWorkflowClientTests.cs` — 3 new regression tests
- `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs` — null auth header silent drop (line 179)
- `src/UmbracoPrism.MockBusinessApp/Program.cs` — `Results.Unauthorized()` in workflow handlers (line 127)
- `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs` — `BackchannelRewritingDocumentRetriever` (fixed in 0904810)
- `src/UmbracoPrism.Core/Middleware/PrismTenantMiddleware.cs` — sets `CurrentTenant` from request host

### Learnings

**Two distinct 401 sources look identical at the surface:** JWT middleware 401 (no valid token) and application-level `Results.Unauthorized()` (tenant/email resolution failed post-auth) both surface as "Business App error (HTTP 401)". The `[PRISM AUTH FAILED]` console log from `OnAuthenticationFailed` distinguishes them: if present, it's JWT validation. If absent, it's the application guard. Looking for this log is the fastest triage step.

**Null auth header is silent and dangerous:** `CreateClientAsync` doesn't log or throw when auth is null — it simply omits the header. This means token-resolution failures in `PrismContext` produce unauthenticated requests silently. Tests now document this contract explicitly.

**The JWKS fix (0904810) is correct but insufficient alone:** If `PrismTenantMiddleware` fails to resolve the tenant for the workflow page request (e.g. host not in tenant registry, or principal/tenant mismatch), the workflow path fails regardless of JWKS cache state.
