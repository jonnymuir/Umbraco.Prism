# Blathers PT2 Backend Security Fixes — Decision Record

**Branch:** `sec/pt2-backend`  
**Date:** 2026-04-30  
**Author:** Blathers (Backend Specialist)  
**Findings addressed:** SEC-PT2-003, SEC-PT2-004, SEC-PT2-006, SEC-PT2-009, SEC-PT2-010

---

## Decisions Made

### SEC-PT2-003 — Logout CSRF (commit 828b5d4)

**Decision:** `AccountController.Logout` changed from `[HttpGet]` to `[HttpPost] + [ValidateAntiForgeryToken]`. Razor views updated from `<a href="/auth/logout">` to `<form method="post">` + `@Html.AntiForgeryToken()` + `<button>`.

**Rationale:** Logout-via-GET is a well-known CSRF class. POST + antiforgery is the standard ASP.NET Core pattern. UX impact is minimal — the button still reads "Sign Out".

---

### SEC-PT2-004 — Missing Security Headers (commit 9f1f34e)

**Decision:** Implemented `PrismSecurityHeadersMiddleware` + `PrismSecurityHeadersOptions`. CSP ships as **Content-Security-Policy-Report-Only** (not enforced).

**Rationale for Report-Only CSP:** Umbraco backoffice uses inline scripts and styles that a strict enforced CSP would block. TestSite Razor views also have inline `<script>` blocks and `@Html.Raw(imageryCss)` inline styles. Enforcing CSP without a prior audit + nonce/hash rollout would break the backoffice. Report-Only lets us observe violations without breaking the site. Promoting to enforced CSP is a tracked follow-up.

**Other header choices:**
- `X-Frame-Options: SAMEORIGIN` (not DENY) — Umbraco backoffice may use same-origin iframes for media/content pickers.
- Backoffice exclusion: `/umbraco/` paths excluded by default (`ExcludeBackoffice: true`) to avoid interfering with CMS UI.
- HSTS only on HTTPS requests — avoids issues in local development over HTTP.

---

### SEC-PT2-006 — Ephemeral DataProtection Keys (commit 6c0e8e9)

**Decision:** Fixed in `TestSiteRuntimeLayout.cs` (TestSite layer), not in `PrismComposer` (Core library).

**Rationale:** The Core library cannot safely double-configure DataProtection — the host may already have configured it. The fix ensures TestSite always calls `PersistKeysToFileSystem` with a fallback path of `{ContentRoot}/App_Data/prism-keys/`.

**Remaining gap:** Encryption-at-rest (`ProtectKeysWith*`) and multi-instance key ring sharing (Azure Blob / Redis) are NOT addressed here. These require ops/infrastructure input and are documented as follow-up requirements.

---

### SEC-PT2-009 — Antiforgery Gap on JSON API Endpoints (commit 7a3b0ef)

**Decision:** Added `[IgnoreAntiforgeryToken]` to `BiometricController`, `PrismNotificationController`, and `PrismVinylNotificationController` — **deliberately exempting them**, not applying antiforgery.

**Rationale:** These are Capacitor native-app JSON API endpoints. Native apps cannot supply the ASP.NET Core antiforgery cookie+header pair (they do not share the browser cookie jar that the antiforgery system relies on). Applying `[ValidateAntiForgeryToken]` would break the mobile app.

**Alternative protections in place:**
1. Cookie auth with `SameSite=Lax` blocks cross-site form-encoded POST.
2. JSON-only `Content-Type: application/json` requirement triggers CORS preflight for cross-origin browser requests.
3. `IsCapacitorOrigin` check on the unauthenticated `Exchange` endpoint.

**Rule going forward:** Any NEW browser-facing form-POST endpoint MUST carry `[ValidateAntiForgeryToken]`. This is documented in both `PrismComposer` and each affected controller's XML doc comment.

---

### SEC-PT2-010 — IsCapacitorOrigin Allows http://localhost in Production (commit 11b8cbb)

**Decision:** `http://localhost` permitted only in Development environments. `capacitor://localhost` (iOS WebKit scheme) always permitted.

**Rationale:** `http://localhost` is the Android Capacitor emulator origin. In production, no real device uses `http://localhost` — Android production builds use `capacitor://localhost` or a custom scheme. Restricting `http://localhost` to Development eliminates the risk of a same-LAN browser page issuing credentialed requests to the Exchange endpoint in production.

**Implementation:** `IWebHostEnvironment` injected into `BiometricController`; `IsCapacitorOrigin` changed from static to instance method using `environment.IsDevelopment()`.

---

## Follow-Up Items (not addressed in this branch)

- **CSP enforcement:** Promote `Content-Security-Policy-Report-Only` → `Content-Security-Policy` after auditing inline script/style usage and applying nonces or hashes.
- **DataProtection at-rest encryption:** Add `ProtectKeysWith*` (DPAPI, certificate, or Key Vault) for production deployments.
- **Multi-instance DataProtection:** Document and provide a seam for Azure Blob / Redis key ring sharing.
- **SEC-PT2-005, SEC-PT2-007, SEC-PT2-008:** Not addressed in this pass — separate findings requiring different review.
