# Blathers — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Tom Nook: Architecture, scope, code review, leadership
- Isabelle: Web Components, Storybook, UI logic, accessibility
- Tangy: Testing methodology, edge cases, test coverage
- Scribe: Session logging, decisions, team memory

## Architecture & Services

**Middleware Stack:**
1. `PrismTenantMiddleware` — Hostname → Tenant Cache lookup
2. `PrismBrandingMiddleware` — CSS variable overrides injection
3. Request scope: `IPrismContext` (current tenant + theme per request)

**Core Services:**
- `TenantService` — CRUD, domain resolution
- `BrandingService` — CSS variable management
- `MobileBundleService` — Capacitor bundle generation (iOS/Android)
- `SecretVaultService` — Azure Key Vault integration (Managed Identity in prod, CLI in dev)
- `PrismTokenService` — Token extraction & refresh
- `PrismUserContext` — High-perf user claims + tenant access

**Identity (Stateless OIDC):**
- Dynamic OIDC via `PrismOidcConfiguration` — ClientId/Authority swapped per request
- All CIAM credentials stored in Azure Key Vault (fetched at runtime)
- No hardcoded secrets; dev uses `az login` CLI auth
- Downstream auth via `AddPrismAuthentication` (secure token propagation to internal APIs)

**Database & Persistence:**
- Schema: `TenantId`, `DomainName`, `ClientId`, secret key refs, Branding (JSON), MobileAppConfig (JSON), MobileBrandingOverrides (JSON)
- Migrations: `PrismMigrationPlan` (AddIdentityColumns, AddMobileAppConfigColumn, etc.)
- Auto-applied on startup via `PrismMigrationHandler`

## Key Patterns

1. **Naming:** `IPrismXxx` (interfaces), `XxxService` (services), `PrismXxxMiddleware` (middleware)
2. **Config:** `appsettings.json` under `"Prism"` section; key settings: `VaultUri` (triggers auth), `AdminGroups.GroupAliases`
3. **Authorization:** `PrismAdminHandler/Requirement` (default: `["admin"]`), `PrismTenantHandler/Requirement` (authenticated + in tenant context)
4. **Mobile Detection:** Query flag (`?prismMobile=1`), user-agent (`PrismMobile`), or cookie

## Learnings & Handoff (2026-03-22)

**From Tom Nook Architecture Review:**
- Token resilience & auth standardization marked P0
- Blocking async in OIDC config (IssuerSigningKeyResolver, OnAuthorizationCodeReceived use `.GetAwaiter().GetResult()`) creates bottleneck; needs non-blocking event hook or pre-warmed metadata cache
- Token refresh has no Polly retry logic; transient CIAM outages cause all refresh attempts to fail → users logged out
- Authorization inconsistency: PrismTenantHandler checks Entra tenant ID; PrismAdminHandler checks local Umbraco groups; should standardize on Entra groups
- OIDC metadata cache (static, app-lifetime) never invalidates; CIAM key rotations require restart; need fallback on 401 + shorter TTL (12 hours)
- Tenant cache pre-warming needed (background task 5 min before expiry)
- Mobile bundle security: no rate limit, no StartUrl validation (same-domain check needed), no Capacitor.ts syntax validation

**Decisions inbox (3 P0 items):**
1. Extract TokenRefreshService with Polly retry/circuit breaker (you own this)
2. Standardize authorization on Entra groups (you own this)
3. Document tenant rejection policy

**Next:** Design TokenRefreshService with exponential backoff + circuit breaker; plan Entra group integration (sync or Graph API lookup)

## Learnings & Handoff (2026-03-22, P0 #2 implementation complete)

**Issue #2: Remove blocking OIDC calls from request path**

- `IssuerSigningKeyResolver` is a *synchronous* delegate in `Microsoft.IdentityModel.Tokens` — you cannot await inside it. The only escape from blocking I/O is to ensure keys are already in memory before it runs.
- Pre-warming in `PrismTenantMiddleware.InvokeAsync` is the correct hook: it's the first async gate on every request, runs before any auth validation, and already resolves the tenant whose keys are needed.
- `IPrismSigningKeyCache` / `PrismSigningKeyCache`: singleton, `ConcurrentDictionary`-backed, 12h TTL per `entraTenantId`. Uses `IHttpClientFactory` named client `"prism-oidc-metadata"` to avoid socket exhaustion.
- `PrismAuthExtensions.AddPrismAuthentication` (downstream APIs) still has the same sync-blocking pattern. Deferred: only cold-start first-request per tenant blocks; subsequent calls are `ConfigurationManager` cache hits. Should be addressed with #3 or a dedicated slice.
- Removed unused `using Microsoft.IdentityModel.Protocols;` from `PrismOidcConfiguration.cs` but kept `Microsoft.IdentityModel.Protocols.OpenIdConnect` because `OpenIdConnectResponseType` and `OpenIdConnectResponseMode` live there.
- When updating `PrismTenantMiddleware.InvokeAsync` signature, must also update existing unit tests that call `InvokeAsync` directly with positional args (ASP.NET DI injection doesn't apply in unit tests).

- Blocking OIDC key retrieval is currently in two request-path resolvers:
  - `PrismOidcConfiguration.PostConfigure` -> `TokenValidationParameters.IssuerSigningKeyResolver`
  - `PrismAuthExtensions.AddPrismAuthentication` -> `TokenValidationParameters.IssuerSigningKeyResolver`
- Both resolver paths call `GetConfigurationAsync(...).GetAwaiter().GetResult()`, which creates sync blocking under load.
- Token refresh logic currently lives in `PrismContext.RefreshTokenAsync` and performs single-attempt network calls without retry or breaker behavior.
- OIDC authorization-code exchange (`PrismOidcConfiguration` -> `OnAuthorizationCodeReceived`) also calls the token endpoint and should share resilience behavior to avoid drift.
- First safe PR sequence:
  1. #2: Introduce async-warmed tenant signing-key cache and remove sync blocking resolvers.
  2. #3: Add retry/backoff/circuit-breaker on refresh path with tests, then consolidate token endpoint logic.
- Validation priorities for first slices: resolver cache-hit/miss tests, refresh transient-vs-non-transient tests, per-tenant concurrency checks.

## Learnings & Handoff (2026-03-22, Issue #3 — Resilient Token Refresh, COMPLETE)

**What shipped:**
- Added `IPrismTokenRefreshService` / `PrismTokenRefreshService` (Polly 8.6.6)
- Pipeline order: CircuitBreaker (outer) → Retry (inner) → HTTP call
  - Outer placement means the circuit breaker samples ONE failure per exhausted retry sequence (not per individual HTTP attempt)
- `PrismContext.RefreshTokenAsync` now delegates HTTP transport to `IPrismTokenRefreshService`; orchestration/cookie-update logic stays in the context
- All resilience settings under `"Prism:TokenRefresh"` in `appsettings.json` — no hardcoded values
- Token values never logged; only status codes, retry counts, exception type names

**Polly v8 gotchas (avoid repeating):**
- `RetryStrategyOptions.MaxRetryAttempts` minimum is **1** (Polly throws `ValidationException` for 0) — use `Math.Max(1, n)` in test-option helpers
- `Enumerable.Repeat(singleInstance, n)` shares one `HttpResponseMessage` across retries; on the second attempt `HttpClient` internals can consume/invalidate the shared object, causing `ObjectDisposedException` instead of the expected 5xx response and silently cutting off retries. Always use a factory delegate (`Func<HttpResponseMessage>`) in stub handlers so each call creates a fresh instance.
- Pipeline execution order in v8: **first strategy added = outermost = executes first**. `AddCircuitBreaker().AddRetry()` → CB outer, Retry inner (correct). `AddRetry().AddCircuitBreaker()` → CB samples every individual attempt, which causes circuit to trip earlier and makes retry/CB interactions harder to reason about.
- `BrokenCircuitException` is in namespace `Polly.CircuitBreaker`; catch it before the broader `Exception` catch for clean "circuit open" log messages.

**Tests (19 passing — 5 new):**
- `RefreshAsync_ReturnsSuccess_OnFirstAttempt`
- `RefreshAsync_RetriesOnTransientFailure_AndSucceedsAfterRetry`
- `RefreshAsync_ReturnsFailure_WhenAllRetriesExhausted`
- `RefreshAsync_CircuitBreaker_OpensAfterThresholdFailures`
- `RefreshAsync_DoesNotRetry_On4xxClientError`

**Follow-up items (not in scope of this issue):**
- Per-tenant circuit breakers — current pipeline is shared app-wide; one CIAM endpoint going down blocks all tenants
- OpenTelemetry/AppInsights integration for retry telemetry once observability stack is available
- `PrismAuthExtensions.AddPrismAuthentication` downstream resolver still has sync-blocking pattern (deferred from #2); should share resilience service too

## Learnings & Handoff (2026-03-22, Local tunnel dev automation)

- Added robust local script at `scripts/dev/start-trycloudflare.sh` that chains three operations in one run: trycloudflare startup, Entra redirect URI update, and `prismTenants.hostname` update in SQLite.
- Config persistence is now explicit via repo-root `.prism_tunnel.conf` and enforced to mode `600`; loader is key-value parsing (not `source`) to avoid shell execution from config content.
- Tunnel startup is treated as a bounded wait (90s timeout) with actionable diagnostics from cloudflared logs; fail-fast if process exits before URL discovery.
- Entra redirect URI update preserves existing redirect URIs by reading `web.redirectUris[]`, appending only when missing, and sending the merged list.
- SQL update safety is handled by strict hostname validation (`[A-Za-z0-9.-]`, dot required, no edge dot/hyphen) plus numeric `TENANT_ID` enforcement before issuing update.
- Script traps `INT`/`TERM`/`EXIT` and always cleans cloudflared process plus temporary log file to reduce local environment drift.

## Learnings & Handoff (2026-03-22, Tunnel input clarity + tenant selector UX)

- Tunnel script now uses `ENTRA_APP_CLIENT_ID` terminology end-to-end (Entra Application (Client) ID) and keeps backward compatibility by loading legacy `ENTRA_APP_OBJECT_ID` when the new key is missing.
- Config persistence is now one-way migration: script always writes `ENTRA_APP_CLIENT_ID` to `.prism_tunnel.conf` and no longer saves the legacy key.
- Tenant input moved from numeric-only prompt to selector prompt (`name` or numeric `id`); script resolves selector to canonical `TENANT_ID` before applying DB updates.
- Name lookup behavior is fail-closed:
  - 0 matches -> explicit "no tenant found" error
  - >1 matches -> explicit duplicate-name error with matching ids to force disambiguation
- Ready summary now prints both `Tenant id updated` and `Tenant name resolved` so operator can confirm target row before continuing.

## Learnings & Handoff (2026-03-22, Redirect callback alignment)

- Prism auth callback path is `/signin-oidc` (as configured in Prism OIDC setup), so tunnel automation and docs must use that same callback path to avoid Entra redirect mismatches.
- Updated local tunnel script default callback path and README tunnel redirect URI examples to `/signin-oidc` for consistency between runtime auth behavior and developer setup guidance.

## Learnings & Handoff (2026-03-22, trycloudflare callback rotation safety)

- Local tunnel script now prunes only stale `*.trycloudflare.com/signin-oidc` redirect URIs before adding current callback URI.
- Non-trycloudflare redirect URIs are preserved unchanged to avoid destructive Entra app mutation.
- Script guarantees current tunnel callback URI exists exactly once and prints a concise stale-prune count.
- README now recommends `az login --allow-no-subscriptions` for dev scenarios where selecting the right Entra tenant is required without an active Azure subscription.

## Learnings & Handoff (2026-03-22, trycloudflared temp log hygiene)

- `scripts/dev/start-trycloudflare.sh` now creates tunnel temp logs under `artifacts/logs/trycloudflared/` instead of repo root to keep workspace root clean.
- Keep cleanup behavior unchanged: temp log file is still removed on script exit via the existing `cleanup` trap.

## Learnings & Handoff (2026-03-22, mktemp fallback hardening)

- Tunnel log creation now attempts `mktemp` in `artifacts/logs/trycloudflared` first and falls back to `${TMPDIR:-/tmp}/prism-trycloudflared-logs` if that creation fails for any reason.
- Script only exits with an error when both `mktemp` attempts fail, and summary output still reports the active tunnel log directory.

## Learnings & Handoff (2026-03-22, tunnel log creation without mktemp)

- Tunnel temp log creation in `scripts/dev/start-trycloudflare.sh` no longer depends on `mktemp`; it now creates files directly with shell redirection after real write probes.
- Candidate log directories are attempted in a fixed order: repo artifacts, TMPDIR fallback folder, `/tmp` fallback folder, then `$HOME/.cache` (if `HOME` is set).
- Failure output now lists all attempted directories and gives actionable guidance (permissions, disk space, TMPDIR/HOME checks).

## Learnings

- 2026-03-28 (Issue #6): Precomputing normalized branding CSS declarations in `TenantService` and storing them on `PrismTenant` removes repeated per-request trimming/concatenation work in `PrismBrandingMiddleware` while preserving tenant-specific override behavior.
- 2026-03-28 (Issue #6): Cache coherence for branding updates is preserved by existing domain cache invalidation; when tenant updates invalidate host entries, refreshed tenant loads rebuild both override dictionaries and precomputed CSS declarations.
- 2026-03-28 (Issue #6): Middleware should keep a dictionary-based fallback path for safety so tests and non-cached tenant objects remain behavior-compatible even when precomputed declarations are absent.
- 2026-03-28 (Cross-agent): Tangy's parallel cache-coherence tests validated the optimization assumptions, confirming no cross-tenant branding bleed and correct same-tenant refresh behavior.
- 2026-03-28 (PrismAuth downstream hardening): `PrismAuthExtensions` key resolution now mirrors the OIDC runtime pattern: snapshot read first, background warm trigger when `ShouldRefresh`, and strict fail-closed return when cache is expired or requested `kid` is missing.
- 2026-03-28 (PrismAuth downstream hardening): `AddPrismAuthentication` now configures JWT options via DI (`Configure<IPrismSigningKeyCache>`) so resolver logic can stay cache-only and avoid sync request-thread metadata I/O.
- 2026-03-28 (PrismAuth downstream hardening): Non-blocking behavior is testable by using a never-completing warm task and asserting resolver return timing + key return correctness on cached snapshot paths.

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.

## Learnings (2026-03-29 — GitHub Release Workflow)

**Workflow change:** Updated `.github/workflows/package-release.yml` to automatically create a GitHub Release on every `v*` tag push.

**Key additions:**
- Added `permissions: contents: write` at job level (required for `softprops/action-gh-release@v2` to create releases via `GITHUB_TOKEN`).
- Added "Extract release notes from CHANGELOG" step using `awk` to extract the section between the current tag's `## [vX.Y.Z]` heading and the next `## [` heading. Pattern: `awk "/^## \[${TAG}\]/{found=1; next} found && /^## \[/{exit} found{print}"`.
- Added "Create GitHub Release" step using `softprops/action-gh-release@v2` with the `.nupkg` from `artifacts/` attached, tag name as release title, extracted CHANGELOG section as body, `generate_release_notes: false`.
- GitHub Release creation is unconditional (not gated on `NUGET_API_KEY`); NuGet publish remains gated as before.

**CHANGELOG extraction pattern (Mabel's format):**
Headings: `## [v1.2.0] — 2026-03-28`. The `awk` script starts capturing after the matching heading line and stops before the next `## [` line, giving the full section body without the heading itself.

## Learnings (Issue #14 — Biometric Registration Endpoint)

- **RefreshTokenEnc column**: `PrismDeviceCredential` schema from PR #30 deliberately deferred `RefreshTokenEnc`. Added via `AddRefreshTokenEncColumn` migration chained in `PrismMigrationPlan` as `"add-refresh-token-enc"`.
- **AES-256-GCM encryption**: `RefreshTokenEncryptionService` uses AES-GCM with 12-byte random nonce, 16-byte auth tag. Wire format: `Base64([nonce][ciphertext][tag])`. Key is base64-encoded 32-byte value from `Prism:Biometric:EncryptionKey`.
- **Controller pattern for mobile endpoints**: `BiometricController` uses `[Route("umbraco/prism/mobile/biometric")]` with `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` — distinct from backoffice Management API controllers which use `[VersionedApiBackOfficeRoute]`.
- **User OID extraction**: Claims from PrismMemberCookie use `oid` (short) or `http://schemas.microsoft.com/identity/claims/objectidentifier` (long form) — always check both.
- **Refresh token from session**: Use `HttpContext.AuthenticateAsync("PrismMemberCookie")` → `authResult.Properties.GetTokens()` → find `"refresh_token"`. Same pattern as `PrismContext.GetAuthorizationHeaderAsync`.
- **DB testing with Moq**: Use `Mock<IUmbracoDatabase>()` + `Mock<IUmbracoDatabaseFactory>()` pattern (matches `TenantServiceCacheStrategyTests`). Don't hand-roll `IUmbracoDatabase` — the interface has 100+ members.
- **Upsert pattern**: Lookup by `(TenantId, DeviceId)` using `db.FirstOrDefault<PrismDeviceCredentialSchema>()` then update or insert. The unique index `IX_prismDeviceCredentials_TenantId_DeviceId` enforces one credential per device per tenant.
- **Key files**: `BiometricController.cs`, `RefreshTokenEncryptionService.cs`, `IRefreshTokenEncryptionService.cs`, `BiometricRegistrationRequest.cs`, `BiometricRegistrationResponse.cs`, `AddRefreshTokenEncColumn.cs`.

## Learnings (Issue #15 — Biometric Exchange Endpoint)

- **Exchange is unauthenticated**: `[AllowAnonymous]` on the action overrides the class-level `[Authorize]` on `BiometricController`. The BiometricToken JWT IS the credential — no cookie required.
- **Token validation flow**: `BiometricTokenService.ValidateToken()` → verify tenantId matches request tenant → hash token → DB lookup by `TokenHash` → assert not revoked/expired → verify DeviceId + UserId binding.
- **Cross-user protection**: Always verify `credential.UserId == claims.UserOid` after DB lookup (security note from Copper's review of #14). Prevents token substitution attacks.
- **DeviceId binding check**: JWT `sub` claim (DeviceId) must match DB row's `DeviceId`. Mismatch returns specific `device_mismatch` error code (distinct from generic `biometric_token_invalid`).
- **Entra token refresh orchestration**: Replicated from `PrismContext.RefreshTokenAsync` — build token endpoint URL from `tenant.EntraTenantId`, get client secret from `ISecretVaultService`, call `IPrismTokenRefreshService.RefreshAsync`. Controller injects both services directly.
- **Rolling refresh token rotation**: On every successful exchange, re-encrypt the new refresh token (or fall back to existing if Entra doesn't return a new one) and update `LastUsedAt`. Matches v1 hard requirement.
- **Cookie issuance**: Build `ClaimsIdentity` with `oid` (user OID) and `tid` (Entra tenant ID — NOT Prism internal ID) claims. Store `access_token`, `refresh_token`, `expires_at` in `AuthenticationProperties`. Call `HttpContext.SignInAsync("PrismMemberCookie", principal, authProps)`.
- **Important distinction**: Biometric token stores Prism internal tenant ID (`tenant.Id.ToString()`) as `tid` claim, but the cookie principal needs the Entra tenant ID (`tenant.EntraTenantId`) because `PrismContext.IsPrincipalBoundToCurrentTenant` checks against `EntraTenantId`.
- **Test pattern for exchange**: `BuildExchangeScenario` helper issues a valid JWT, creates a matching DB record with encrypted refresh token, and wires up `IAuthenticationService` mock for `SignInAsync` verification. 18 tests cover all error paths.
- **Error code convention**: `biometric_token_invalid` (catch-all for bad JWT, not found, revoked, expired, user mismatch), `device_mismatch` (specific), `credential_refresh_failed` (Entra-side failures).
- **Key files added**: `BiometricExchangeRequest.cs`.

## Learnings (Issue #27 — Multi-tenant Boundary Validation)

- **Client-side keystore audit (biometric-bridge.ts)**: All `SecureStorage` keys use hostname strings (`tenantHost`), not integer tenant IDs. Key pattern: `prism_biometric_token_{hostname}`. This is architecturally correct — hostnames are globally unique and the client never needs to resolve numeric IDs. Documented with JSDoc block.
- **Exchange tenant mismatch error code**: Changed from generic `biometric_token_invalid` to explicit `tenant_mismatch`. Defence-in-depth: even though JWT signature validation implicitly covers tenant binding, the explicit assertion prevents a misconfigured signing key from leaking credentials across tenants.
- **All DB queries already included TenantId predicates**: Register (`TenantId + DeviceId + UserId`), Exchange (`TokenHash + TenantId + UserId`), Unenrol (`TenantId + UserId + DeviceId`), Admin Revoke (`DeviceId + TenantId`). Added verification tests to lock this invariant.
- **DeviceAdminController was already tenant-scoped**: The `Revoke` action queries by `DeviceId + TenantId`, so admin of Tenant A cannot see or delete Tenant B's devices. Added explicit TenantId-predicate verification test alongside the existing cross-tenant 404 test.
- **Test count**: 159 → 165 after adding 6 tenant boundary tests.

## Learnings (Reference Member Portal — Test Site Improvements)

- **OIDC prompt override pattern**: `OpenIdConnectDefaults.PromptKey` does NOT exist in the ASP.NET Core OIDC package bundled with Umbraco 17 / .NET 10. Use a custom key (`"PrismPrompt"`) in `AuthenticationProperties.Items` and read it in `PrismOidcConfiguration.OnRedirectToIdentityProvider` before defaulting to `"select_account"`.
- **Registration route**: `/auth/register` mirrors `/auth/login` but sets `properties.Items["PrismPrompt"] = "create"` to trigger Entra CIAM's sign-up flow. No new dependencies needed.
- **MemberDashboardController placement**: Lives in `UmbracoPrism.Core/Controllers/` alongside `AccountController` and `BiometricController`. Uses `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` — same pattern as `BiometricController`.
- **Claim extraction for display**: User OID uses dual-claim check (`oid` / long-form URI). Display name uses `name` → `preferred_username` fallback. Email uses `email` → `preferred_username`.
- **View discovery for Core controllers**: Views for controllers in `UmbracoPrism.Core` are resolved from the TestSite's `Views/` folder (e.g. `Views/MemberDashboard/Index.cshtml`). This works because the TestSite references Core as a project and MVC's default view discovery finds them.
- **HomePage redesign**: Removed the inline `CallBackOfficeAsync` demo code and design-token showcase. Replaced with a member-portal landing page that shows different hero content based on authentication state. Preserved `prism-mobile-user-agent-demo` tag helper, mobile CSS overrides, and `prism-debug` tag helper.
- **CSS variable tokens**: All portal and dashboard styles use existing branding CSS variables (`--prism-primary`, `--prism-surface`, `--prism-radius`, etc.) so they automatically adapt per-tenant. Added `--prism-nav-height`, `--prism-dash-icon-size`, `--prism-dash-icon-radius` to `prism-components.css`.

## Learnings (Content Type and Starter Content Seeders)

- **Seeder registration pattern**: `PrismContentTypeSeeder` and `PrismStarterContentSeeder` registered in `PrismComposer` using `builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, T>()` — runs after Umbraco is fully booted (`runtimeState.Level >= RuntimeLevel.Run`).
- **PrismContentTypeSeeder**: Creates `homePage` and `memberDashboard` document types idempotently on startup using `IContentTypeService.Get()` guard check. Uses `ContentType(shortStringHelper, -1)` constructor with `-1` parent for root-level types. `AllowedAsRoot = true` only on `homePage`.
- **v17 content type API**: `IContentTypeService.Save()` is marked obsolete but still works. Recommended approach is separate Create/Update methods, but Save remains functional for both operations. Warning is non-blocking.
- **PrismStarterContentSeeder**: Opt-in via `PrismConfiguration.SeedStarterContent` flag (default: false). Only runs if flag is true AND content tree is empty (`contentService.GetRootContent().Any()` returns false). Creates Home page at root, Dashboard as child, and publishes both.
- **v17 content creation & publishing**: `IContentService.Create()` returns `IContent` directly (not a result object). Save using `contentService.Save()` which returns an `Attempt<T>` result. Publish using `contentService.Publish(content, new[] { "*" })` for all cultures. Check `result.Success` before proceeding.
- **Configuration model pattern**: Created `PrismConfiguration.cs` in `/Models/` with `SectionName = "Prism"` constant. Registered in `PrismComposer` using `builder.Services.Configure<PrismConfiguration>(builder.Config.GetSection(PrismConfiguration.SectionName))` — matches existing pattern from `PrismTokenRefreshOptions` and `PrismBiometricOptions`.
- **TestSite appsettings**: Added `"Prism": { "SeedStarterContent": true }` to enable seeding. This creates a functional member portal tree on first run without manual Umbraco backoffice setup.
- **Idempotency guarantee**: Both seeders are safe to run on every startup. `PrismContentTypeSeeder` checks existence before creating types. `PrismStarterContentSeeder` exits early if content already exists. No duplicate content risk.
- **Package consumer UX**: With both seeders, any Prism consumer can install the package, set `SeedStarterContent: true`, and immediately have a working member dashboard without backoffice intervention. Document types are always created automatically.
- **Blueprint feature deferred**: Initial implementation included `IContentService.CreateContentFromBlueprint()` for a "Member Dashboard Template" blueprint, but that API is obsolete and scheduled for removal in v18. Removed from seeder to avoid future breaking changes. Teams can manually create blueprints in the backoffice if needed.
- **Key files**: `PrismConfiguration.cs`, `PrismContentTypeSeeder.cs`, `PrismStarterContentSeeder.cs`, updated `PrismComposer.cs` and `appsettings.json`.

## Work Summary (2026-03-29)

Completed implementation of PrismContentTypeSeeder and PrismStarterContentSeeder notification handlers. Both handlers register in PrismComposer and run on UmbracoApplicationStartedNotification.

**Build Status:** 0 errors (1 non-blocking deprecation warning on Save()). **Test Status:** All 165 tests pass.

**Package consumer impact:** With the seeder enabled via `"Prism:SeedStarterContent": true`, any consumer can install Prism and immediately get a working member portal with document types and starter content, no backoffice intervention required. This significantly reduces onboarding friction.

**Documented in** `.squad/decisions/decisions.md` under "Decision: Content Type & Starter Content Seeders".

## Learnings (2026-03-30 — Demo Controller Relocation)

**DownstreamDemoController moved from Core → TestSite:**
- Demo/example controllers have no place in the NuGet package. Core is for reusable utilities and platform services; TestSite is the correct home for demo/example code.
- Namespace updated to `UmbracoPrism.TestSite.Controllers`; route attribute `[Route("api/prism/downstream-demo")]` unchanged — the view in MemberDashboard.cshtml continues to call `/api/prism/downstream-demo` with no modifications needed.
- TestSite already references Core via ProjectReference, so `IPrismContext` and `UmbracoPrism.Core.Models` remain available without any new package references.
- No explicit registration existed for the controller in Core (Umbraco's `AddComposers()` / `AddWebsite()` scans assemblies automatically).

## Learnings (2026-04-20 — Safari Web Inspector Biometric Enrollment Debug Fix)

**Problem:** Safari Web Inspector reconnects to iOS page after SSO redirect with ~1-2 second delay. The `[Prism Enroll]` biometric enrollment script (injected by `PrismBrandingMiddleware.BuildBiometricEnrollScriptTag()`) fires immediately on page load, before Safari reconnects, causing all console.log messages to be lost.

**Fix:** Wrapped the biometric enrollment IIFE invocation in a `setTimeout(..., 2500)` call to delay execution by 2.5 seconds, giving Safari Web Inspector time to reconnect after navigation. Added an immediate "heartbeat" console.log (`'[Prism Enroll] page loaded — enrollment script will run in 2.5s...'`) outside the setTimeout that fires instantly, confirming Safari is connected before the main logic runs.

**Key Files:** `PrismBrandingMiddleware.cs` (`BuildBiometricEnrollScriptTag` method)

**Impact:** This is a debug-only enhancement. Production behavior unchanged. Jonny can now see all enrollment console logs when debugging iOS biometric flows via USB-connected Safari Web Inspector.

## Learnings (2026-04-20 — localStorage-based Debug Replay System)

**Problem:** The `setTimeout(..., 2500)` hack from the previous fix was a speculative workaround. The real issue: WKWebView's `console.log` output can fire before Safari Web Inspector reconnects after navigation/redirect, causing logs from `[Prism Enroll]` and `[Prism Bio]` scripts to be lost.

**Fix:** Implemented a localStorage-based debug replay system:
1. Created `__prismDebug` helper that wraps `console.log` and also persists each log entry to localStorage (rolling 50-entry buffer)
2. At the start of each script, call `__prismDebug.replay()` which reads the buffer, re-emits all stored logs to console, and clears the buffer
3. Replaced all `console.log('[Prism Enroll]` and `console.log('[Prism Bio]` calls with `__prismDebug.log(...)`
4. Reverted the `setTimeout(..., 2500)` wrapper — scripts now run immediately on page load

**Result:** Even if logs fire before Safari connects, they're replayed on the next page where Safari is definitely connected. This gives Jonny full visibility into the enrollment and biometric startup flows without artificial delays.

**Key Files:**
- `PrismBrandingMiddleware.cs` (`BuildBiometricEnrollScriptTag` method) — enrollment script
- `MobileBundleService.cs` (`BuildPlaceholderIndex` method) — biometric startup script in `www/index.html`

**Impact:** Debug-only enhancement. Production behavior unchanged. The helper uses localStorage which persists within the same app origin — safe for debug purposes, no effect on authentication or app functionality.

## Learnings (2026-04-20 — Authentication Timing Issue in PrismBrandingMiddleware Investigation)

**Problem:** Jonny discovered that `context.User.Identity?.IsAuthenticated` is ALWAYS false inside `PrismBrandingMiddleware.InvokeAsync` (line 33), even after successful login. This prevents `injectBiometricEnroll` from ever being true, so the biometric enrollment script never gets injected into mobile app pages.

**Root Cause — Pipeline Order Issue:**

The `PrismBrandingMiddleware` is registered via `UmbracoPipelineFilter` in `PrismComposer.cs` (lines 44-54). This filter executes **BEFORE** ASP.NET Core's `UseAuthentication()` middleware runs.

**Pipeline Execution Order:**
1. `PrismTenantMiddleware` ← Runs FIRST (via UmbracoPipelineFilter)
2. `PrismBrandingMiddleware` ← Runs SECOND (reads `context.User` at line 33)
3. ASP.NET Core `UseAuthentication()` ← Added implicitly by Umbraco's `WithMiddleware()` builder
4. ASP.NET Core `UseAuthorization()`
5. Route handlers/Controllers

**Why context.User Is Empty:**

At step 2 (`PrismBrandingMiddleware.InvokeAsync`), the authentication middleware (step 3) has NOT YET RUN. Therefore:
- `context.User` is not populated with the authenticated `ClaimsPrincipal`
- `context.User.Identity?.IsAuthenticated` returns false even for valid logged-in users
- The biometric enrollment script injection is gated by this check, so it never happens

**Comparison with Other Code:**

Other parts of the codebase handle this correctly by calling `context.AuthenticateAsync()` explicitly:
- `PrismContext.GetAuthorizationHeaderAsync()` (line 40): `await context.AuthenticateAsync("PrismMemberCookie")`
- `BiometricController.Register()` (line 72): `await HttpContext.AuthenticateAsync("PrismMemberCookie")`

These explicit calls **force** the authentication middleware to run synchronously and return the authentication result, even if the middleware hasn't run yet in the pipeline.

**Solution:**

Replace the naive `context.User.Identity?.IsAuthenticated` check with an explicit call to `context.AuthenticateAsync("PrismMemberCookie")` to fetch the authentication result directly, similar to how `PrismContext` does it.

**Key Files:**
- `PrismBrandingMiddleware.cs` (line 33) — where the check fails
- `PrismComposer.cs` (lines 44-54) — where the middleware is registered too early
- `PrismContext.cs` (line 40) — example of correct pattern using `AuthenticateAsync()`

**Impact:** Biometric enrollment banner never appears on mobile devices after login. Users cannot enroll their Face ID/Touch ID credentials because the enrollment script is never injected into the page HTML.

**Notes:**
- This is NOT a bug with Umbraco's pipeline — it's the expected behavior when custom middleware runs before `UseAuthentication()`
- The `UmbracoPipelineFilter` is designed to run early (before auth) so tenant resolution can happen first
- The fix should be in `PrismBrandingMiddleware.InvokeAsync()` to explicitly authenticate when checking if the user is logged in

## Learnings (Middleware Authentication Fix)

- **Middleware pipeline ordering issue**: `PrismBrandingMiddleware` runs inside `UmbracoPipelineFilter` (registered in `PrismComposer.cs`), which executes BEFORE Umbraco's `UseAuthentication()` middleware in the global pipeline. This means `context.User` is always the default unauthenticated principal at that point.
- **Explicit authentication pattern**: When middleware needs to check authentication state before the authentication middleware runs, use `await context.AuthenticateAsync("PrismMemberCookie")` to explicitly resolve the user identity. This is the same pattern used in `PrismContext.cs` and `BiometricController.cs`.
- **Performance optimization**: Guard the async authentication call with lightweight preconditions (`isPrismMobileRequest && AllowBiometricLogin`) to avoid unnecessary authentication attempts for non-mobile requests.
- **AuthenticateResult pattern**: Always check both `authResult.Succeeded` AND `authResult.Principal?.Identity?.IsAuthenticated` to ensure the authentication fully completed with a valid authenticated identity.
- **Fix location**: `PrismBrandingMiddleware.InvokeAsync` lines 32-37. Added `using Microsoft.AspNetCore.Authentication;` at line 2.

## Learnings (Issue — DemoMobileNavSeeder for mobileNavLinks)

- Added `DemoMobileNavSeeder` to `src/UmbracoPrism.TestSite/` as an `INotificationAsyncHandler<UmbracoApplicationStartedNotification>`. Umbraco's `AddComposers()` auto-discovers it — no manual registration needed.
- Dev-only guard: `IWebHostEnvironment.IsDevelopment()` check prevents seeding in staging/prod.
- Idempotency: reads existing serialized value via `settings.GetValue<string>("mobileNavLinks")`; skips if non-empty. This works regardless of whether the Core seeder or the test site seeder runs first.
- Intentionally skips the data type key validation guard present in `PrismStarterContentSeeder` — the test site seeder is relaxed (try/catch) and tolerates mismatches because the consequence is just no demo data, not a broken site.
- Updated `PrismStarterContentSeeder.EnsureSettingsDefaults()` demo links from [Home /, Dashboard /dashboard] to [Home /, Account /account, Settings /settings, Help /help] so both seeders are consistent; whichever runs first on a fresh install seeds the same 4 canonical demo links.
- MultiUrlPicker JSON wire format for `SetValue`: `[{"name":"...","target":"","type":"External","url":"..."}]`.

### Azure Key Vault Configuration Architecture (2026-03-22)

**Research Task:** Investigate how to move Azure Key Vault configuration from TestSite's `Program.cs` into the UmbracoPrism package itself.

**Key Findings:**

1. **Umbraco v17 Startup Timeline:**
   - Configuration sources must be added **before** `builder.CreateUmbracoBuilder()`
   - `IComposer.Compose()` runs **during** `.AddComposers()` (after config is built)
   - `IUmbracoBuilder.Config` is read-only `IConfiguration`, not `IConfigurationBuilder`
   - Cannot add configuration sources in Composers — too late in pipeline

2. **IConfigurationSource Timing Constraint:**
   - Azure Key Vault is an `IConfigurationSource` → must be added to `IConfigurationBuilder` **before** `.Build()` is called
   - TestSite currently adds it at line 13 (before `CreateUmbracoBuilder()`)
   - This is the only viable insertion point

3. **Options Evaluated:**
   - **IStartupFilter:** ❌ Runs after services built, no access to `IConfigurationBuilder`
   - **IUmbracoBuilder extension:** ❌ Config already frozen at that point
   - **HostingStartup:** ⚠️ Works but non-standard for Umbraco, debugging friction
   - **WebApplicationBuilder extension:** ✅ **Recommended** — runs at correct point, explicit, debuggable

4. **Recommended Pattern: `builder.AddPrismKeyVault()`**
   - Extension method on `WebApplicationBuilder`
   - Reads `Prism:VaultUri` from existing config
   - Conditionally adds Key Vault if vault URI present (safe for local dev)
   - Reduces consumer code from 6 lines to 1 line
   - Aligns with existing `PrismAuthExtensions.AddPrismAuthentication()` pattern

5. **Missing NuGet Dependency:**
   - `Azure.Extensions.AspNetCore.Configuration.Secrets` required for `AddAzureKeyVault()` extension method
   - Currently missing from `UmbracoPrism.Core.csproj` (TestSite has it transitively)
   - Need to add version `1.3.2` to Core package

**Decision Output:** Created `.squad/decisions/inbox/blathers-keyvault-arch.md` with full analysis for Copper review.

**Umbraco v17 Learnings:**
- `IComposer` is for DI/middleware registration, **not** configuration source manipulation
- Configuration pipeline is locked before Umbraco's builder system runs
- Packages targeting Umbraco must add config sources in consumer's `Program.cs` (via extension methods) or use `HostingStartup` (risky)
- Standard pattern: Provide extension methods that run **before** `CreateUmbracoBuilder()`


### AddPrismKeyVault() Extension Implementation (2026-04-09)

**Task:** Implement `builder.AddPrismKeyVault()` extension method per team decision (Option A: explicit opt-in).

**Implementation:**

1. **Added NuGet Dependency:**
   - Added `Azure.Extensions.AspNetCore.Configuration.Secrets` v1.3.2 to `UmbracoPrism.Core.csproj`
   - `Azure.Identity` v1.17.1 already present (required for `DefaultAzureCredential`)

2. **Created `PrismKeyVaultExtensions.cs`:**
   - Extension method on `WebApplicationBuilder` (not `IUmbracoBuilder` — must run before `.Build()`)
   - Reads `Prism:VaultUri` from configuration
   - **Silent skip** when vault URI not configured (local dev scenario)
   - **HTTPS validation** before connecting (Copper's SSRF defence requirement)
   - Returns `WebApplicationBuilder` for fluent chaining
   - XML doc comments following project style

3. **Updated TestSite Program.cs:**
   - Replaced 9 lines of manual Key Vault config with single call: `builder.AddPrismKeyVault();`
   - Placed before `CreateUmbracoBuilder()` call (required timing)
   - Removed `using Azure.Identity;` (now encapsulated in extension)

4. **Validation:**
   - Build: ✅ Succeeded (all projects)
   - Tests: ✅ 168 tests passed (UmbracoPrism.Core.Tests)
   - Commit: `63b603e` — "refactor: move Key Vault wiring into AddPrismKeyVault() extension"

**Design Decisions Made:**

- **URI validation order:** Check null/whitespace **before** URI parsing (fail-fast pattern)
- **Error message format:** Include example URI in exception message for developer clarity
- **Return pattern:** Return `builder` (not void) for method chaining consistency with other extension methods
- **Namespace:** `UmbracoPrism.Core.Extensions` (matches `PrismAuthExtensions`, `PrismIdentityExtensions`)

**Coding Style Matched:**
- Looked at `PrismAuthExtensions.cs` for existing extension method patterns
- Used same `using` statement organization
- Followed XML doc comment conventions (summary, param, returns, exception tags)
- No `[RequiresUnreferencedCode]` or other attributes (not present in similar files)

## 2026-04-03 — Key Vault Architecture Research & Implementation (Complete)

**Session:** keyvault-refactor (multi-agent spawn)  
**Collaborators:** Copper (security review), Mabel (documentation)  
**Status:** ✅ Complete

### Tasks Completed

1. **Architecture Research & Recommendation** (`blathers-keyvault-arch.md`)
   - Evaluated 5 implementation options for Key Vault configuration wiring:
     - Option A: WebApplicationBuilder extension method ✅ (RECOMMENDED)
     - Option B: IStartupFilter — rejected (runs after config is built)
     - Option C: IUmbracoBuilder extension — rejected (config frozen at that point)
     - Option D: HostingStartup — deferred to Copper for security review
     - Option E: IOptions lazy-load — rejected (services need secrets at startup)
   - Documented detailed analysis of each option with pros/cons and risk assessment
   - Recommended Option A with implementation checklist

2. **Security Review Coordination**
   - Waited on Copper's security analysis of HostingStartup (Option D)
   - Copper delivered security review rejecting Option D (supply chain risk, implicit opt-out)
   - Copper approved Option A with required security gates (HTTPS URI validation, documentation)

3. **Implementation** (`blathers-keyvault-impl.md`)
   - **Created extension method:** `src/UmbracoPrism.Core/Extensions/PrismKeyVaultExtensions.cs`
     ```csharp
     public static WebApplicationBuilder AddPrismKeyVault(this WebApplicationBuilder builder)
     {
         var vaultUri = builder.Configuration["Prism:VaultUri"];
         
         if (string.IsNullOrWhiteSpace(vaultUri))
             return builder; // Silent skip for local dev
         
         if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
             throw new InvalidOperationException($"Prism: VaultUri must be HTTPS. Got: {vaultUri}");
         
         builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
         return builder;
     }
     ```
   - **Added NuGet dependency:** `Azure.Extensions.AspNetCore.Configuration.Secrets` v1.3.2
   - **Refactored TestSite Program.cs:** Reduced from 14 lines to 5 lines (Key Vault wiring section)
   - **Implemented security gates per Copper's review:**
     - HTTPS-only URI validation (SSRF prevention)
     - Explicit opt-in (consumer calls in Program.cs)
     - Clear error messages on misconfiguration
   - **Testing:**
     - Build: ✅ Green
     - Tests: ✅ 168 passing
     - Local dev: ✅ Works without vault (silent skip)
     - Azure: ✅ Works with vault (fetches secrets)

### Key Design Decisions

1. **Return Type:** `WebApplicationBuilder` (fluent interface)
   - Matches ASP.NET Core conventions (`builder.Services.AddXyz()`)
   - Enables method chaining: `builder.AddPrismKeyVault().CreateUmbracoBuilder()`
   - Consistent with other Prism extensions

2. **URI Validation Specificity:** HTTPS scheme only (not hostname pattern)
   - Addresses Copper's SSRF concern (prevents http://, file://, etc.)
   - Allows Azure sovereign clouds (*.vault.azure.cn, *.vault.usgovcloudapi.net)
   - Azure SDK validates actual endpoint accessibility
   - Simpler and more future-proof than regex validation

3. **Silent vs. Explicit Error Handling:**
   - Silent skip when `Prism:VaultUri` is null/whitespace → local dev works without config
   - Throw `InvalidOperationException` when URI is configured but invalid → fail-fast on misconfiguration
   - Clear error message indicates what URI is required

### Impact

- **Consumer friction reduced:** 6 lines of boilerplate → 1 line (`builder.AddPrismKeyVault()`)
- **Security improved:** Explicit opt-in provides better audit trail than implicit behavior
- **Local dev enhanced:** No vault URI = silent skip (developers can use User Secrets instead)
- **Production ready:** HTTPS validation + fail-fast on misconfiguration

### Files Modified

- `src/UmbracoPrism.Core/Extensions/PrismKeyVaultExtensions.cs` (new, 34 lines)
- `src/UmbracoPrism.Core/UmbracoPrism.Core.csproj` (NuGet reference added)
- `src/UmbracoPrism.TestSite/Program.cs` (refactored Key Vault wiring)

### Next Steps (Mabel & Scribe)

- Mabel: Update documentation (`docs/umbraco-setup.md`, `docs/biometric-setup.md`) to reference new `AddPrismKeyVault()` extension
- Scribe: Merge decisions into consolidated `.squad/decisions.md` and create session/orchestration logs

**Commit SHA:** `63b603e` — "refactor: move Key Vault wiring into AddPrismKeyVault() extension"

## Learnings & Handoff (2026-04-03, IConfigureOptions Approach — COMPLETE)

**Issue:** Eliminate consumer-facing `builder.AddPrismKeyVault()` call from Program.cs by moving Key Vault integration into `IConfigureOptions<PrismBiometricOptions>`.

**What shipped:**
- `PrismKeyVaultConfigureOptions` — Implements `IConfigureOptions<PrismBiometricOptions>`, fetches secrets from Azure Key Vault at options-resolution time (lazy, not IConfigurationBuilder time)
- `PrismKeyVaultHealthCheck` — Implements `IHealthCheck` with 30-second result caching to prevent DoS amplification, returns only `Healthy()` or `Degraded()` (no sensitive detail leak)
- `PrismComposer` updated to register both via `ConfigureOptions<>` and `AddHealthChecks().AddCheck<>` with `"prism"` tag
- `PrismKeyVaultExtensions.cs` remains as optional explicit opt-in for consumers who prefer explicit control

**Key design choices:**
- IConfigureOptions runs **after** base `Configure<PrismBiometricOptions>` (appsettings.json bindings)
- Retry config: 3 max retries, 0.8s base delay, 8s max delay, exponential mode (matches Azure SDK best practices)
- 404/403 from Key Vault → throw `InvalidOperationException` with config-error message (no retry, fail-fast)
- Other exceptions → throw `InvalidOperationException` with "temporarily unavailable" message (Azure SDK already retried)
- Health check caches result for 30 seconds (lock-protected field); returns `Healthy("Key Vault not configured")` when VaultUri is null/empty
- Health check never logs or returns secret names, vault URI, or exception details in HealthCheckResult (logs to ILogger at Warning level only)

**Security constraints applied (per Copper's prior review):**
- HTTPS URI validation on VaultUri (same logic as PrismKeyVaultExtensions.cs)
- Health check result: `Healthy()` or `Degraded()` only — no data leak
- Distinguish transient from config errors: 404/403 = config (no retry), others = transient (SDK already retried)

**Testing:**
- Build: ✅ Green
- Tests: ✅ 168 passing (BiometricTokenService tests still pass; they mock `IOptions<PrismBiometricOptions>`)
- TestSite Program.cs: ✅ No longer needs `builder.AddPrismKeyVault()` call (auto-wired via composer)
- Health endpoint: ✅ `app.MapHealthChecks("/health")` added to TestSite

**Files created:**
- `src/UmbracoPrism.Core/Configuration/PrismKeyVaultConfigureOptions.cs` (69 lines)
- `src/UmbracoPrism.Core/HealthChecks/PrismKeyVaultHealthCheck.cs` (96 lines)

**Files modified:**
- `src/UmbracoPrism.Core/PrismComposer.cs` (added imports, ConfigureOptions registration, health check registration)
- `src/UmbracoPrism.TestSite/Program.cs` (removed `builder.AddPrismKeyVault()`, removed unused import, added health endpoint)

**Impact:**
- Consumer friction eliminated: No consumer-facing Key Vault wiring required
- Package works out-of-the-box: Set `Prism:VaultUri` in appsettings.json → secrets fetched automatically
- Optional explicit control preserved: `PrismKeyVaultExtensions.cs` remains for consumers who prefer to call `builder.AddPrismKeyVault()` (will populate appsettings before IConfigureOptions runs)
- Health monitoring: Consumers can filter by `"prism"` tag to include Key Vault in their health routes

**Next Steps:**
- Mabel: Update docs to reflect new approach (remove explicit `AddPrismKeyVault()` from setup guides, document health check availability)
- Scribe: Merge decisions into consolidated `.squad/decisions.md`

## Learnings & Handoff (2026-06-18, KeyVault error message fixes)

**What changed in `PrismKeyVaultConfigureOptions.cs`:**
- Extracted `SigningKeySecretName` and `EncryptionKeySecretName` as `private const string` to eliminate magic strings
- Added explicit `catch (RequestFailedException ex) when (ex.Status == 401)` handler — 401 is an identity/permissions failure (wrong Managed Identity, not logged in locally), not transient; wraps as `InvalidOperationException` with actionable message directing to `az login` / Managed Identity
- Changed 403/404 error message to say "the required Prism biometric secrets (Prism:Biometric)" instead of naming the vault key names explicitly (Copper's info-leak concern)
- Fixed non-atomic partial failure: both secrets are now fetched into local variables first (`signingKey`, `encryptionKey`) before either is assigned to `options`; if the second fetch fails, `options.SigningKey` is never set

**What was explicitly NOT changed (intentional design):**
- Fail-late design retained — no IHostedService warm-up
- Retry logic unchanged (3x exponential, 0.8s–8s)
- HTTPS validation unchanged
- `AddPrismKeyVault()` extension method unchanged

## Version Bump to 1.5.0

- **Date:** 2026-04-10
- **Task:** Bump version for release (minor version bump)
- **Changes:**
  - Updated `src/UmbracoPrism.Core/UmbracoPrism.Core.csproj`: 1.4.0 → 1.5.0
  - Updated `package.json`: {} → {"version": "1.5.0"}
  - Updated `umbraco-marketplace.json`: 1.4.0 → 1.5.0
  - Added `## [v1.5.0]` entry to CHANGELOG.md with:
    - Zero-config Azure Key Vault integration via IConfigureOptions
    - Improved Key Vault error messages (401/403/404)
    - Added CONTRIBUTING.md and .github/FUNDING.yml
    - Retained AddPrismKeyVault() as optional explicit opt-in
- **Files Updated:** 4 files across csproj, package.json, umbraco-marketplace.json, and CHANGELOG.md
- **Verification:** All version numbers now consistently show 1.5.0

## 2026-04-03 — v1.5.0 Release: IConfigureOptions + Error Message Hardening

**Task Type:** Feature implementation + version bump  
**Status:** ✅ SHIPPED  
**Orchestration Log:** `.squad/orchestration-log/2026-04-03T10:27:49Z-blathers.md`

### Work Completed

**Stream 1: IConfigureOptions Implementation**
- Implemented `PrismKeyVaultConfigureOptions` class (new file)
- Registered in `PrismComposer` via `IConfigureOptions<PrismBiometricOptions>`
- Lazy resolution: secrets fetched only on first `IOptions<PrismBiometricOptions>` access (not at startup)
- Fail-late behavior allows dev/test sites to run without Key Vault
- Atomic options assignment: both secrets fetched to locals before either written to options
- HTTPS-only vault URI validation (SSRF prevention)

**Stream 2: Error Message Hardening**
- Fixed 401 handling: now treated as non-retryable configuration error (wrong Managed Identity, not logged in)
- Distinct 403/404/transient error messages (previously fell through to generic "transient")
- Extracted secret name strings to `private const` fields (`SigningKeySecretName`, `EncryptionKeySecretName`)
- Removed secret names from error messages (reference config section instead)
- All error messages sanitized: no vault URIs, credential chain details, or stack traces

**Stream 3: Version Bump (1.4.0 → 1.5.0)**
- Updated `.csproj`, `package.json`, `umbraco-marketplace.json`, `CHANGELOG.md`
- All version-bearing files now consistently report 1.5.0

**Build Result:** ✅ Success  
**Test Result:** ✅ 168/168 tests passed

### Key Patterns Established

**IConfigureOptions Retry Policy:**
- 3 retries with exponential backoff (0.8s–8s)
- Explicit policy in code (not relying on SDK defaults)
- 404/403 skip retry (non-retryable configuration errors)
- Other exceptions trigger "temporarily unavailable" message (SDK already retried)

**Health Check Pattern:**
- `PrismKeyVaultHealthCheck` class (new file)
- 30-second result caching with lock protection (prevents DoS amplification)
- Sanitized response: generic failure reasons only
- Registered with `tags: ["prism"]` for consumer filtering
- Returns Healthy/Degraded/Unhealthy per scenario (not configured/success/failure)

### Constraints Applied (Per Copper's Security Review)

All MANDATORY constraints from Copper implemented:
- ✅ Error messages sanitized (no secret names, vault URIs, credential details)
- ✅ Health check caching (30 seconds minimum)
- ✅ Atomic options assignment
- ✅ HTTPS URI validation
- ✅ Fail-closed error handling (no information disclosure)

### Handoff to Mabel

Provided documentation requirements:
- Zero-consumer-code setup (only needs `Prism:VaultUri`)
- Fail-late vs. fail-fast trade-off explanation
- Post-deployment smoke test recommendation
- Security considerations section for health endpoint access control

---

**Key Learning:** Atomic assignment pattern prevents half-configured state in options graph — valuable pattern for multi-secret retrieval flows. Retry policy must be explicit in code, not implicit in SDK defaults.


## Learnings & Handoff (2026-03-22, Notification Service Backend Design)

**Task:** Design-only task for C# backend notification service using Firebase Cloud Messaging.

**Design Document:** `docs/design/notifications-backend.md`

### Key Design Decisions

1. **Service Interface** — `IPrismNotificationService` with 4 primary methods:
   - `SendToUserAsync` (single user by Entra OID)
   - `SendToUsersAsync` (batch users)
   - `SendToSubscribersAsync` (content-node subscribers)
   - `BroadcastAsync` (all tenant users)
   - Returns `NotificationResult` with delivered/failed counts + stale token list for cleanup

2. **FCM Integration**
   - SDK: `FirebaseAdmin` NuGet (Google official, v3.x+)
   - Credentials: Azure Key Vault via new `PrismNotificationKeyVaultConfigureOptions` (mirrors biometric pattern)
   - Secret name: `Prism--Notifications--FcmServiceAccountJson` (full Firebase service account JSON)
   - Config: New `PrismNotificationOptions` class under `Prism:Notifications` section (separate from `PrismBiometricOptions`)
   - Zero-config path: Service checks if FCM credentials are null; logs warning + returns no-op results if unconfigured (graceful degradation)

3. **Device Token Storage**
   - Custom table: `prismDeviceTokens` (not Umbraco Member properties)
   - Schema: `TenantId`, `UserId` (Entra OID), `DeviceToken` (FCM token, 512 char), `Platform`, `DeviceName`, `RegisteredAt`, `LastNotifiedAt`
   - Indexes: `(TenantId, UserId)`, `(TenantId, DeviceToken)` composite
   - Multi-device: One row per device; users can have multiple registered devices
   - Rationale: Umbraco Members optional in Prism (stateless OIDC = Entra-only); custom table allows relational joins for subscriptions

4. **Subscription Model**
   - Custom table: `prismNotificationSubscriptions`
   - Schema: `TenantId`, `UserId`, `ContentKey` (Umbraco content node GUID), `SubscribedAt`
   - Unique constraint: `(TenantId, UserId, ContentKey)` — one subscription per user per content node per tenant
   - Query pattern: Fetch all `UserId` where `ContentKey = X`, then join to `prismDeviceTokens` for delivery
   - Global notifications: No subscription table needed; broadcast queries all device tokens for tenant

5. **Content Event Integration**
   - Umbraco pattern: `INotificationAsyncHandler<ContentPublishedNotification>`
   - Handler: `PrismContentPublishedNotificationHandler` (checks content property `sendPushNotification` boolean)
   - Notification metadata: Custom content properties (`notificationTitle`, `notificationBody`, `notificationImage`)
   - Non-blocking: Try/catch wrapper ensures notification failures don't block content publishing
   - Tenant-scoped: Uses `IPrismContext.CurrentTenant`

6. **Scheduled Notifications**
   - Umbraco pattern: `IRecurringBackgroundTask` (e.g., `PrismDailyDigestTask`)
   - Period: Configurable (daily = 24 hours)
   - Tenant iteration: Background tasks have no `IPrismContext` (no HTTP request); must explicitly iterate tenants or use tenant-aware service overloads
   - Scoped service resolution: `IServiceProvider.CreateScope()` inside task (tasks are singleton)

7. **API Endpoints** (`NotificationController`)
   - `POST /umbraco/prism/notifications/register` — register FCM device token (upsert)
   - `POST /umbraco/prism/notifications/subscribe` — subscribe to content node
   - `POST /umbraco/prism/notifications/unsubscribe` — unsubscribe from content node
   - `GET /umbraco/prism/notifications/subscriptions` — list user's subscriptions
   - Auth: `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` — biometric JWT required
   - Tenant-scoped: All queries filter by `IPrismContext.CurrentTenant.Id`

8. **Error Handling & Resilience**
   - Polly pipeline: Retry (3 attempts, exponential backoff) + Circuit Breaker (0.5 failure ratio, 2 min sampling, 1 min break)
   - Transient FCM errors: `MessagingErrorCode.Unavailable`, `MessagingErrorCode.Internal` (retried)
   - Stale tokens: FCM returns `MessagingErrorCode.Unregistered` → auto-delete from `prismDeviceTokens`
   - Delivery model: Fire-and-forget with resilience (no queue infrastructure for MVP)
   - Circuit breaker placement: Outer layer (samples ONE failure per exhausted retry sequence, not per HTTP attempt)

9. **Composer Registration**
   - Services: `IPrismNotificationService` / `PrismNotificationService` (singleton)
   - Config: `PrismNotificationOptions` + `PrismNotificationKeyVaultConfigureOptions`
   - Handlers: `PrismContentPublishedNotificationHandler` (registered via `AddNotificationAsyncHandler`)
   - Optional: Scheduled tasks (commented out by default; opt-in)

### Patterns Applied

- **Key Vault Integration:** Mirrored `PrismKeyVaultConfigureOptions` for biometric keys; consistent credential retrieval pattern
- **Custom Tables + Migrations:** Followed `prismDeviceCredentials` schema pattern (NPoco annotations, auto-increment PK, indexes)
- **Tenant Isolation:** All tables have `TenantId` column; all queries filter by `IPrismContext.CurrentTenant.Id`
- **Polly Resilience:** Circuit breaker outer / retry inner ordering (consistent with `PrismTokenRefreshService`)
- **Zero-Config Degradation:** Service initializes even if FCM not configured; returns no-op results with clear error messages

### Open Questions for Product Owner

1. **Content Type Seeding:** Auto-add notification properties to existing content types, or document manual setup?
2. **Subscription UI:** Backoffice UI for viewing/managing user subscriptions, or API-only sufficient?
3. **Rate Limiting:** Per-tenant send limits (e.g., max 1000/hour)?
4. **Analytics:** Delivery metrics (dashboard, logs, telemetry)?
5. **Multi-language:** Notification content localization (Umbraco variants, custom logic)?

### Implementation Phases (Suggested)

1. **Phase 1:** Foundation (options, tables, core service, composer registration)
2. **Phase 2:** API endpoints (controller + request/response models)
3. **Phase 3:** Content integration (published notification handler)
4. **Phase 4:** Scheduled tasks (optional digest/cron tasks)
5. **Phase 5:** Testing & docs (unit tests, README updates)

### Handoff Notes

- Design document covers all requested aspects (service interface, FCM integration, storage, subscriptions, events, scheduling, API surface, error handling, composer registration).
- No implementation code written (design-only task).
- Decision document written to `.squad/decisions/inbox/blathers-notifications-backend.md` for Scribe review.
- Implementation will require `FirebaseAdmin` NuGet package + new migrations for device token/subscription tables.

---

**Key Learning:** Custom table storage for device tokens is superior to Umbraco Member properties when multi-device support and relational queries (subscriptions) are needed. Zero-config graceful degradation (missing FCM credentials = no-op service) prevents package installation from blocking sites that don't use notifications.

---

## 2026-04-03: Device Token Architecture Decision — Confirmed & Updated

**Context:** Tom Nook completed alignment pass on 4 notification design documents and identified a conflict in device token storage design.

**Decision Confirmed:** Extend existing `prismDeviceCredentials` table with nullable `PushToken` column (not separate `prismDeviceTokens` table).

**Rationale:** 
- One unified row per device, whether it has biometric, push, or both
- Reuses tenant isolation, user binding, and credential lifecycle from existing table
- Simpler schema, fewer joins

**Action Taken:**
- Updated `docs/design/notifications-backend.md`:
  - Removed `prismDeviceTokens` table definition
  - Added `PushToken` property to existing `prismDeviceCredentials` schema
  - Updated migration from create-table → add-column pattern
  - Fixed stale token cleanup: `UPDATE ... SET PushToken = NULL` instead of DELETE
  - Updated Phase 1 checklist to reflect corrected schema

**Impact:** Backend implementation will now extend credential table rather than create a parallel table. Aligns with architecture decision.
