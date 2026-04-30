# Copper — History




## 📋 Recent History

Previous history archived to reduce file size. Recent entries below.

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

**Context:** Aspire 9.2.0 displayed OTLP telemetry warning. After upgrade to 13.2.2, warning persists and is understood to be correct behavior.

**Root Cause:** Three distinct Aspire security controls were conflated in prior investigation attempts:
1. `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` → Dashboard **UI** authentication (browser access)
2. `ASPIRE_ALLOW_UNSECURED_TRANSPORT` → HTTP vs HTTPS transport (protocol security)
3. `Dashboard__Otlp__AuthMode` → OTLP endpoint **API key authentication** (telemetry ingestion security)

**Critical Learning:** Environment variables in AppHost's `launchSettings.json` do NOT automatically propagate to the dashboard child process. AppHost controls dashboard configuration **programmatically** via `DashboardLifecycleHook.cs`. When no OTLP API key is configured, AppHost always sets `OtlpAuthMode.Unsecured`.

**Security Distinction:**
- `Unsecured` mode = any local process can push telemetry without credentials (development-only, acceptable)
- `ApiKey` mode = requires API key for telemetry ingestion (production/staging/shared environments, required)

**Decision:** Accept the warning as expected behavior in local development. Suppressing the warning requires API key configuration (unjustified for localhost). The warning correctly informs developers of the security posture.

**Production Guidance:** Always use `Dashboard__Otlp__AuthMode=ApiKey` in non-development environments with secure API key distribution.

**Key Learning for Squad:**
- **Read the source code instead of guessing** — prior fix attempts could have been avoided
- **Process boundaries matter** — parent process env vars don't automatically reach child processes
- **Security warnings serve a purpose** — suppressing them should require understanding why they exist first
- **Local dev vs production security** — some warnings are informational in dev but critical in production

**Decision:** `.squad/decisions/2026-04-20-copper-otlp-telemetry-upgrade.md` (recorded in main decisions ledger)

---

## Learnings / 2026-04-30

**Review:** Full-stack security audit post-V2 polymorphic component model.

**Top 3 findings:**
1. **WorkflowPollController (HIGH — PATCHED):** `GET /api/prism/workflow/poll` had no `[Authorize]` attribute, exposing workflow state (step type, state version) to unauthenticated callers. Fixed immediately; regression test added to Phase1SecurityRegressionTests.cs.
2. **Microsoft.AspNetCore.DataProtection 10.0.0 (CRITICAL CVE):** Transitive dep in `UmbracoPrism.Shared` has advisory GHSA-9mv3-2cwr-p262. DataProtection is the cryptographic substrate for cookie encryption and antiforgery. Must upgrade via Umbraco.Cms version bump or explicit override.
3. **HMACSecretKey committed (HIGH):** Real base64 HMAC signing key committed in `TestSite/appsettings.json`. Key is compromised; rotate immediately.

**Also patched:** `DescribedBy` aria attribute used unencoded `FieldKey` (operator-controlled injection surface). Log injection in `PrismTenantMiddleware` (string interpolation → structured logging).

**Phase1SecurityRegressionTests.cs state:** 547 passing. Gaps remain for security headers, CookieSecurePolicy, and X-Forwarded-For rate limiting.

**Confirmed solid:** Open redirect hardening, OIDC nonce validation, SQL parameterisation, antiforgery on workflow POST, biometric tenant cross-check, TenantManagementController double-auth, WrapperAttrs/PatternAttr encoding.

**Watch items for next review:**
- SEC-003 (XSS in @Html.Raw content) — needs sanitiser design before definition editor leaves Dev-only mode
- SEC-006 (CookieSecurePolicy) — easy fix, low urgency but should not ship to production as-is
- SEC-007 (rate-limit bypass via proxy) — required before any cloud deployment

**Key learning:** V2 polymorphic component model introduced no new XSS surface at the Razor TagHelper level (all attrs encoded), but the pre-existing `@Html.Raw(Content)` pattern in display components is a latent risk that grows as the system matures.

### 2026-04-30 — SEC-004 Closed by Blathers

**Commit:** `b6336fd` (main)

**Finding:** HIGH — HMAC signing key and Vault URI committed in tracked `TestSite/appsettings.json`.

**Remediation:** Blathers implemented the `appsettings.Local.json` pattern (gitignored, loaded before Umbraco builder). Pattern decision recorded in decisions.md.

**Status:** ✅ CLOSED — Build passes, 547/547 tests green, no secrets in tracked files.




## 2026-04-30: Full Security Audit Post-V2 Rollout — 11 Findings Reviewed, 6 Closed This Batch

**Status:** ✅ BATCH COMPLETE — 6 findings closed (SEC-002, SEC-005, SEC-006, SEC-007, SEC-008, SEC-010); 1 in-flight (SEC-003 design); 4 pre-batch (SEC-001, SEC-004, SEC-009, SEC-011 closed earlier)

**Audit Scope:** Comprehensive codebase security review following v2.0 polymorphic component model rollout.

**Closed Findings Summary:**
1. **SEC-002 (CRITICAL):** Microsoft.AspNetCore.DataProtection GHSA-9mv3-2cwr-p262 → 10.0.0→10.0.7 (Blathers/NuGet bump)
2. **SEC-005 (HIGH):** npm CVE chain (1 critical + 10 high) → 0 critical, 0 high (Isabelle/npm audit fix)
3. **SEC-006 (HIGH):** CookieSecurePolicy SameAsRequest → Always (Blathers/security regression test)
4. **SEC-007 (HIGH):** Proxy-aware IP for rate limiting (Blathers/ForwardedHeadersMiddleware)
5. **SEC-008 (MEDIUM):** OpenTelemetry.Api GHSA-g94r-2vxg-569j → 1.12.0→1.15.3 (Blathers/NuGet bump)
6. **SEC-010 (MEDIUM):** Scrub PII + Vault IDs in MockBusinessApp (Blathers/appsettings.Local.json pattern)

**In-Flight / Designed (Not Yet Implemented):**
- **SEC-003 (HIGH):** @Html.Raw content sanitization — IWorkflowContentSanitizer design + allowlist frozen (Tom Nook proposal in inbox awaiting team sign-off)

**Earlier (Pre-Batch):**
- **SEC-001 (HIGH):** WorkflowPollController auth (2026-04-30, immediate fix)
- **SEC-004 (HIGH):** TestSite secrets management (already implemented, consolidated in batch)
- **SEC-009 (LOW):** Log injection (2026-04-30, immediate fix)
- **SEC-011 (LOW):** HTML encoding (2026-04-30, immediate fix)

**Triaged & Locked Decisions:**
- `IWorkflowContentSanitizer` (Ganss.Xss + GDS allowlist) is precondition for shipping definition editor to non-dev
- `CookieSecurePolicy.Always` + `ForwardedHeadersMiddleware` required pre-production
- Secrets policy: no real keys in version-controlled appsettings.json; use dotnet user-secrets (local) / env vars (CI/CD)

**Test Coverage:**
- 547 baseline → 550 passing (+3 findings tested)
- Phase1SecurityRegressionTests: 3 new cases (CookieSecurePolicy, ForwardedHeaders, rate-limit partitioning)

**Production Gates Remaining:**
- SEC-003 implementation (awaiting team go-ahead)
- SEC-007 KnownProxies/KnownNetworks hardening (dev-safe default; production must specify)
- SEC-010 PII notification per applicable data protection law (jonnypmuir@gmail.com in git history)

**Artifacts:**
- `.squad/decisions.md` — 6 findings + SEC-003 design proposal consolidated
- `.squad/orchestration-log/2026-04-30T12:*-{agent}.md` — Per-agent logs (Tom Nook, Isabelle, Blathers)
- `.squad/log/2026-04-30-security-batch-2.md` — Batch session summary
- `.squad/security-review-2026-04-30.md` — Full audit report (if exists; reference from decisions.md)

**Batch Scope:** All 6 closed findings reviewed, approved, tested, and merged to main. SEC-003 design frozen pending implementation assignment.

---

## 2026-04-30: SEC-003 T2+T8+T9 — WorkflowContentSanitizer Implementation

**Commit:** `ae616a2` (main)

### Work Completed

**T2 — Real implementation:**
- Created `src/UmbracoPrism.Core/Services/Sanitization/WorkflowContentSanitizer.cs`
- Ganss.Xss 9.0.892 configured with GDS-aligned allowlist per Tom Nook's §4.3 (exact compliance — no deviations from security policy)
- Tags: p, ul, ol, li, blockquote, br, h2, h3, h4, strong, em, b, i, code, abbr, span, a
- Attributes: href (a only, scheme-checked in RemovingAttribute handler), rel (a only), title (abbr only)
- Schemes: http, https, mailto, tel — all others blocked
- Post-processing: rel=noopener noreferrer + target=_blank injected for external http(s) links
- DI updated: NoOpWorkflowContentSanitizer → WorkflowContentSanitizer (singleton)
- NoOp retained as internal test fixture (comment updated)

**T8 — Unit tests:**
- Created `src/UmbracoPrism.Core.Tests/Services/Sanitization/WorkflowContentSanitizerTests.cs`
- 40 test cases covering allowed tags, href schemes, event handler stripping, disallowed tags, inline style, idempotency, null/whitespace

**T9 — Un-skip regression tests:**
- Removed Skip attribute from all 6 `Phase1SecurityRegressionTests` in WorkflowContent region
- Updated `BuildEnginePayloadForBody` helper to use real `WorkflowContentSanitizer` (not mock NoOp)

### Test Delta

| State | Count |
|-------|-------|
| Blathers handoff | 554 passing, 6 skipped |
| After T8 + T9 | **601 passing, 0 skipped, 0 failed** |

### Key Technical Decision

Used `RemovingAttribute` event with empty `AllowedAttributes` for per-tag attribute enforcement. Ganss.Xss only applies `AllowedSchemes` to attributes surviving the `AllowedAttributes` gate — since `href` is not in `AllowedAttributes`, we perform our own scheme check in the event handler. This gives strict per-tag enforcement (e.g. `href` on `<div>` is stripped) rather than global allowance.

### Production Gate Status

SEC-003 implementation complete. Precondition for definition editor's non-Dev rollout is now satisfied.

### Artifacts

- `.squad/decisions/inbox/copper-sec-003-impl.md` — allowlist verification, deviations, test counts
- `.squad/skills/ganss-xss-gds-allowlist/SKILL.md` — reusable Ganss.Xss GDS allowlist pattern

---


---

## 2026-04-30 — PR #39 Closeout: Pt2 Security Review (Depth-First, claude-opus-4.7)

**Context:** Second-pass depth-first security review following Pt1 (breadth-first, gpt-5). Pt1 closed 11 findings on 2026-04-26; Pt2 raised 10 additional findings with premium model on targeted high-risk areas.

**Scope:** Auth/identity defaults, sanitizer producer-side coverage, anonymous endpoints, CSRF posture, security response headers, dependency CVEs, DataProtection key management, CORS/origin trust on BiometricController.

**Findings:** 0 Critical / 0 High / 5 Medium (2 patched, 3 open) / 4 Low (open) / 1 Info (open)

**Fixes Landed:**
1. **SEC-PT2-002** — Bumped transitive `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.2 → 1.15.3` (CVE-2026-42191). Pt1 missed transitive; audit now clean.
   - Commit: `244f3b5`
2. **SEC-PT2-001** — Gated anonymous `/api/test/reset` endpoint behind `IsDevelopment()`. Neighbouring `/admin/*` guard didn't cover this path.
   - Commit: `2ce771f`

**Quality:** `dotnet build` clean, 601/601 Core tests green, vulnerable-package audit clean.

**Open Items (Dispatched as `sec/pt2-backend`, separate PR):**
- SEC-PT2-003: Logout-CSRF (GET → POST + antiforgery)
- SEC-PT2-004: Missing security response headers (middleware + exemptions)
- SEC-PT2-005: `DefaultAuthenticateScheme` integration test gap (assignee: Blathers)
- SEC-PT2-006: DataProtection ephemeral keys; needs `PrismDataProtectionOptions`
- SEC-PT2-007: Unsanitized `accordionSection.Content` Razor partial (XSS trap; unused today)
- SEC-PT2-008: RTE field operator-trust pattern; informational
- SEC-PT2-009: Antiforgery gap on JSON endpoints (mitigated by SameSite=Lax)
- SEC-PT2-010: `IsCapacitorOrigin` localhost acceptance (risk-accept candidate)

**Ledger:** `.squad/security-review-2026-04-30-pt2.md`

**Model Used:** claude-opus-4.7

**Key Learning:**
- Transitive vulnerability audits must be explicit in full-stack reviews.
- Middleware guards require path clarity; `/admin/*` doesn't cover `/api/test/*`.
- Standard practice going forward: breadth-first pass + depth-first follow-up per security cycle.

**Decision Recorded:** `.squad/decisions.md` § 2026-04-30 (merged from inbox).

---
