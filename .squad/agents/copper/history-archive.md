# Copper — History Archive

**Date:** 2026-06-04T21:31:07Z  
**Original size:** 18704 bytes  
**Archived:** Sessions prior to 2026-06-04

This file contains the full session history archived to keep the active history.md under 15KB. Refer to this file for prior session details, learnings, and context.

---

# Copper — History

**Recent summary:** Security engineer reviewing Codespaces URL derivation, backchannel refresh token injection, and bedrock invariant compliance.

**Full history:** See `history-archive.md` for sessions prior to 2026-05-01.

---

## 2026-05-02 — PR #46 Security Verdict (`fix/codespaces-invalid-grant-refresh`)

**Date:** 2026-05-02
**Verdict:** ✅ **APPROVE**

**Context:** PR #46 extends the Codespaces backchannel pattern to refresh-token grant requests. Keycloak 26 with `--proxy-headers xforwarded` derives its canonical issuer URL scheme from the `X-Forwarded-Proto` header. The backchannel refresh POST to `http://localhost:8080` carried no forwarding headers, so Keycloak computed an `http://...` issuer. The stored refresh token's `iss` claim was `https://...` (set when the token was originally issued through YARP, which does forward headers). Keycloak's issuer comparison on the refresh token grant detected the scheme mismatch and returned `invalid_grant`.

**Solution:** `PrismContext.RefreshTokenAsync` now derives `X-Forwarded-Proto` and `X-Forwarded-Host` from `OidcAuthority` (the public HTTPS URL) and passes them as optional `requestHeaders` to `IPrismTokenRefreshService.RefreshAsync`. `PrismTokenRefreshService` applies these headers to the `HttpRequestMessage` before sending.

## Bedrock Invariants — All Pass

1. ✅ **HTTPS metadata required** — `RequireHttpsMetadata` not touched; guarded by existing test.
2. ✅ **Validation flags untouched** — `ValidateIssuer/Audience = true` at `PrismOidcConfiguration.cs:171-172, 184-185`; `ValidateLifetime = true` preserved; `ValidateIssuerSigningKey` defaults preserved.
3. ✅ **Issuer/audience DB-sourced** — `validationParameters.ValidIssuer = tenant.OidcAuthority`; no request-derived fallback added.
4. ✅ **Dual gating preserved** — `if (isDevelopment && !string.IsNullOrEmpty(backchannelBase))`; forwarding headers assigned only inside that branch; `backchannelForwardingHeaders` is `null` outside.
5. ✅ **No transport-derived identity** — `X-Forwarded-Proto/Host` derived from `new Uri(CurrentTenant.OidcAuthority!...)`; never from `HttpContext.Request`, `Host` header, or env var.
6. ✅ **Headers scoped to backchannel only** — `backchannelForwardingHeaders` is local, set only when rewrite fires, and passed to `RefreshAsync` alongside the rewritten endpoint.
7. ✅ **`IsRepoOwnedLocalDemoTenant` gate untouched** — Unchanged.
8. ✅ **Group E tests present** — Three new tests in `BackchannelRewriteTests.cs` cover positive case, no-rewrite negative case, and critical "scheme must come from authority not backchannel" anti-regression.

**Notes:** `TryAddWithoutValidation` is correct here (these are non-standard request headers); no header-injection risk because values come from a `Uri`-parsed DB string, not user input. No production `.app.github.dev` seeding introduced; PR is transport-only.

**Verdict:** No bedrock violations. Ship it.

---

## 2026-05-02 — PR #45 Security Review: Codespaces URL Derivation Fix

**Verdict:** ✅ APPROVED WITH NOTES

**Context:** PR #45 fixes Codespaces URL derivation to handle both the legacy `{CODESPACE_NAME}-{port}.app.github.dev` and new regional `{token}-{port}.{region}.app.github.dev` URL schemes, using `gh codespace ports` as the authoritative source.

**Bedrock Preserved:**
- ✅ RequireHttpsMetadata untouched; BackchannelRewriteTests security gate continues passing.
- ✅ ValidateIssuer/Audience re-enabled in IssuerSigningKeyResolver from DB values, not request headers.
- ✅ Backchannel dual gate unchanged (codespaceName env var gate + IsDevelopment() throw-guard in TestSite).
- ✅ IsRepoOwnedLocalDemoTenant semantics unchanged for non-Codespace traffic (hostname check uses tenant.Hostname from DB).
- ✅ JWT issuer/audience strings come from tenant DB row, not request. New regression test confirms this for regional URL scheme.

**Soft Notes Raised:**
1. `TenantService` LIKE fallback (`%.app.github.dev`) has no ORDER BY — non-deterministic row selection if multiple .app.github.dev rows exist (orphan rows from token rotation). Not exploitable; could cause dev confusion. **Recommendation:** Add `ORDER BY Id DESC LIMIT 1` or a comment acknowledging non-determinism.
2. LIKE fallback not gated by IsDevelopment() in TenantService. Defense-in-depth concern only (seeder is already dev-gated so no production .app.github.dev rows can exist). **Recommendation:** Add an `IsDevelopment` guard in `TenantService` for this fallback path.

**Key Learnings:**
- Request.Host override from a static env var (TESTSITE_PUBLIC_URL) is SAFER than reading the inbound Host header — it overrides whatever the client sends, making host-header injection impossible on that path.
- The `gh codespace ports` startup-only pattern (ProcessStartInfo without shell, JSON.TryCreate downstream) is injection-safe and provides the correct authoritative URL for both Codespace URL schemes.
- When reviewing hostname-based tenant fallbacks, trace whether the returned tenant.Hostname (from DB) or the inbound request hostname is used for OIDC configuration downstream. In this PR, DB values are always the source — the fallback is config-routing only.
- All bedrock invariants remain intact despite the new regional URL scheme. Origin-prefix matching in `BackchannelRewritingDocumentRetriever` survives the change because it anchors on the configured `OidcAuthority` origin — agnostic to URL form.

**Test Results:** 647/647 passed (0 failures).

---

## Core Context

This agent specializes in security engineering, threat modeling, and bedrock invariant validation for the Prism project.

**Key domains:** Threat modeling, security review, authentication/authorization, cryptography, compliance, incident analysis, security testing

**Bedrock Invariants (immutable security contract):**
1. `RequireHttpsMetadata = true` (never disabled, never conditional)
2. `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateIssuerSigningKey = true`
3. Backchannel rewrite dual-gated (`KEYCLOAK_BACKCHANNEL_URL` env var + `IsDevelopment()`)
4. Tenant resolution must NOT trust hostname suffix for security decisions
5. No transport-derived identity (hostname, headers, env vars never become claims)
6. `IsRepoOwnedLocalDemoTenant` unchanged for non-Codespace traffic
7. JWT issuer/audience strings sourced from configured authority, never from request

**Review discipline:** Pre-merge security assessment on all PRs touching auth, OIDC, cryptography, or infrastructure.

## 2026-05-03 — MockBusinessApp 401 Invalid Token Investigation

**Date:** 2026-05-03  
**Context:** User reported TestSite is back up but downstream call to MockBusinessApp returns `HTTP 401 Unauthorized` with `WWW-Authenticate: Bearer error="invalid_token"`.

### Investigation

**Scope:** Reviewed downstream token trust chain, identified most likely root cause, determined stale runtime vs code-side validation mismatch.

**Key Findings:**

1. **Stale Runtime Confirmed (HIGH CONFIDENCE)**
   - Aspire MockBusinessApp (PID 28308, port 7245): Started 09:45:37, running 2h+ at investigation time
   - TestSite: Recently restarted (user confirmed "back up")
   - Last auth code change: bf1c6e7 (2026-05-02 11:23:54) — 24+ hours before investigation
   - Classic pattern: TestSite restarted → fresh runtime; MockBusinessApp NOT restarted → stale runtime

2. **Token Trust Chain Verified (Code-Side)**
   - Browser → Keycloak HTTPS proxy (`https://localhost:8443/realms/prism-dev`)
   - Token issued with `iss: "https://localhost:8443/realms/prism-dev"`
   - MockBusinessApp `appsettings.json` OidcAuthority: `"https://localhost:8443/realms/prism-dev"` ✅
   - ClientId: `"prism-client"` ✅
   - Issuer validator (line 115-126) matches token issuer against `t.OidcAuthority` ✅
   - Audience validator (line 146-163) accepts `aud` OR `azp` claim (Keycloak pattern) ✅

3. **Backchannel JWKS Fetch Pattern Verified**
   - `KEYCLOAK_BACKCHANNEL_URL` only set in Codespaces (AppHost line 145)
   - Local dev: MockBusinessApp fetches JWKS from HTTPS proxy (correct)
   - Keycloak HTTP backchannel advertises `http://localhost:8080/realms/prism-dev` as issuer
   - Keycloak HTTPS proxy advertises `https://localhost:8443/realms/prism-dev` as issuer
   - Code correctly separates issuer validation (uses `OidcAuthority` config) from JWKS fetch (uses backchannel when set)

4. **Manual Fresh Instance Detected**
   - User started manual MockBusinessApp on port 9245 at 12:02 with `KEYCLOAK_BACKCHANNEL_URL` set
   - Matches "live-oidc-401-stale-runtime" skill pattern: fresh comparison instance

### Recommendation

**Primary:** Restart MockBusinessApp Aspire resource (port 7245) to pick up current runtime state.

**If restart doesn't fix:**
- Real code-side issue (issuer/audience/signing key mismatch)
- Check `OnAuthenticationFailed` console diagnostics (PrismAuthExtensions.cs lines 27-68) for actual token `iss`/`azp` vs configured authorities
- Verify token is actually reaching MockBusinessApp (not being stripped by middleware)

### Missing Diagnostics

**Console output from MockBusinessApp's OnAuthenticationFailed handler would materially improve this failure mode:**
- Actual token `iss` claim
- Actual token `azp` claim  
- Configured `OidcAuthorities` from appsettings
- `KEYCLOAK_BACKCHANNEL_URL` env var status

**Current diagnostics are good** (lines 27-68), but user didn't provide the output. If stale runtime is ruled out, these logs are the next step.

### Relevant Skills Applied

- `.squad/skills/live-oidc-401-stale-runtime/SKILL.md` — Pattern matched: persistent 401 after partial stack restart
- `.squad/skills/generic-oidc-downstream-bearer-validation/SKILL.md` — Verified trust chain, issuer/audience validators, azp claim handling

### Learnings

1. **Aspire child processes can outlive parent orchestrator restarts** — TestSite restart doesn't guarantee MockBusinessApp restart
2. **Keycloak backchannel issuer mismatch is handled correctly** — Code separates issuer validation (config-sourced) from JWKS fetch (backchannel-aware)
3. **OnAuthenticationFailed diagnostics are good but need to be surfaced** — User didn't provide console output; consider logging to structured sink or test harness
4. **Manual fresh instance is a strong signal** — User started port 9245 comparison instance; suggests they're following the "stale runtime" skill pattern


## 2026-05-03: Spawn Manifest — MockBusinessApp 401 Trust Chain Verification

**Timestamp:** 2026-05-03T11:07:19.866Z  
**Status:** ✅ Verified; 📋 Recommendation issued

### Investigation Summary

Reviewed HTTP 401 `invalid_token` from MockBusinessApp when called from live Codespaces dashboard.

**Root Cause (HIGH CONFIDENCE): Stale Runtime**
- MockBusinessApp running 2h+, predates recent auth code changes
- TestSite recently restarted (code changes picked up)
- Runtime mismatch: fresh TestSite + stale MockBusinessApp = validation failure
- Pattern matches `.squad/skills/live-oidc-401-stale-runtime/`

### Trust Chain Verification: ✅ VERIFIED

All code-side authentication components correct:
- OidcAuthority configured correctly
- ClientId matches (prism-client)
- Issuer/audience validators correctly implemented (PrismAuthExtensions.cs lines 115–163)
- Backchannel JWKS fetch correctly scoped to Development + env var guard
- No vulnerabilities or configuration errors detected

### Recommendation

**Primary Action:** Restart MockBusinessApp Aspire resource (port 7245)

**If restart doesn't fix:**
1. Capture `OnAuthenticationFailed` console diagnostics
2. Compare actual token `iss`/`azp` claims vs configured values
3. Verify token reaches MockBusinessApp (not stripped by middleware)
4. Check for typo in ClientId or OidcAuthority

**Optional Improvements:** Structured logging sink (Development-gated) for easier future debugging

### Coordination

- Blathers: Deployed enhanced diagnostics (token kid, environment, JWKS URL)
- Brewster: Fixed Codespaces URL regression
- User directive: Diagnose against actual failing runtime (live Codespaces), not assumptions


---

**2026-05-03T11:58:20Z:** Dispatched as Copper-6 — Security-review downstream token rejection (agent: Copper). Concluded: real downstream bearer-token rejection at MockBusinessApp; no code changes required. Evidence gaps noted: missing /debug/auth output, PRISM AUTH FAILED log block, and runtime build/start evidence to be collected next session. Core tests passing.


## 2026-05-03 — Scribe: Codespaces Fixes Decisions Recorded

Scribe recorded codespaces-dashboard-and-auth-fixes work in decisions.md. Copper review of MockBusinessApp auth fix and port 17214 decision has been documented.

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## Learnings

### 2026-05-15T06:35:47.013+01:00

- PASA-style bereavement journeys should separate **channel proof** (magic link/OTP) from **authority proof** (relationship, evidence, reviewer judgement).
- For one-off third-party pension notifications, a **case-scoped verified session** is a better default than forcing permanent account registration.
- Pre-verification notifier journeys should expose only **generic case states** and never confirm pension membership or benefits until the reviewer-backed threshold is met.

## 2026-05-15: PASA Death Process Security Decision

Produced security decision on staged assurance and case-scoped access. Defined verified contact-channel (magic link/OTP), case-scoped identity, reviewer-backed step-up. Established fail-closed data visibility until verification. Decision emphasizes separation of channel proof from authority/member-match. Merged to shared registry.


## 2026-05-30 — Workflow Editor Reset Security Review (squad/82, HEAD a251bcd)

**Scope:** CIA + tenant isolation review after the proposal-diff/preview removal and gateway-first authoring rebase. Read-only. No code changes.

### Top findings

1. **CRITICAL — `/api/workflow-authoring/*` is fully unauthenticated.** `MapPrismWorkflowEditor` adds no `.RequireAuthorization()`; only a Development CORS policy is applied. MockBusinessApp's non-Dev `/admin` 404 middleware does NOT match `/api/workflow-authoring`, so the endpoints survive a non-Dev deployment. Comment at `Program.cs:139` explicitly says "no auth required". Combined with `AllowAnyOrigin` in Dev → CSRF-trivial.
2. **CRITICAL — Authorship is self-asserted.** `/apply` reads `approver: string` from the request body (`ApplyWorkflowRequest.cs:8`); provenance store writes whatever string the caller supplied. No claims-based identity. Authorship spoofing is the default behaviour.
3. **HIGH — Path traversal in all three filesystem stores.** `{key}` route param is interpolated unsanitised into `Path.Combine(basePath, $"{key}.workflow.json")` (FilesystemAuthoredWorkflowStore:36/81/110/123), and into provenance filenames (FilesystemWorkflowAuthoringProvenanceStore:19-20). `key="../../etc/passwd"` escapes the base directory. MockBusinessApp dodges this only because it pre-registers InMemory stores — downstream hosts that follow the default DI path are exposed.
4. **MEDIUM — `update-transition` is a covert insert path.** WorkflowPatchService:187-195 falls through to `transitions.Add(updated)` when no match exists. Combined with no auth, any caller can insert arbitrary edges and rely only on the projector to reject; projector relies on PROJ141/142 (the new rules), so the schema is now the *only* gate. Defence-in-depth issue.
5. **MEDIUM — Path-traversal in patch op stage selector.** WorkflowPatchService:218 treats `op.Path` segments like `parts[1]` as a literal stage key without sanitisation — fine for the in-memory model but means audit logs become attacker-controlled strings.
6. **LOW — PROJ140 sentinel scoping is fragile.** `AuthoredStage.LegacyWaitingPayload` only flips `_hasLegacyWaitingPayload` when JSON value is non-null; `{ "waiting": null }` slips past. Not a present-day exploit (null payload carries nothing), but the design is property-name-coupled — any future legacy alias added under a different JSON property will silently bypass PROJ140.
7. **LOW — Filesystem path disclosure in apply response.** `savedPath`/`provenancePath` returned to the client expose absolute server paths.

### CIA verdict

- **Confidentiality:** unchanged (response leaks server paths; no PII in ProposalEnvelope itself).
- **Integrity:** **regressed** by the auth + spoofable-approver combination. The reset removed the preview step that previously gave a (weak) two-step ceremony.
- **Availability:** unchanged; validators do recursive walks but parameter-schema depth is bounded by JSON depth limits in System.Text.Json defaults (64).

### Net attack surface

Surface area shrank (one endpoint, one service removed) but the *remaining* surface is more dangerous because (a) authorship is now solely carried by the body-supplied `approver`, with no agentic preview/staging step in front, and (b) schema validators are doing more load-bearing work after PROJ140/141/142 took on integrity duties that previously lived partly in the now-deleted SemanticDiff path.

### Action items emitted

Detailed findings written to `.squad/decisions/inbox/copper-editor-reset-security-review.md` for Scribe pickup.

## Learnings

- **Lit `html``` tag escapes interpolations.** Inspector + outline render lane / stage / gateway / waiting-message strings via Lit templates with no `unsafeHTML` anywhere in `src/UmbracoPrism.Client/src/workflow-editor/`. XSS surface in the editor authoring shell is currently nil even though every field flows from authored JSON.
- **`TryAddSingleton` masks default-DI vulnerabilities.** `AddPrismWorkflowEditor` registers filesystem stores via `TryAddSingleton`, so a reference host that pre-registers in-memory stores (MockBusinessApp) appears safe while downstream hosts inherit the path-traversal default. Always audit the *default* registration, not just the call site you tested.
- **Legacy JSON shims need a property-name allowlist, not a value-presence check.** `LegacyWaitingPayload` keys off `"waiting"` being present and non-null. Any new field name that future authors use to carry waiting metadata (e.g. `"waitConfig"`) bypasses PROJ140 without anyone noticing — the sentinel should be replaced with a positive allowlist of stage properties.
- **`{key}` route params on filesystem-backed stores need explicit sanitisation.** This codebase has now repeated the unsanitised-`{key}` → `Path.Combine` pattern across three stores. Worth a repository-wide rule.
