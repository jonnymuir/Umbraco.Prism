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
