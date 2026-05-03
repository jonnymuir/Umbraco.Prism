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

---

## 2026-05-03: Downstream Diagnostics & 401 Refresh Path

**Status:** ✅ Complete; merged to main.

**Scope:** Diagnose localhost:5163 invalid-response path and fix downstream security diagnostics.

**Security Findings Triaged:**
- **HTTP Metadata Preservation:** Real downstream HTTP status/reason now preserved (not flattened to `statusCode: 0`)
- **Header Logging:** Real downstream headers (e.g., `WWW-Authenticate`) now logged and included in diagnostics
- **401 Refresh/Retry Fix:** Eliminated mutation of `HttpClient.Timeout` between requests; uses per-request cancellation token

**Root Cause Diagnosed:**
Live Codespaces symptom (`http://localhost:5163/api/backoffice/me`, `contentType: unknown`) was hiding real HTTP metadata behind flattened "Invalid Response" response. This made 401 auth rejections appear as transport errors.

**Impact:** Dashboard diagnostics now surface actionable clues for:
- Transport failures (`Network Error` / `Timeout`)
- Auth rejections (`401 Unauthorized` + `WWW-Authenticate`)
- Redirect/proxy behaviour (`302 Found` + `Location`)
- HTML tunnel pages (text/html diagnostics)

---

## 2026-05-03: Live 401 Stale Runtime Diagnosis

**Status:** ✅ Diagnosed; operator action required (no repo changes)

**Scope:** User reported 401 `invalid_token` from MockBusinessApp after running `refresh.sh`.

**Root Cause:** Stale runtime. The MockBusinessApp process started at 09:45 (2h15m before diagnosis) predates the PR #46 fix for generic OIDC bearer validation. The running code does not include the `KEYCLOAK_BACKCHANNEL_URL` backchannel JWKS fetch logic that was merged to main.

**Evidence:**
1. AppHost/BusinessApp started 09:45 (PID 28308)
2. PR #46 shipped JWKS backchannel fix (PrismAuthExtensions.cs:231-242)
3. AppHost sets `KEYCLOAK_BACKCHANNEL_URL` env var for BusinessApp (Program.cs:145)
4. Running BusinessApp does not have the updated validator code

**Solution:** Restart the stack via `bash scripts/codespaces/refresh.sh` or restart BusinessApp via Aspire Dashboard (https://localhost:17214).

**Status Page Confusion:** User noted "status page doesn't work, but health-check redirected to something that did work". Diagnosis: standalone status server (port 3000) not running, but Aspire Dashboard (port 17214) IS running. The health-check.sh script checks both. No repo issue — status server is optional convenience for Codespaces.

**Learnings:**
- Applied `.squad/skills/live-oidc-401-stale-runtime` pattern: differentiate stale runtime from repo bug by checking process start time vs. git history
- Confirmed that Aspire-managed services require stack restart or Aspire Dashboard restart to pick up code changes
- Status server independence from Aspire confirmed; not a critical dependency

---

## 2026-05-03: Enhanced 401 Diagnostics for Live Codespaces Failures

**Status:** ✅ Complete; ready for live Codespaces runtime testing

**Scope:** Enhance logging in `PrismAuthExtensions` and MockBusinessApp debug endpoint to surface root cause evidence when HTTP 401 `invalid_token` occurs in live Codespaces.

**Changes Shipped:**

1. **Enhanced `OnAuthenticationFailed` logging** (`PrismAuthExtensions.cs`):
   - Added `token.kid` extraction (identifies which signing key the token expects)
   - Added `ASPNETCORE_ENVIRONMENT` display
   - Added computed `backchannel JWKS enabled` boolean (true when Development + KEYCLOAK_BACKCHANNEL_URL set)
   - For `SecurityTokenSignatureKeyNotFoundException`, now logs the JWKS metadata address that would be fetched

2. **Enhanced `/debug/auth` endpoint** (`MockBusinessApp/Program.cs`):
   - Added `aspNetCoreEnvironment` field
   - Added `backchannelJwksEnabled` computed boolean matching the auth validator logic
   - Fields now reveal whether the backchannel JWKS logic gate is open

**Diagnostic Contract:**

When a 401 occurs in Codespaces, operators can now quickly check:
- Is `KEYCLOAK_BACKCHANNEL_URL` set in the running BusinessApp process? (`curl https://.../debug/auth`)
- Does the token's `kid` match what the signing key cache has? (console log from `OnAuthenticationFailed`)
- What JWKS metadata URL is being used? (logged for signature key failures)
- Is the backchannel gate condition actually true? (`backchannelJwksEnabled` field)

**Test Results:**
- All 672 Core tests passing
- All 20 PrismAuthExtensions security tests passing
- Build clean (1 pre-existing nullability warning, not introduced by this change)

**Root Cause of User's Report:**

The user mentioned a "live Codespaces" failure at `https://organic-space-fortnight-77g9wvq6jxhxg97-44345.app.github.dev/dashboard` but the environment diagnosed was actually localhost (CODESPACE_NAME not set). The enhanced diagnostics will now clearly reveal whether the backchannel fix is active when the actual live Codespaces failure reproduces.

**Learnings:**

- "Do not guess; prefer logging/messages that reveal the real problem" — enhanced diagnostics ship preemptively before the next live failure
- Token `kid` is essential for signature key debugging but was previously omitted
- Boolean computed fields (`backchannelJwksEnabled`) make dual-gate logic transparent in diagnostics
- The `/debug/auth` endpoint is a first-class diagnostic tool; operators should `curl` it first when investigating 401s

