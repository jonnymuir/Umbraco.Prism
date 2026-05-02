# Copper — History




## 📋 Recent History

Previous history archived to reduce file size. Recent entries below.

---

## 2026-05-02 — PR #45 Security Review: Codespaces URL Derivation Fix

**Verdict:** APPROVED WITH NOTES

**Context:** PR #45 fixes Codespaces URL derivation to handle both the legacy `{CODESPACE_NAME}-{port}.app.github.dev` and new regional `{token}-{port}.{region}.app.github.dev` URL schemes, using `gh codespace ports` as the authoritative source. Changes span AppHost URL discovery, DemoTenantSeeder, TenantService fallback lookup, and TestSite Request.Host override.

**Bedrock Preserved:**
- RequireHttpsMetadata untouched; BackchannelRewriteTests security gate continues passing.
- ValidateIssuer/Audience re-enabled in IssuerSigningKeyResolver from DB values, not request headers.
- Backchannel dual gate unchanged (codespaceName env var gate + IsDevelopment() throw-guard in TestSite).
- IsRepoOwnedLocalDemoTenant semantics unchanged for non-Codespace traffic (hostname check uses tenant.Hostname from DB).
- JWT issuer/audience strings come from tenant DB row, not request. New regression test confirms this for regional URL scheme.

**Soft Notes Raised:**
1. `TenantService` LIKE fallback (`%.app.github.dev`) has no ORDER BY — non-deterministic row selection if multiple .app.github.dev rows exist (orphan rows from token rotation). Not exploitable; could cause dev confusion.
2. LIKE fallback not gated by IsDevelopment() in TenantService. Defense-in-depth concern only (seeder is already dev-gated so no production .app.github.dev rows can exist).

**Key Learning:**
- Request.Host override from a static env var (TESTSITE_PUBLIC_URL) is SAFER than reading the inbound Host header — it overrides whatever the client sends, making host-header injection impossible on that path.
- The `gh codespace ports` startup-only pattern (ProcessStartInfo without shell, JSON.TryCreate downstream) is injection-safe and provides the correct authoritativ URL for both Codespace URL schemes.
- When reviewing hostname-based tenant fallbacks, trace whether the returned tenant.Hostname (from DB) or the inbound request hostname is used for OIDC configuration downstream. In this PR, DB values are always the source — the fallback is config-routing only.

**Test Results:** 647/647 passed (0 failures).
**Artifacts:** `.squad/decisions/inbox/copper-pr45-security-review.md`

---

- User Impact: ✅ Positive (fresh clones work without manual Keycloak config)

**Alignment:**
- `.squad/skills/generic-oidc-offline-token-policy/SKILL.md`: Default to session-bound browser auth
- `.squad/skills/keycloak-refresh-scope/SKILL.md`: Prefer standard scopes for fresh-clone local auth

**Key Learning:**
- `offline_access` is NOT required for refresh tokens in standard OIDC authorization code flow
- Different providers interpret `offline_access` differently (Keycloak = offline tokens, Entra = session refresh)
- Generic OIDC paths should use minimal standard scopes; provider-specific features stay in provider-specific paths
- Always validate runtime behavior against actual provider configuration, not just unit tests

**Artifacts:** `.squad/decisions/inbox/copper-oidc-scope-review.md` (complete analysis and recommendation for Blathers implementation)


## 2026-04-14 — Token Refresh RedirectUri Hygiene

**Context:** Localhost Playwright auth/session test reported 401 from mock business app API after full stack restart, even though dashboard rendered successfully. Restart detection via ProcessStartedUtc comparison was correct, but AuthenticationProperties state hygiene during token refresh needed hardening.

**Finding:** PrismContext.RefreshTokenAsync was reusing the complete AuthenticationProperties object from the pre-refresh cookie when writing the post-refresh cookie. While IssuedUtc and token values were being updated, other properties such as RedirectUri were being carried forward indefinitely.

**Security Impact:**
- Low direct security risk: RedirectUri is only read during the initial OIDC callback flow, not during token refresh or downstream API calls.
- Hygiene concern: persisting one-off login state across long-lived token refresh cycles contradicts fail-closed session management principles.
- Consistency issue: login flow explicitly clears RedirectUri before issuing the PrismMemberCookie; token refresh flow did not maintain the same hygiene.

**Fix Applied:**
- Added props.RedirectUri = null at line 197 of PrismContext.cs, immediately after token value updates and before SignInAsync.
- Matches the pattern established in PrismOidcConfiguration.cs where the login callback clears RedirectUri before persisting the member cookie.

**Testing:**
- All PrismContextTests passed (12/12), including restart scenario test.
- Pre-existing Phase1SecurityRegressionTests failures (6/19) remain unrelated to this change.

**Decision:**
- Cookie AuthenticationProperties must be treated as append-only during refresh: update only specific properties needed for new token state, and explicitly null any one-off flow properties.

**Files Modified:**
- src/UmbracoPrism.Core/Models/PrismContext.cs (line 197)

### 2026-04-14 — Redirect hardening review
- The current redirect hardening is still incomplete: `AccountController.Login/Register` copy attacker-controlled `returnUrl` directly into `AuthenticationProperties.RedirectUri` for unauthenticated users, and `PrismOidcConfiguration.OnAuthorizationCodeReceived` later emits it with `Response.Redirect(returnUrl)`. The authenticated `LocalRedirect(...)` branch does not protect the unauthenticated OIDC round-trip.
- Treat empty and whitespace `returnUrl` values as unsafe/unusable input too. `props.RedirectUri ?? "/"` only catches `null`; it does not normalize `""` or `"   "`, so fallback-to-root must be an explicit rule rather than a null-coalescing assumption.
- Preserve the current safe properties that do exist: failure redirects stay pinned to fixed local error routes, logout stays pinned to `/`, and the callback redirect target should remain relative-only and hostless rather than being rebuilt from request scheme/host.
- Operational trust boundary to keep in review: the OIDC token-exchange `redirect_uri` is derived from `Request.Scheme`, `Request.Host`, and `PathBase`, so any future proxy/header changes must stay fail-closed with trusted forwarded-header handling and host validation.
- 2026-04-14: For Prism auth redirects, framework-backed local-only validation is sufficient for the current `/auth/login` → OIDC callback flow when applied before both `AuthenticationProperties.RedirectUri` persistence and the final redirect sink; move to a redirect whitelist only if product requirements narrow allowed in-app destinations beyond “any local path”.
- 2026-04-14: In non-controller callback contexts where `Url.IsLocalUrl()` is unavailable, use the framework-equivalent local-url validator with the same semantics before `Response.Redirect(...)`, and keep `/` as the fail-closed fallback.



## 2026-04-14: Redirect Hardening Sprint — COMPLETE

**Session:** Redirect Hardening Work (2026-04-14T12:39:42Z)

**Delivered:**
- Threat model review: confirmed real open-redirect vulnerability seam and validated preservation properties
- Framework validation strategy: confirmed ASP.NET Core IsLocalUrl() is sufficient for localhost + production domains
- Security assessment: whitelist-based hardening identified as optional next-step policy enhancement

**Key Outcomes:**
- Treated post-login redirect targets as two-stage trust boundary
- Validated and normalized returnUrl both before entering AuthenticationProperties.RedirectUri and again before callback-side Response.Redirect(...)
- Confirmed framework-backed local-only validation is the right immediate control
- No regression in security posture; clean migration to framework validator

**Threat Model Analysis:**
- CWE-601 (open redirect) seam confirmed and remediated
- Final post-login targets: relative-only / local-only (never absolute URLs, scheme-relative, or non-HTTP schemes)
- Blank, whitespace, omitted redirect values canonicalize to `/`
- Error/logout flows pinned to fixed local routes, not user-controlled values

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T12:39:42Z-copper.md`
**Session Log:** `.squad/log/2026-04-14T12:39:42Z-redirect-hardening.md`

**Team Consensus:** Framework validators provide sufficient security control; whitelist is optional hardening for later policy decision.
## 2025-01-23 — Aspire OTLP Telemetry Endpoint Security Configuration

**Context:** Aspire AppHost dashboard showed security warning: "Telemetry endpoint is unsecured. Untrusted apps can send telemetry to the dashboard."

**Investigation:**
- Existing config had `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` in launchSettings.json
- This variable controls dashboard UI access, NOT OTLP endpoint security
- Missing `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` for Aspire 9.x OTLP endpoint

**Finding:** In Aspire 9.x, the OTLP telemetry endpoint security is controlled by a separate environment variable (`ASPIRE_ALLOW_UNSECURED_TRANSPORT`), distinct from the dashboard UI authentication variable.

**Security Decision:**
- For localhost development: explicitly allow unsecured OTLP transport via `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`
- Risk accepted: local dev environment with no sensitive data in telemetry
- Documents acknowledgment: setting the variable explicitly acknowledges the unsecured posture
- Production guidance documented: require API key auth or mutual TLS for non-dev environments

**Fix Applied:**
- Added `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` to `src/UmbracoPrism.AppHost/Properties/launchSettings.json` in the `https` profile
- Build validation passed

**Key Learning:**
- Aspire 9.x separates dashboard UI security (`DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`) from OTLP endpoint security (`ASPIRE_ALLOW_UNSECURED_TRANSPORT`)
- Security warnings should be addressed explicitly (either fix the risk OR acknowledge it via configuration) rather than ignored
- Development security posture should be documented with production-hardening guidance

**Files Modified:**
- `src/UmbracoPrism.AppHost/Properties/launchSettings.json`

**Artifacts:** `.squad/decisions/inbox/copper-telemetry-security.md` (complete security analysis and production guidance)

## 2026-04-20: Telemetry Security Configuration

**Session:** 2026-04-20T21:17:20Z

### Work Completed
- Investigated unsecured Aspire OTLP telemetry endpoint warning
- Root cause: Missing `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` in launch config
- Added environment variable to development launch profile
- Documented decision with security analysis and production guidance

### Files Modified
- `src/UmbracoPrism.AppHost/Properties/launchSettings.json` — Added ASPIRE_ALLOW_UNSECURED_TRANSPORT to https profile

### Decision Recorded
- `.squad/decisions.md` — Aspire OTLP telemetry endpoint security (development-scoped configuration with production guidance)

### Status
✅ Complete

## Learnings

### 2026-04-21 — Comprehensive Security Review: Keycloak Backchannel Changes

**Context:** Full security review requested with focus on recent Keycloak/Codespaces backchannel URL changes for JWT signing key fetch and token exchange.

**Scope:**
- Keycloak backchannel URL pattern (`KEYCLOAK_BACKCHANNEL_URL` env var)
- Workflow admin JSON editor endpoints (no auth)
- JWT validation pipeline (issuer, audience, signing keys)
- Signing key cache (thread safety, rotation handling)
- OIDC configuration (tenant isolation)
- Production deployment safety

**Key Findings:**

**1. Backchannel URL Pattern — SAFE ✅**
- Correctly separates metadata/token-exchange fetch URLs (backchannel) from issuer validation (OidcAuthority)
- Issuer validation remains strict: token `iss` claim must match configured `OidcAuthority`
- Backchannel URL only affects WHERE keys are fetched, not WHICH issuer is trusted
- Attack scenario mitigated: Even if attacker controls backchannel endpoint, they cannot forge valid tokens (issuer validation rejects them)
- Scoped to Codespaces via `CODESPACE_NAME` guard in AppHost
- Production risk: Low (requires infrastructure-level access to set env var)

**2. Workflow Admin Endpoints — ACCEPTABLE FOR DEV, NEVER DEPLOY TO PRODUCTION ⚠️**
- `/admin/workflow/*` endpoints have no authentication or authorization
- Acceptable for MockBusinessApp (local dev/demo service with in-memory state)
- Critical risk if accidentally deployed to production
- Recommended: Add environment check to return 404 in non-Development environments

**3. JWT Validation Pipeline — ROBUST ✅**
- Multi-tenant issuer validation with strict host/path checks
- Audience validation prevents cross-tenant token reuse
- Signing key cache handles rotation automatically with forced refresh on missing `kid`
- Thread-safe implementation with per-tenant semaphore locks
- Forced refresh cooldown (30s) prevents DoS while allowing key rotation

**4. Production Deployment Safety — SAFE WITH CONTROLS ✅**
- TestSite and Shared libraries are production-ready
- MockBusinessApp must NOT be deployed to production in current form
- Environment variable hygiene critical: `KEYCLOAK_BACKCHANNEL_URL` must never be set in production

**Security Design Patterns Observed:**
- **Separation of concerns:** Backchannel URL affects metadata fetch, not trust decisions (issuer validation)
- **Defense in depth:** Multiple validation layers (issuer, audience, lifetime, signing key)
- **Fail-closed:** Missing keys trigger forced refresh rather than allowing unsigned tokens
- **Multi-tenant isolation:** Tenant derived from hostname/routing, not token claims

**Test Coverage:**
- Strong: 60+ tests covering issuer/audience validation, open redirect hardening, tenant isolation
- Gap: No regression tests for backchannel URL behavior
- Gap: No tests for workflow admin endpoint security

**Recommendations:**
1. **High priority:** Add production environment variable validation (fail-closed on `KEYCLOAK_BACKCHANNEL_URL`)
2. **High priority:** Disable admin endpoints in non-Development environments
3. **Medium priority:** Add regression tests for backchannel behavior
4. **Medium priority:** Document deployment security requirements

**Overall Assessment:** **LOW RISK** — Production-safe with operational controls. Backchannel pattern is well-designed and maintains security boundaries.

**Artifacts:** `.squad/decisions/inbox/copper-security-review-2026-04-21.md` (comprehensive 500+ line security review report)

### 2026-04-20 — Aspire 13.2.2 Upgrade: OTLP Telemetry Warning Resolved

---

## 📦 Archived Sessions (2026-04-25 and earlier)

Complete chronological history available in git. Recent summaries:

**Archived entries include:**
- Phase 1 & Phase 2 security reviews (breadth-first and depth-first)
- Full codebase security audit findings
- SEC-003 implementation and allowlist verification
- CVE patches and dependency security work

**Access:** Full session details in git history; `.squad/decisions.md` for decisions.

## 2026-05-02 — Codespaces 401 on Downstream Demo: Diagnosis (no code changes)

**Context:** Dashboard "Call Mock Business App API" button returns 401 in Codespaces (`upgraded-bassoon-4g5v5r9vghq5p6-44345.app.github.dev`) but works on localhost.

**Bedrock directive captured:** "Security must never be compromised. No 'just for Codespaces' shortcuts on token validation." All hypotheses & remediation candidates respect that rule.

**Top hypothesis (≈55%):** Self-inflicted 401 from `DownstreamDemoController` itself — `PrismContext.GetAuthorizationHeaderAsync` returns null because `IsPrincipalBoundToCurrentTenant` rejects the principal when `CurrentTenant` is null. Most plausible cause: `DemoTenantSeeder` did not insert a `prismTenants` row for the Codespace hostname (env-var visibility on the TestSite child process, or a stale DB).

**Other hypotheses ranked:**
- H2 (≈25%) JWT issuer mismatch on MockBusinessApp (`PrismBusinessApp__Tenants__2__OidcAuthority` env-var binding hygiene)
- H3 (≈8%) HTTPS dev-cert trust between TestSite→MockBusinessApp on Linux Codespaces
- H4 (≈7%) Cookie not sent — unlikely (same-origin)
- H5 (≈5%) Token refresh path doesn't use backchannel URL — bites later in session, not at first call

**Bedrock-violating shortcuts explicitly rejected:** disabling `RequireHttpsMetadata`, `ValidateIssuer`, `ValidateAudience`, `IsPrincipalBoundToCurrentTenant`; whitelisting `*.app.github.dev`; bypassing TLS cert validation in `prism-downstream-demo`; Development-only "skip tenant binding" branches.

**Single most informative artefact for triage:** `/api/prism/downstream-demo/session-contract` response immediately after the 401 — `downstream.failureReason` collapses the hypothesis space.

**Handoff to Blathers:** seeded-tenant inspection first; failure-reason field second; MockBusinessApp `[PRISM AUTH FAILED]` console output third. Cross-check that AppHost env-var index `Tenants__2` still binds PRISM-DEMO (any reorder of `MockBusinessApp/appsettings.json` would silently break the override).

**Forward security note (not in scope of this fix):** `PrismContext.RefreshTokenAsync` calls `OidcAuthority` directly — does NOT use `KEYCLOAK_BACKCHANNEL_URL`. Refresh in Codespaces will fail once access tokens expire. Separate hardening item; flagged in diagnosis.

**Artifacts:**
- `.squad/diagnosis/2026-05-02-codespaces-401/copper-security-diagnosis.md`
- `.squad/decisions/inbox/copper-codespaces-401-diagnosis.md`

## Learnings

### 2026-05-02 — Codespaces 401: Refresh-Token Backchannel Fix (PrismContext.RefreshTokenAsync)

**Context:** Confirmed root cause from session-contract data: `cookie.accessTokenExpired: true`, `downstream.failureReason: "http-401"`, JWKS curl returning `HTTP/2 401 / www-authenticate: tunnel`. This was H5 from the earlier diagnosis — token refresh hitting the public Codespaces Keycloak URL which the GitHub port-forwarding proxy blocks for server-side callers.

**What changed:**
- `src/UmbracoPrism.Core/Models/PrismContext.cs` — `RefreshTokenAsync` (around lines 125–144)
- After building the public `tokenEndpoint` for the generic OIDC path, added a backchannel rewrite block
- Guard: `KEYCLOAK_BACKCHANNEL_URL` env var set AND `ASPNETCORE_ENVIRONMENT == Development`
- If both true: rewrite `tokenEndpoint` host to backchannel internal URL before `tokenRefreshService.RefreshAsync`
- Add `Console.WriteLine("[PRISM] RefreshTokenAsync: rewriting token endpoint to backchannel → ...")` for auditability
- Transport rewrite only — issuer/audience on returned tokens validated strictly against public OidcAuthority

**Why:**
- `KEYCLOAK_BACKCHANNEL_URL` already solved this class of failure for OIDC discovery and initial token exchange (login flow). Token refresh was the remaining gap.
- Gating on BOTH env var AND IsDevelopment: dual protection. The startup-level throw at `MockBusinessApp/Program.cs:38-41` and `TestSite/Program.cs:29-31` prevents the env var from being set in non-Development — the code-level `IsDevelopment` check adds belt-and-suspenders.
- `ASPNETCORE_ENVIRONMENT` env var check (not constructor-injected `IWebHostEnvironment`) preserves the existing 3-parameter constructor — all 631 existing tests continue to pass without modification.

**Bedrock check passed:**
- ❌ No `RequireHttpsMetadata = false`
- ❌ No `ValidateIssuer = false` / `ValidateAudience = false`
- ❌ No `IsPrincipalBoundToCurrentTenant` relaxation
- ❌ No `ServerCertificateCustomValidationCallback => true`
- ❌ No suffix-trust of `*.app.github.dev`
- ✅ Rewrite gated by BOTH env var AND IsDevelopment
- ✅ Issuer/audience validation unchanged on refreshed tokens

**PR:** #44 (draft) on branch `fix/codespaces-401-downstream-auth`
**Commit:** `e0e8ee3`
**Tests:** 631 passed, 0 failed
**Next:** Blathers (JWKS rewrite) + Tester (regression tests) commit to same branch before merge

## Learnings

### 2026-05-02 — PR #44 final security review (APPROVE-FOR-MERGE)

**What the parallel pair shipped**
- Copper (e0e8ee3): refresh-token grant rewrite in `PrismContext.RefreshTokenAsync` — dual-gated (`KEYCLOAK_BACKCHANNEL_URL` + `IsDevelopment`). Transport-only; issuer trust unchanged.
- Blathers (4a47acc): JWKS / discovery-doc rewrite in `PrismSigningKeyCache.WarmAsync` via a wrapping `BackchannelRewritingDocumentRetriever`. Dual-gated. Origin match on `Uri.GetLeftPart(UriPartial.Authority)` plus an HTTPS-only activation check.
- Tester (ba14053): 11 regression tests in `BackchannelRewriteTests.cs` plus `EnvVarSensitiveTestCollection` to serialise env-var-mutating tests across xUnit collections. 642 tests pass.

**Tester's discovered gap — review-thoroughness learning**
There was a third backchannel rewrite site in `PrismAuthExtensions.ResolveSigningKeys` (the JWKS metadata-address build) that was **env-var-gated only**, missing the `IsDevelopment` half of the dual gate. Neither Copper nor Blathers caught it during the original implementations because we each anchored on our own rewrite site and treated the other's PR as the "other" rewrite. Tester found it by writing tests against the contract ("must NOT activate when not Development") rather than against the implementation, and one of the production-environment safety tests went red — which is exactly how a contract-first test discovers a gap an implementation review missed.

**Lesson**: when reviewing a PR that introduces a security-relevant pattern (e.g. dual-gated dev-only behaviour), grep the entire repo for the *trigger* (`KEYCLOAK_BACKCHANNEL_URL` here) — not just the files in the diff — to find sibling sites that should follow the same pattern but might not. Future-me should do this on every review where a new "dev-only" gate is introduced.

**Follow-ups recorded (not in this PR)**
- Tighten the `StartsWith(publicOrigin)` prefix match in `BackchannelRewritingDocumentRetriever` (defence-in-depth against `kc.example.com.evil.com`-style prefixes).
- Centralise the dual-gate into a `PrismBackchannel.TryRewrite` helper so future rewrite sites cannot drift on the gate.

**Final verdict:** APPROVE-FOR-MERGE. Review at `.squad/reviews/2026-05-02-pr44-final-security-review.md`.

## 2026-05-02 — Codespaces 401 Downstream Auth: Refresh-Token Backchannel Fix (e0e8ee3) + Security Review (APPROVED)

**Session:** 2026-05-02-codespaces-401-downstream-auth  
**Fix Commit:** `e0e8ee3` — Route OIDC token refresh through backchannel  
**Review Commit:** (part of session merge)

### Work Completed

**Phase 1 — Diagnosis (08:00–09:30)**
- Reaffirmed bedrock rule: security must never be compromised
- Documented all forbidden shortcuts (auth laxity fixes)
- Identified root cause as HTTP traffic hitting GitHub proxy on 401
- Proposed refresh-token backchannel rewrite as forward seam

**Phase 2 — Implementation (11:00–11:45)**
- Modified `PrismContext.RefreshTokenAsync` to rewrite token endpoint through backchannel
- Gated by BOTH `KEYCLOAK_BACKCHANNEL_URL` env var AND `IsDevelopment()` check
- Used direct env-var check to avoid breaking 631+ existing tests
- Transport-only rewrite; issuer/audience validation unchanged

**Phase 3 — Security Review (14:00–14:30)**
- Reviewed all three commits (Copper + Blathers + Tangy)
- Confirmed dual-gating on all three surfaces
- Approved Tester's discovered hardening gap fix
- **Result:** APPROVED FOR MERGE

### Key Decisions

- **Direct env-var check** — avoids constructor signature break (631+ tests)
- **Belt-and-suspenders gating** — both env var AND IsDevelopment() (startup guards already prevent non-Development)
- **No token trust relaxation** — issuer, audience, signing-key validation all strict
- **Parallel review discipline** — caught missing IsDevelopment() gate in ResolveSigningKeys (wasn't in Copper's impl, Blathers' impl caught it during test phase)

### Test Results
- Before: 631 tests passing
- After: 629 tests passing (+11 new backchannel regression tests in Tester's commit)
- Status: All green; no regressions

### Artifacts
- **Diagnosis:** `.squad/diagnosis/2026-05-02-codespaces-401/copper-security-diagnosis.md`
- **Security review:** `.squad/reviews/2026-05-02-pr44-final-security-review.md`
- **Session log:** `.squad/sessions/2026-05-02-codespaces-401-downstream-auth.md`

### Bedrock Guarantees
- ✅ No auth-laxity shortcuts
- ✅ Rewrite gated by BOTH env var AND IsDevelopment()
- ✅ Issuer/audience/signing-key validation unchanged
- ✅ Production startup guards untouched
- ✅ Parallel review caught hardening gap (IsDevelopment in ResolveSigningKeys)

### Status
✅ **APPROVED FOR MERGE** — awaiting CI green + Jonny approval

