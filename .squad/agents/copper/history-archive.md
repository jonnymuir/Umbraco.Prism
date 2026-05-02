# Copper — History Archive

**Summarized:** 2026-05-02  
**Sessions archived:** Pre-Codespaces 401 investigation (2026-05-01 and earlier)

---

## 2026-05-01 — PR #44 Backchannel Refresh Token Fix (e0e8ee3) + Security Review (APPROVED)

**Session:** 2026-05-02-codespaces-401-downstream-auth  
**Fix Commit:** `e0e8ee3` — Route OIDC token refresh through backchannel

**Scope:** Route OIDC token refresh through backchannel to handle GitHub Codespaces port-forwarding proxy. Root cause: Keycloak 26 with `--proxy-headers xforwarded` uses `X-Forwarded-Proto` to compute its canonical issuer URL scheme. Without that header on the backchannel refresh POST, Keycloak computed its issuer as `http://...` but the stored refresh token's `iss` JWT claim was `https://...` (issued through YARP), causing `invalid_grant`.

**Key Decisions:**
- **Direct env-var check** — avoids constructor signature break (631+ tests)
- **Belt-and-suspenders gating** — both env var AND IsDevelopment()
- **No token trust relaxation** — issuer, audience, signing-key validation all strict

**Bedrock Guarantees:** ✅ No auth-laxity shortcuts. Rewrite gated by BOTH env var AND IsDevelopment(). Issuer/audience/signing-key validation unchanged. Production startup guards untouched.

**Test Results:** 631 tests passing, 0 failed.

---

## 2026-04-30 — PT2 Security Review & Patching

**Status:** ✅ COMPLETED — Security review findings prioritized and dispatched.

**Key decisions:** CSP shipped as Report-Only (enforce later after nonce audit). Intentional antiforgery exemptions on Capacitor endpoints documented with policy comments. DataProtection persistence at TestSite layer with follow-up on encryption-at-rest.

---

## Earlier Sessions

Full session history and decision records remain accessible in git history and `.squad/decisions.md`.
