# Copper — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**User:** Jonny Muir

## Security Context

- Prism is multi-tenant and security-critical by design.
- User directive: prioritize confidentiality, integrity, and availability.
- Zero tolerance objective: no cross-tenant authentication leakage and no cross-tenant data leakage.
- OAuth must be implemented with tenant-safe boundaries; avoid single-tenancy caching assumptions common in generic MSAL-style designs.

## Learnings

- Entra-first authorization model migration is underway (#4 with child issues #8, #9, #10).
- OIDC and token refresh paths recently hardened (#2, #3) and require ongoing isolation-focused verification.
- Security reviews should include cache keying, token claim scoping, fallback behavior, and failure-mode isolation.
- Repeated unknown-`kid` tokens can trigger forced key-cache refresh loops unless refresh cadence is bounded; a short per-tenant cooldown materially reduces outbound metadata amplification DoS risk while keeping fail-closed signature behavior.

## 2026-03-22 — CIA Hardening Round 1

- Added strict tenant-binding in `PrismContext`: bearer token usage and refresh now require principal `tid` to match resolved `CurrentTenant.EntraTenantId`; mismatch returns null and blocks refresh.
- Added fail-closed guards in token refresh flow for missing tenant OIDC config (`EntraTenantId`, `EntraClientId`, `SecretKeyName`) and empty resolved vault secret.
- Hardened downstream JWT validation in `PrismAuthExtensions`:
	- Issuer must be a valid absolute URI with exact host/path bound to token `tid` (`{tid}.ciamlogin.com/{tid}/v2.0...`).
	- Audience must match the configured `ClientId` for the same token tenant (`tid`), preventing cross-tenant audience acceptance.
	- Signing keys are resolved only for configured tenant IDs.
- Added regression coverage for tenant mismatch and issuer/audience tenant-bound checks in core tests.
- Remaining availability risk: token refresh circuit breaker is still application-wide; outage/failure bursts from one tenant can contribute to shared breaker pressure for all tenants.

## 2026-03-22 — trycloudflare Dev Automation Security Review

- Reviewed `scripts/dev/start-trycloudflare.sh` from CIA + tenant-isolation perspective.
- Added stricter guardrails for local inputs: enforce valid TCP port range (1-65535) and GUID format for Entra app object ID.
- Enforced tunnel hostname trust boundary to `*.trycloudflare.com` before any redirect URI or tenant DB mutation is applied.
- Added clearer operator warning in script output that the flow is local-development only and mutates Entra + local tenant state.
- Added README security notes clarifying dev-only scope, least-privilege Azure access, and local/test database targeting to reduce blast radius.

## 2026-03-28 — Issue #7 Security Gate Review

- Re-reviewed OIDC key rotation fail-closed path in `PrismOidcConfiguration`: unknown or expired `kid` returns no keys synchronously while triggering background warm, preventing fail-open acceptance.
- Added cache test coverage proving forced-refresh cooldown is tenant-scoped (not global), preserving tenant isolation while reducing metadata amplification pressure under unknown-`kid` bursts.
- Added refresh stress coverage proving an open circuit on one token endpoint does not suppress concurrent refresh success on another endpoint.

## 2026-03-28 — Biometric Auth Security Threat Model (Design Phase)

**Decision:** Biometric auth for Prism Mobile will use Prism-issued device credentials (not Entra tokens on device).

**Threat Model Completed:**
- Device credential JWT model with optional multi-tenant binding via `prism_device_cred_{tenantId}_{userId}` keystore key pattern
- Server-side device registry with admin revocation (`DELETE /api/prism/device/{deviceId}`)
- Device credential exchange endpoint with rate limiting requirement
- Bounded lifetime (max 30 days, configurable per tenant)
- Biometric enrollment change detection → automatic credential wipe

**Security Properties Achieved:**
- No Entra refresh token exposure on device
- Server-side revocation control independent of Entra
- Cross-tenant isolation via keystore naming and JWT claims
- Device binding enables theft/replay detection
- Credential lifetime forces periodic full re-auth

**Hard Constraints Documented:**
1. No Entra Refresh Token Storage in device keystore
2. Single-Tenant Binding (tenant_id JWT claim)
3. Server-Side Registry (central issuance/revocation)
4. Bounded Lifetime (max 30 days)
5. Biometric Failure Handling (fallback to full OIDC)
6. Keystore Isolation (multi-tenant scenarios)

**Design document:** `/Design/biometric-auth.md` (merged from Tom Nook, Copper, Kicks)
- Added middleware cancellation coverage proving request-aborted warm operations rethrow `OperationCanceledException` and do not continue the pipeline.
- Security gate outcome for issue #7: pass-with-conditions; residual availability candidate remains downstream synchronous metadata retrieval path in `PrismAuthExtensions`.

## 2026-03-28 — PrismAuthExtensions Mitigation Security Gate

- Reviewed downstream auth key resolution and confirmed tenant allow-list plus tenant-bound issuer/audience checks remain intact in `PrismAuthExtensions`.
- Verified fail-closed behavior in `ResolveSigningKeys`: when the signing-key cache snapshot is expired or does not contain the requested `kid`, resolver returns empty keys and only triggers non-blocking background warm.
- Confirmed tenant isolation invariants remain intact:
	- Signing key lookup is tenant-scoped via tenant-id keyed cache entry and tenant allow-list gate.
	- Unknown tenant IDs return no keys.
	- Unknown/stale key paths fail closed in both downstream resolver and `PrismOidcConfiguration` snapshot path.
	- Background warm trigger in `PrismOidcConfiguration` does not bypass validation because resolver still returns no keys when cache is expired or requested key is absent.
- Updated security tests to target the current cache-snapshot resolver API shape in `PrismAuthExtensions`.
- Focused security tests (exact counts):
	- `PrismAuthExtensionsSecurityTests`: 5 passed, 0 failed.
	- `PrismSigningKeyCacheTests`: 5 passed, 0 failed.
	- `PrismOidcConfigurationTests`: 4 passed, 0 failed.
	- Total: 14 passed, 0 failed.
- Gate outcome: pass.

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.

## 2026-03-29 — Biometric Authentication Security Analysis

- Reviewed biometric login design for Prism Mobile (Capacitor WebView wrapper).
- Core threat: storing Entra refresh tokens on-device creates high-value target with insufficient server-side revocation control.
- **Recommendation:** Use Prism-issued device credentials (JWTs with device binding) instead of Entra refresh tokens.
  - Device registration flow creates server-side credential that can be tenant-admin revoked.
  - Device credential has bounded lifetime (7-30 days) with forced re-auth.
  - Server validates device binding (device ID) and tenant scope on every exchange.
- Multi-tenant isolation: device keystore entries MUST include tenant ID in key name to prevent cross-tenant credential leakage in shared-device scenarios.
- Root/jailbreak mitigation: require server-side device registration with approval flow; device credential revocation on suspicious activity.
- Session establishment: device credential exchanged for short-lived access token via secure API endpoint; token injected into WebView session using secure message passing over Capacitor bridge.
- Hard constraints:
  - Device credentials scoped to single tenant (no cross-tenant reuse).
  - Server-side device registry with admin revocation API.
  - Maximum credential age enforcement (30 days recommended).
  - Biometric failure → immediate full OIDC re-auth (no fallback to stored credential).

## 2026-03-29 — Biometric Design Security Review (Advisory)

- Conducted comprehensive security review of `/Design/biometric-auth.md` at Jonny's request.
- **Trust chain analysis:** Entra is the root identity provider; Prism acts as delegation layer storing encrypted Entra refresh tokens server-side; BiometricToken is opaque server-issued credential; device biometric is local user presence verification only.
- **Industry practice assessment:** Design aligns with banking/enterprise app patterns (opaque device tokens, server-side storage, bounded lifetime). Not bleeding-edge (FIDO2/passkeys would be stronger) but solid for v1 convenience feature.
- **Strengths identified:**
  - No Entra tokens stored on device (critical security win).
  - Single-tenant binding with keystore isolation.
  - Server-side registry with revocation control.
  - Fail-closed on all error paths.
  - Rolling refresh token rotation (marked as v1 hard requirement).
  - Biometric enrollment change detection with credential wipe.
- **Critical gaps flagged for mitigation:**
  - **90-day token expiry too long:** Recommended 30 days with sliding expiry; document admin revocation requirement on user disable.
  - **Rate limiting underspecified:** Recommended per-token lockout (3 failed exchanges/10min) + IP-based rate limiting (10 req/min); log all failed exchanges.
  - **Device binding incomplete:** Recommended full specification with UUID device ID, server-side storage in `prismBiometricTokens.DeviceId`, and validation on exchange; this is the single biggest security improvement before implementation.
  - **Certificate pinning optional:** Should be default for credential-handling endpoints, not a "consideration."
  - **Global refresh token encryption key:** Recommended per-record IVs and quarterly key rotation for v1; defer per-tenant keys to v2.
  - **No audit logging:** Deferred to v2 but recommended minimum logging of exchange attempts (success/failure) for incident response.
- **Prism vs. Entra role separation:** Well-separated. Prism is session convenience layer delegating identity decisions to Entra. Prism respects Entra CA policies via token refresh flow (policies re-evaluated on every exchange). Risk is in implementation: must maintain bounded lifetime and refresh token protection to avoid creating parallel weaker identity system.
- **Primary recommendation:** Fully specify and implement device binding before implementation starts. This is an architectural decision (adds `DeviceId` column, exchange validation logic, client-side UUID generation) that closes bearer token theft vector with minimal user friction impact.
- **Overall assessment:** Design is 80% there with sound architecture; needs tactical hardening on token lifetime, rate limiting, and device binding to reach production-ready security posture.

## 2026-07-10 — Biometric Design Walkthrough (Advisory to Jonny)

- Delivered full chain-of-trust walkthrough: Entra is root; Prism is delegation/session layer; BiometricToken is opaque server-issued bearer credential; biometric is local device-side access gate only — server has NO cryptographic proof biometric occurred.
- **Critical design inconsistency flagged:** Security Considerations section describes a signed JWT with embedded DeviceId claim; main architecture section describes a plain UUID v4 stored by SHA-256 hash with no DeviceId column in the DB schema. These are fundamentally different credential models with very different security properties. This MUST be resolved before implementation begins — it is not a detail, it changes the DB schema, the exchange validation logic, and the threat model.
- **Bearer token without device binding is the real gap:** UUID v4 presented at `/exchange` with no cryptographic proof of device identity. Server cannot distinguish the real device from an attacker who extracted the UUID via root/jailbreak. Mitigation requires either: (a) commit to JWT model with DeviceId embedded + validated on exchange, or (b) commit to UUID model explicitly, document it's a pure bearer credential, and compensate with extremely tight rate limiting and 30-day max lifetime.
- **Token lifetime conflict:** DB schema says 90 days. Security section says 7–30 days. v1 scope says 90 days. Three different values in the same document. 90 days is too long for a bearer credential without device binding.
- **Biometric trust is local-only:** The server trusts the OS enforced biometric. This is the standard industry model and is fine — but it means the real security is in OS platform integrity and hardware security module, not in Prism's code.
- **Prism-as-secondary-identity-system risk:** Holding Entra refresh tokens server-side is correct (vs. device) but makes the Prism DB a very high-value target. The encryption key for `RefreshTokenEnc` is the blast radius control — must be in Key Vault, per-record IVs non-negotiable, key rotation plan required before go-live.
- **Industry comparison:** Design aligns with banking app patterns (opaque device token, server-side storage, bounded lifetime). Not FIDO2/passkeys, which would give cryptographic proof of device and user presence. FIDO2 is the stronger path but adds significant implementation complexity. The opaque token model is defensible for v1 if device binding and lifetime are properly specified.
- **One change before implementation:** Resolve JWT vs UUID inconsistency and commit to the JWT-with-DeviceId model. Add `DeviceId` to `prismBiometricTokens` schema, validate on exchange. This is the single architectural decision that closes the bearer theft vector without user friction.

## 2026-07-10 — FIDO2/Passkeys vs JWT+DeviceId Advisory (Response to Jonny)

- Jonny challenged the design choice directly: "If FIDO2 is more secure, why not use it in v1?"
- **Verdict: JWT+DeviceId is the right call for v1. FIDO2 is not.**

**Key findings documented:**

1. **Multi-tenant RP ID is the hard blocker.** FIDO2 WebAuthn credentials are cryptographically bound to an RP ID (origin domain). Prism serves tenants on different custom domains. Each tenant domain becomes a separate FIDO2 Relying Party — meaning a credential registered on `tenant-a.example.com` is cryptographically invalid for `tenant-b.example.com`. There is no standard FIDO2 mechanism for cross-domain RP sharing. This is an architectural problem, not a complexity problem. It is not solvable without either (a) forcing all tenants onto a shared subdomain, which contradicts the multi-tenant custom-domain model, or (b) implementing per-tenant FIDO2 credential stores, which multiplies complexity without benefit.

2. **No mature Capacitor passkey plugin.** `@capawesome-team/capacitor-passkeys` exists but is new and unproven in production enterprise apps. The FIDO2 native path (iOS ASWebAuthenticationSession / Android FIDO2 Client API) is callable from Capacitor but requires custom native plugin work. Passkeys in a WebView (WKWebView/Android WebView) require iOS 16+ and Android 9+ with specific WebView versions — the Prism WebView is not the system browser, which adds risk.

3. **Entra CA policies would not apply.** The current design calls the Entra `/token` endpoint on every exchange — Entra Conditional Access policies (MFA, compliant device, location restrictions) re-evaluate on every refresh. If FIDO2 is the local authenticator and replaces the Entra token exchange, CA policies only fire on initial FIDO2 enrollment (which requires a prior Entra login) and on re-registration. Between those events, CA policy changes are invisible to the device. The JWT+DeviceId model preserves live CA enforcement because Entra is called on every exchange server-side. FIDO2 would need to sit alongside Entra (not replace it) to preserve this — but then you have two parallel identity paths with no material benefit from FIDO2.

4. **Implementation cost: 3–6× the JWT model.** JWT+DeviceId: 3 endpoints, 1 table, 1 service, ~2 weeks. FIDO2: `Fido2NetLib` integration (non-trivial, needs careful multi-tenant config), challenge/session state management, attestation verification, assertion verification, new credential store tables, per-tenant RP config, Capacitor native plugin work, and bridging back to an Entra session at the end anyway. Conservative estimate: 8–14 weeks for a correct implementation.

5. **FIDO2 closes a gap the current design mostly compensates for.** FIDO2 provides cryptographic proof that the authenticating key is on the registered hardware. The JWT+DeviceId model provides device binding by UUID — it's a bearer credential, not a hardware-bound key. An attacker who extracts the JWT from a jailbroken device can replay it. The mitigation is rate limiting, short lifetimes, and anomaly detection — already in the design. For an enterprise mobile convenience feature (not a payment authorization path), this is an acceptable tradeoff.

**Recommendation recorded:**
- v1: JWT+DeviceId model as designed. The multi-tenant RP problem alone rules FIDO2 out cleanly.
- v2/v3 candidate: Revisit FIDO2 only if (a) Prism adopts a unified shared domain for all tenants, OR (b) Microsoft Entra External ID natively supports passkeys at the Entra level (delegating the RP and credential management to Microsoft, not Prism), OR (c) a specific high-security tenant requests it and is willing to fund the per-tenant implementation. Not a generic v2 roadmap item.
- The JWT model is genuinely good enough long-term for what biometric login is: a session convenience layer, not an identity root.

## 2026-03-31 — Issue #28 Penetration Test Checklist (Spike)

- Conducted comprehensive security audit of biometric auth implementation against 17-item pen test checklist from issue #28.
- **Scope:** 17 security test items covering auth tokens, tenant isolation, rate limiting, data leakage, session injection, and fallback flows.
- **Coverage Assessment:**
  - ✅ **9/17 Automated Tests:** Device mismatch 401, token expiry, revoked tokens, JWT tampering, rolling rotation integrity, cross-tenant rejection, admin cross-tenant isolation, rate limiting (3-failure lockout + reset), audit logging without sensitive data
  - 🟡 **4/17 Partial:** Audit log sanitization (tested but needs staging log inspection), Entra token isolation (design confirmed server-side; device Keystore check pending), OIDC fallback (error handling present; integration test pending)
  - ❌ **4/17 Manual Device Testing:** Cookie attributes (Secure, HttpOnly, SameSite=Strict), session cookie pre-injection before navigation, WebView JS isolation verification, credential clearance on 401 response
- **Key Security Findings:**
  - Device mismatch validation prevents bearer token replay on different hardware
  - Tenant isolation enforced at two levels: DB query filter (`WHERE TenantId = @TenantId`) + JWT claim validation
  - Rate limiting service correctly tracks per-token and per-IP limits with configurable thresholds
  - Audit logging verified to exclude raw JWTs, refresh tokens, and cookie values
  - **Critical:** Design and code confirm Entra refresh tokens stored server-side only in encrypted DB column; no Entra tokens on device. This is the major security win vs. naive client-side token storage.
- **Manual Testing Phase Requirements:**
  - Staging deployment: All Phase 1–3 issues complete, rate limiting functional, device registry operational
  - Devices: iOS (iPhone 14+, iOS 16+) + Android (Pixel 6+, Android 13+) with biometric sensors
  - Tools: HTTP intercept proxy (Charles/Fiddler), iOS Safari Web Inspector, Android Chrome DevTools (remote debugging)
  - Time: 4–6 hours estimated for device test plan execution
- **Created documentation:** Design/pentest-checklist.md (388 lines) with:
  - Full 17-item checklist with inline test citations
  - Coverage summary table (9 ✅ | 4 🟡 | 4 ❌)
  - Detailed manual verification steps for each manual test item
  - Device requirements and tool specifications
  - Pre-ship gate status and sign-off placeholder
- **Gate Status:** 9/17 covered by CI/CD automated tests; 4/17 partial (code review verified, staging inspection pending); 4/17 blocked on real device testing.
- **Next Steps:** Jonny to schedule staging deployment window + device testing. Issue #28 remains open pending manual testing completion and final security sign-off.

## 2026-03-31 — Token Warmup Security Review (MemberDashboardController)

**Context:** `MemberDashboardController.Index()` changed from synchronous `override IActionResult Index()` to `new async Task<IActionResult> Index()`, now calling `await prismContext.GetAuthorizationHeaderAsync()` before rendering to proactively warm up and refresh the Prism access token. Return value is discarded; side effect (cookie update via `SignInAsync`) is the goal.

**Security Review Completed:**

### A. Token Refresh Correctness ✅
- `GetAuthorizationHeaderAsync()` with discarded return is **safe by design**. The method returns `null` on all failure paths and returns a bearer header on success. Discarding a header or `null` has no side effects beyond the intentional `SignInAsync` call in `RefreshTokenAsync`.
- `SignInAsync("PrismMemberCookie", ...)` inside `RefreshTokenAsync` is **correctly called**. ASP.NET Core cookie middleware writes the updated cookie to the response via `HttpContext.Response` — this works in any async MVC/Razor controller context, including Umbraco render controllers. Response headers are committed at render time, after the controller method returns.
- Silent failure risk is **mitigated**. `RefreshTokenAsync` logs all failure paths (`Log.Error`/`Log.Warning`) and returns `null`. The controller does not check the return value — if refresh fails, the page still renders with a stale token. This is **acceptable**: user sees the dashboard, downstream API calls may fail, but the page load succeeds gracefully. Alternative (throwing exception) would cause 500 errors on token service outages — current behavior is better availability posture.

### B. Race / Concurrency 🟡 LOW RISK
- **No locking or idempotency guard** in `RefreshTokenAsync` or `GetAuthorizationHeaderAsync`.
- **Scenario:** User opens two tabs → both call `GetAuthorizationHeaderAsync()` → both detect expiry → both call `RefreshTokenAsync()` → both POST to the Entra token endpoint with the same refresh token.
- **Entra refresh token behavior (CIAM single-use rolling model):**
  - First request succeeds → Entra returns new `access_token` + new `refresh_token`, invalidates old refresh token.
  - Second request fails → 400 Bad Request (refresh token already used).
- **Result:** Second request logs failure, returns `null`, writes no cookie update. First request's cookie update wins (last-write-wins for the session cookie). User's session remains valid with the first refresh result.
- **Risk Level:** **Low**. One tab gets the refresh, one tab logs a failure. No session invalidation, no cross-tenant leakage, no prolonged outage. User experience: second tab may see downstream API errors until next page load (which will succeed because the first tab's refresh wrote a valid cookie).
- **Mitigation opportunity (future):** Add in-process SemaphoreSlim keyed by `(EntraTenantId, userId)` to serialize concurrent refresh attempts per user-tenant pair. Not critical for v1.

### C. Tenant Isolation ✅
- `GetAuthorizationHeaderAsync` **correctly gates** on `CurrentTenant` non-null (line 32 check + line 46–50 tenant-binding validation via `IsPrincipalBoundToCurrentTenant`).
- `IsPrincipalBoundToCurrentTenant` enforces principal `tid` claim **must match** `CurrentTenant.EntraTenantId` (lines 146–159). Mismatch returns `false`, which blocks both bearer header return and `RefreshTokenAsync` invocation.
- **If `CurrentTenant` is null** when controller runs: `GetAuthorizationHeaderAsync` returns `null` (line 36), no refresh occurs, page renders with `ViewBag.Tenant = null`. No token leakage, no cross-tenant risk.
- **Refresh cannot write wrong-tenant token:** `RefreshTokenAsync` (lines 72–76) fails closed if `CurrentTenant` is `null` or if principal tenant binding fails. The token endpoint URL and client secret are derived from `CurrentTenant`, and the updated cookie is only written if tenant validation passes.

### D. Cookie Security ✅
- Cookie security properties are **correctly configured** in `PrismComposer.cs:91–96`:
  - `Cookie.Name = "PrismMemberCookie"`
  - `Cookie.SameSite = SameSiteMode.Lax` (acceptable; prevents CSRF on state-changing requests, allows top-level navigation)
  - `Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest` (allows local HTTP dev; enforces HTTPS in production when request is HTTPS)
  - **HttpOnly + Secure** are ASP.NET Core cookie authentication **defaults** (`HttpOnly = true`, `Secure` follows `SecurePolicy`). Verified in Microsoft.AspNetCore.Authentication.Cookies source.
- **`new` keyword risk:** **Low**. The `new` keyword hides `RenderController.Index()` but does **not** change Umbraco's route-hijacking dispatch behavior. Umbraco v17 route-hijacking resolves controllers by naming convention (`MemberDashboardController` matches document type alias `memberDashboard`) and invokes the most-derived `Index()` method via polymorphic dispatch. The async `Task<IActionResult> Index()` signature is **valid** and will be called correctly by Umbraco's MVC dispatcher. No base-class fallback risk.

### E. Error Handling 🟡 MEDIUM CONCERN
- **No try-catch** around `await prismContext.GetAuthorizationHeaderAsync()` in `MemberDashboardController.Index()` (line 41).
- **Exception scenarios:**
  1. `HttpContext` is `null` → returns `null`, no exception.
  2. `AuthenticateAsync` fails → returns `null`, no exception.
  3. Tenant validation fails → returns `null`, no exception.
  4. `RefreshTokenAsync` throws (vault secret resolution, HTTP client factory, Polly pipeline exception outside Polly's `ShouldHandle` scope) → **exception propagates to controller → 500 error page**.
- **Known safe paths:**
  - `BrokenCircuitException` is caught in `PrismTokenRefreshService.RefreshAsync` (line 123), returns `TokenRefreshResult(false, ...)`, no exception.
  - `HttpRequestException`, `TaskCanceledException` caught (line 128), returns failure result.
  - JSON parse failure caught (line 152), returns failure result.
- **Unhandled exception risk:** Vault service (`vault.GetSecretAsync`) or HTTP client factory could throw. These would bubble up as 500 errors.
- **Recommendation:** **Wrap in try-catch with graceful fallback:**
  ```csharp
  try
  {
      await prismContext.GetAuthorizationHeaderAsync();
  }
  catch (Exception ex)
  {
      logger.LogWarning(ex, "Token warmup failed during dashboard page load; continuing with stale token");
      // Page still renders, user may see downstream API errors but dashboard is accessible
  }
  ```
- **Severity:** **Medium**. Availability impact: a vault or HTTP factory failure during page load causes 500 for all users hitting that controller. Likelihood is low (vault and HTTP client factory are core dependencies, failures are rare), but blast radius is high (complete page failure vs. graceful degradation).

### Summary Verdict: ⚠️ CONCERNS — Medium Severity

**Recommendation:**
1. **Required (Medium severity):** Add try-catch around `GetAuthorizationHeaderAsync()` call in `MemberDashboardController.Index()` to prevent vault/HTTP exceptions from causing 500 errors. Log warning and continue rendering page.
2. **Optional (Low severity, future enhancement):** Add per-user-tenant SemaphoreSlim in `RefreshTokenAsync` to prevent double-refresh races in multi-tab scenarios. Not blocking for current change.

**Security Invariants Confirmed:**
- Tenant isolation: ✅ Maintained
- Cookie security: ✅ Correct
- Fail-closed on tenant mismatch: ✅ Preserved
- Token refresh correctness: ✅ Side-effect model works as intended

**Gate Status:** Pass with required fix for exception handling. Error handling improvement should be applied before merge.
