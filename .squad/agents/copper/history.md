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
