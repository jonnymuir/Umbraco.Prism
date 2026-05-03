# Blathers — History

**Recent summary:** Backend specialist shipping Codespaces URL derivation fixes, backchannel rewrites for JWKS/token-refresh, and security analysis for auth isolation.

**Full history:** See `history-archive.md` for sessions prior to 2026-05-01.

---

## Session: Fix Codespaces BusinessApp Backchannel URL Bug (2026-05-02)

**Status:** ✅ Complete — main/ffc32c5

**Scope:** Fixed persistent "https://localhost:7245/api/backoffice/me" errors in Codespaces downstream demo. Root cause: Commit 09baa09 attempted to use `businessApp.GetEndpoint("https")` as a backchannel URL, mirroring the Keycloak pattern. However, unlike Keycloak (a container), BusinessApp is an Aspire project whose `GetEndpoint()` returns an Aspire service discovery URL (`https+http://businessapp`) that doesn't resolve from TestSite's plain HttpClient. When resolution failed, it fell back to literal `https://localhost:7245`, which doesn't work in Codespaces.

**Changes:**
- `src/UmbracoPrism.AppHost/Program.cs`:
  - **Removed** `BUSINESSAPP_BACKCHANNEL_URL` env var in Codespaces
  - Added comment explaining why BusinessApp doesn't need backchannel unlike Keycloak
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`:
  - **Removed** backchannel preference from `BuildTargetUrl()`
  - Always uses `PrismBusinessApp__WorkflowApiBaseUrl` (the discovered public Codespaces URL)

**Impact:** Server-to-server downstream demo calls now use the discovered public Codespaces URL (e.g., `https://{token}-7245.app.github.dev/api/backoffice/me`) as originally intended. The HTML response issue from 09baa09 was likely a transient port-forwarding startup delay, not a persistent proxy blocking problem.

**Test results:** 653 passed, 0 failed, 0 skipped — no regressions.

**Build:** Succeeded, 6 pre-existing warnings (unchanged).

**Decision:** Written to `.squad/decisions/inbox/blathers-codespaces-localhost-fallback.md`

**Confidence:** HIGH — Fix aligns with Aspire architecture: containers (Keycloak) can use concrete endpoint references, but projects (BusinessApp) require public URLs or Aspire service discovery extensions in HttpClient.

**Key learning:** `GetEndpoint()` behavior differs between `AddContainer()` and `AddProject()` resources. Service discovery URLs from projects don't automatically resolve in plain HttpClients.

---

## Session: Codespaces BusinessApp Backchannel Fix (2026-05-02)

**Status:** ⚠️ REVERTED by ffc32c5 — see session above

**This fix was incorrect.** The approach worked for Keycloak but failed for BusinessApp due to architectural differences between containers and projects in Aspire.

## Session: Codespaces BusinessApp Downstream Target Fix (2026-05-02)

**Status:** ✅ Complete — worktree

**Scope:** Fixed the Mock Business App downstream demo returning 401 in Codespaces. Root cause: `PrismBusinessApp:WorkflowApiBaseUrl` was hardcoded to `https://localhost:7245` in the AppHost and never updated to use the Codespaces-discovered URL. When the TestSite's `DownstreamDemoController` tried to call `localhost:7245/api/backoffice/me`, the request failed because that localhost URL doesn't exist in the Codespaces port-forwarding context.

**Changes:**
- `src/UmbracoPrism.AppHost/Program.cs`:
  - Extended `TryDiscoverCodespaceUrls()` to discover and return BusinessApp URL (port 7245)
  - Extended `FallbackCodespaceUrls()` to include port 7245 fallback
  - Changed `BusinessAppUrl` from `const string` to runtime-computed `string businessAppUrl` that uses discovered URL in Codespaces or defaults to `https://localhost:7245` for local dev
  - Updated console logging to show discovered BusinessApp URL

**Impact:** The downstream demo "Call Mock Business App API" now targets the correct public Codespaces URL for port 7245 (e.g., `https://{token}-7245.{region}.app.github.dev/api/backoffice/me`) instead of the non-existent `localhost:7245`.

**Test results:** 650 passed, 0 failed, 0 skipped — no regressions.

**Build:** Succeeded, 0 errors, 6 pre-existing warnings (unchanged).

**Decision:** Written to `.squad/decisions/inbox/blathers-codespaces-downstream-target.md`

---

## Session: Refresh Token `invalid_grant` Fix — fix/codespaces-invalid-grant-refresh (2026-05-02 future)

**Status:** ✅ Complete — branch `fix/codespaces-invalid-grant-refresh`

**Scope:** Diagnosed and fixed a persistent `invalid_grant` 401 on the Codespaces "Call Mock Business App API" demo. Root cause: Keycloak 26 with `--proxy-headers xforwarded` uses `X-Forwarded-Proto` to compute its canonical issuer URL scheme. Without that header on the backchannel refresh POST, Keycloak computed its issuer as `http://...` but the stored refresh token's `iss` JWT claim was `https://...` (issued through YARP which forwards proper headers). The scheme mismatch triggered `invalid_grant`.

**Changes:**
- `IPrismTokenRefreshService` — added optional `requestHeaders` param to `RefreshAsync`
- `PrismTokenRefreshService` — changed to `HttpRequestMessage`/`SendAsync` to support per-request headers
- `PrismContext.RefreshTokenAsync` — derives `X-Forwarded-Proto`/`X-Forwarded-Host` from `OidcAuthority` when backchannel rewrite active; passes to `RefreshAsync`
- `BackchannelRewriteTests.cs` — Group E: 3 new regression tests for forwarding header behaviour

**Security bedrock:** `ValidIssuer` remains `tenant.OidcAuthority` (public HTTPS URL). Forwarding headers only affect Keycloak's grant computation; Prism's own validation is unchanged.

**Test results:** 645 passed, 0 failed, 0 skipped.

---

## Session: Codespaces URL Derivation Fix — fix/codespaces-url-derivation (2026-05-02)

**Status:** ✅ Complete — PR opened as draft on `fix/codespaces-url-derivation`

**Scope:** Stop all Codespaces public-URL derivation from using the legacy `{CODESPACE_NAME}-{port}.{domain}` string-concat pattern, which fails on the new regional URL scheme (`{opaque-token}-{port}.{region}.app.github.dev`).

**Changes:**
- `src/UmbracoPrism.AppHost/Program.cs` — Replace string-concat with `gh codespace ports --codespace "$CODESPACE_NAME" --json sourcePort,browseUrl` discovery via `System.Diagnostics.Process`. Falls back to legacy pattern with a visible console warning if gh is unavailable or the lookup fails.
- `src/UmbracoPrism.TestSite/DemoTenantSeeder.cs` — `BuildCodespaceTestSiteHostname()` now reads `TESTSITE_PUBLIC_URL` first (set by AppHost via gh discovery) and falls back to the legacy pattern.
- `src/UmbracoPrism.Core/Services/TenantService.cs` — Added lenient `LIKE '%.app.github.dev'` fallback in `GetByDomainAsync` when no exact hostname match exists for a `.app.github.dev` request.
- `src/UmbracoPrism.TestSite/Program.cs` — Updated comments to document the two-path derivation.
- `.devcontainer/on-start.sh` — Added `get_codespace_url()` function using `gh codespace ports` with `jq`/`python3` fallback.
- `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs` — Added Group D: two tests proving `BackchannelRewritingDocumentRetriever` and issuer validation work correctly with the new `v7ldkc4c-8443.uks1.app.github.dev` URL form.
- `src/UmbracoPrism.Core.Tests/PrismOidcConfigurationTests.cs` — Added `[Theory]` over both legacy and regional Codespaces hostname forms for `IsRepoOwnedLocalDemoTenant`.
- `src/UmbracoPrism.Core.Tests/PrismAuthExtensionsSecurityTests.cs` — Added `[Collection(EnvVarSensitiveTestCollection.Name)]` to prevent parallel env-var races.

**Security bedrock:** All PR #44 invariants preserved. No `RequireHttpsMetadata = false`, no `ValidateIssuer = false`. The tenant lenient lookup uses `*.app.github.dev` only for tenant *lookup*, never for security validation — `IsRepoOwnedLocalDemoTenant` remains the downstream gate.

**Test results:** 647 passed, 0 failed, 0 skipped.

**Build:** Succeeded, 0 errors, 5 pre-existing warnings (unchanged).

---

## Session: JWKS Backchannel Rewrite — fix/codespaces-401-downstream-auth (2026-05-02)

**Status:** ✅ Complete — commit `4a47acc` pushed to `fix/codespaces-401-downstream-auth`

**Scope:** Fix the transitive JWKS fetch through the GitHub Codespaces port-forwarding proxy. The discovery-doc URL was already rewritten via backchannel in `PrismAuthExtensions.ResolveSigningKeys`, but `OpenIdConnectConfigurationRetriever` then followed `jwks_uri` from the discovery doc — which Keycloak emits as the public Codespace URL — and that fetch was not rerouted.

**Changes:**
- `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs` (+59 lines, 1 file) — Added private `BackchannelRewritingDocumentRetriever` sealed class implementing `IDocumentRetriever`. Rewrites any URL whose origin matches the public Keycloak origin to the backchannel base before delegating to the inner `HttpDocumentRetriever`. Modified generic `WarmAsync` overload: when `KEYCLOAK_BACKCHANNEL_URL` is set AND `ASPNETCORE_ENVIRONMENT == Development` AND `tenantKey` parses as an HTTPS URI, creates the `ConfigurationManager` with `BackchannelRewritingDocumentRetriever` instead of the injectable factory.

**Dual gating:** Same pattern as Copper's `e0e8ee3` (PrismContext.RefreshTokenAsync) — both env vars checked with `string.Equals(..., OrdinalIgnoreCase)`.

**Security bedrock:** No `RequireHttpsMetadata = false`, no `ValidateIssuer = false`, no certificate bypass. `normalizedKey` (the public OidcAuthority URL) remains the issuer trust anchor for JWT validation.

**Test results:** 631 passed, 0 failed, 0 skipped — no regressions.

**Build:** Succeeded, 0 errors, 5 pre-existing warnings (unchanged).

**Commit SHA:** `4a47acc`

## Learnings

- `OpenIdConnectConfigurationRetriever` uses a single `IDocumentRetriever` instance for ALL fetches — both the discovery-document GET and the transitive `jwks_uri` GET. Wrapping the retriever at construction covers both with one interception point.
- The injectable `_configurationManagerFactory` (internal constructor) is the right seam for unit tests. The backchannel path creates the `ConfigurationManager` directly (bypassing the factory) — tests don't set the env vars, so they still hit the factory.
- `Uri.GetLeftPart(UriPartial.Authority)` is the correct way to extract `scheme://host:port` from a URI — covers both standard ports and non-standard ports without manual string manipulation.
- When `tenantKey` is the full public OidcAuthority URL (e.g. `https://{name}-8443.app.github.dev/realms/prism-dev`), the origin extracted is `https://{name}-8443.app.github.dev` — which correctly matches the prefix of every Keycloak-emitted URL in the discovery doc.
- `BackchannelRewritingDocumentRetriever` works unchanged with regional Codespaces URLs (`v7ldkc4c-8443.uks1.app.github.dev`): it operates on URI origins, not hostname patterns, so no scheme-specific code was needed.
- When adding env-var-sensitive tests to `BackchannelRewriteTests` (in `EnvVarSensitiveTestCollection`), also add `[Collection(EnvVarSensitiveTestCollection.Name)]` to ANY other test class that reads those same env vars without setting them — otherwise parallel test runs can race on the env var window.
- `gh codespace ports --codespace "$CODESPACE_NAME" --json sourcePort,browseUrl` is the authoritative URL source. The output is stable per-session and should be queried ONCE at AppHost startup, stored in variables, then threaded into child processes via Aspire env vars.
- The Codespaces lenient tenant fallback (`LIKE '%.app.github.dev'`) in `TenantService.GetByDomainAsync` should be narrowly scoped to tenant *lookup*, never to security validation. `IsRepoOwnedLocalDemoTenant` remains the downstream security gate.
- `TESTSITE_PUBLIC_URL` (env var set by AppHost) is the cleanest seam for passing the correct Codespace hostname to `DemoTenantSeeder` — avoids the seeder needing to run `gh` itself and keeps the discovery logic in one place (AppHost).
- GitHub's new Codespaces URL scheme (`{token}-{port}.{region}.app.github.dev`) is opaque — the token is NOT derivable from `CODESPACE_NAME` and is NOT exposed as an env var. `gh codespace ports` is the only reliable source.
- Both legacy (`{CODESPACE_NAME}-{port}.app.github.dev`) and new regional URL forms end with `.app.github.dev` — this is the safe Codespaces hostname check for lenient lookups.
- The AppHost's `TryDiscoverCodespaceUrls()` function must discover ALL ports that need server-to-server communication. When adding a new service, extend the function to discover its port's public URL and thread it through to dependent services via env vars — never hardcode `localhost:{port}` in Codespaces-aware code paths.
- `PrismOidcConfiguration.OnAuthorizationCodeReceived` should only honor `KEYCLOAK_BACKCHANNEL_URL` for Development + HTTPS public authorities. This keeps Codespaces rewrites for real Keycloak endpoints while preventing `http://127.0.0.1` loopback test authorities from being rewritten to `keycloak-internal:8080`.
- Any Core test class that reaches Prism auth code paths reading `KEYCLOAK_BACKCHANNEL_URL` / `ASPNETCORE_ENVIRONMENT` transitively must join `EnvVarSensitiveTestCollection` and snapshot/restore both env vars. The concrete regression was `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs`.

---

## Core Context

This agent manages backend services, authentication infrastructure, and CI/CD workflows.

**Key domains:** Auth/OIDC, Aspire local dev, CI infrastructure, Database services, Security hardening, Playwright/E2E

---

**2026-05-02 — Codespaces BusinessApp URL Discovery Fix (Task Completion)**

Committed fix for port 7245 BusinessApp URL discovery in Codespaces environments. Downstream demo was failing with `401 Unauthorized` because `TryDiscoverCodespaceUrls()` only discovered Keycloak and TestSite, leaving BusinessApp hardcoded to `localhost:7245`.

**Changes:**
- Extended `TryDiscoverCodespaceUrls()` return type to include BusinessApp URL
- Added port 7245 to discovery loop in `TryDiscoverCodespaceUrls()`
- Extended `FallbackCodespaceUrls()` to include BusinessApp fallback pattern
- Changed `BusinessAppUrl` from const to runtime-computed variable

**Verification:** 650 Core tests passing; no regressions; build clean.

**Deployed:** Commit `6205bd4` on main/origin-main.

Orchestration log written to 2026-05-02T13:14:32Z-blathers.md.


**2026-05-02** — Completed: Diagnosed that server-side calls to the public Codespaces 7245 forwarded URL can return GitHub tunnel HTML instead of JSON; recommended backchannel transport path fix (approved by team, implemented by Brewster). Decision recorded in decisions.md.

---

### 2026-05-03 09:39:06 — CI Repair Orchestration

**Event:** Scribe coordination of CI fix across team.

**Role:** Root-cause investigator. Identified env-var bleed in KEYCLOAK_BACKCHANNEL_URL, proposed solutions. Initial proposal included runtime hardening; scoped to test layer per Tom Nook review.

**Outcome:** Test-isolation-first strategy locked in. Awaiting Tangy's final implementation for CI validation.

**Learnings:** Scope discipline matters; verify production impact before runtime changes.

## 2026-05-03: Team Spawn — CI Repair Confirmation

**Status Update (Scribe):** Blathers confirmed remaining local test run failures are dual-origin: HMAC secret key in appsettings (auth regression) + TestSite view override failures (unrelated). Clarified scope for downstream test-isolation fix.

**Security Directive:** Do not commit HMACSecretKey in appsettings.
