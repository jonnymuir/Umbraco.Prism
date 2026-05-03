# Blathers — History (Summary)

**Agent:** Backend specialist shipping Codespaces URL derivation fixes, backchannel rewrites for JWKS/token-refresh, and security analysis for auth isolation.

**Recent focus:** CI repair (test isolation strategy), Keycloak backchannel env-var serialization, auth path regression prevention.

---

## 2026-05-03: Team Spawn — CI Repair Confirmation

**Status:** ✅ Investigation complete

**Finding:** Remaining local test failures are dual-origin:
1. HMAC secret key committed in appsettings (auth regression)
2. TestSite view override failures (unrelated to CI auth fix)

**Security Directive:** Do not commit HMACSecretKey in appsettings.

**Impact:** Clarified scope for downstream test-isolation fix; confirmed test-isolation-first strategy.

---

## 2026-05-02: Codespaces Auth & OIDC Infrastructure (4 PRs)

**Status:** ✅ Complete (PR #44, #45, #46 shipped)

### Key Fixes

1. **JWKS Backchannel Rewrite** — `BackchannelRewritingDocumentRetriever` wraps document retrieval to rewrite public Keycloak URLs to backchannel on HTTPS+Development
2. **Refresh Token Headers** — Added `X-Forwarded-Proto`/`X-Forwarded-Host` to backchannel refresh requests (fixes `invalid_grant` scheme mismatch)
3. **Codespaces URL Derivation** — Replaced string-concat pattern with `gh codespace ports` discovery (handles new regional URL scheme)
4. **BusinessApp Downstream Target** — Extended URL discovery to include port 7245; removed broken backchannel fallback

### Security Bedrock Unchanged

- `RequireHttpsMetadata = true`, `ValidateIssuer = true`
- Issuer trust anchor remains public OidcAuthority
- Forwarding headers only affect Keycloak's grant computation; Prism validation untouched

### Learnings

- GitHub's new Codespaces URL scheme is opaque; only `gh codespace ports` is reliable source
- `OpenIdConnectConfigurationRetriever` uses single `IDocumentRetriever` for all fetches
- Dual-gate pattern (ASPNETCORE_ENVIRONMENT=Development + HTTPS authority) prevents loopback test rewrites
- Must snapshot env vars in test classes reading `KEYCLOAK_BACKCHANNEL_URL` (race prevention)

---

## Earlier Sessions

Full history archived to `history-archive.md` (prior to 2026-05-01).

## 2026-05-03: Team Spawn — HMAC Secret Remediation

**Status Update (Scribe):** Blathers completed local TestSite appsettings drift repair. Tracked `appsettings.json` no longer contains real HMAC secret; local-only config now in gitignored `appsettings.Local.json`. Decision recorded in decisions.md (entry dated 2026-05-03).
