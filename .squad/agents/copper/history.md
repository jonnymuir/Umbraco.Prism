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