# Tom Nook — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Isabelle: Web Components, Storybook, Playwright UI tests
- Blathers: C# backend, services architecture, databases, auth
- Tangy: Testing methodology, edge cases, test coverage
- Scribe: Session logging, decisions, team memory

## Architecture Established

- **Middleware:** PrismTenantMiddleware (tenant resolution) → PrismBrandingMiddleware (CSS injection)
- **Identity:** Stateless OIDC via PrismOidcConfiguration (swapped per request)
- **Services:** TenantService, BrandingService, MobileBundleService, SecretVaultService
- **Database:** PrismMigrationPlan handles schema evolution; no local Members (stateless auth)
- **Mobile:** Capacitor bundle generation from Backoffice settings; safe-area support for notched devices

## Key Patterns

1. **Naming:** `IPrismXxx` (interfaces), `XxxService` (services), `PrismXxxMiddleware` (middleware), `PrismXxx` (models)
2. **Drop-box pattern:** Agents write decisions to `.squad/decisions/inbox/{name}-{slug}.md` → Scribe merges to canonical
3. **Parallelism:** Spawn all independent agents as background mode in one turn; no serialization on shared files
4. **Eager downstream:** Anticipate testing, docs, scaffolding work; spawn while implementation runs

## Learnings

### Stateless OIDC Architecture (2026-03-22)
**What works:** Per-request tenant resolution + dynamic OIDC config is elegant:
1. PrismTenantMiddleware resolves hostname → fetches PrismTenant from cache (30 min TTL)
2. IPrismContext (scoped per request) holds the tenant
3. PrismOidcConfiguration.PostConfigure hooks token validation to use tenant's EntraTenantId/ClientId dynamically
4. No per-tenant authentication state; every request is fully self-contained.

**Design trade-off:** Burst of Azure calls on cache miss (30-min window hits DB + may trigger OIDC metadata fetch).
**Risk:** Token refresh in PrismContext uses blocking `.GetAwaiter().GetResult()`; no circuit breaker if CIAM endpoint is degraded.

### Multi-tenancy via Shared Schema (2026-03-22)
**Database model:** Single `prismTenants` table with tenant metadata + JSON blobs (BrandingOverrides, MobileBrandingOverrides, MobileAppConfig).
**Pros:** No schema sprawl; easy to add tenants dynamically; secrets stored in Azure Key Vault by name reference.
**Cons:** Scales to ~1K tenants without issue, but no advanced partitioning for 10K+ (would need read replicas).
**Cache strategy:** Runtime cache (30 min tenant, 1 hour secrets, 10 min branding tabs); no pre-warming or lease renewal.

### Branding Injection Pattern (2026-03-22)
**Flow:** PrismBrandingMiddleware buffers HTML response → injects CSS overrides + mobile shell guards into `<head>`.
**Smart details:**
- Scans CSS files in app root on boot; parses CSS variables (regex-based)
- Merges tenant overrides with detected defaults
- Supports both web (`--var`) and mobile (`prism-mobile` media) variants
- Graceful degradation: no overrides = no injection; silently skips non-HTML responses

**Concern:** CSS file scan happens on first BrandingService call; slow on monolith apps. Needs lazy/explicit registration.

### Mobile Bundle Generation (2026-03-22)
**What it does:** MobileBundleService generates ZIP with:
- Capacitor config (bundled with tenant ENVvars)
- Package.json + bootstrap scripts
- Safe-area CSS for notched devices
- Placeholder index.html with error UI

**Design quality:** Excellent separation of concerns; generates valid JS configs; validates app ID format.
**Risk:** Accepts arbitrary URLs (StartUrl, IconUrl, SplashUrl) without SSRF guards; no bundle size limits; no rate limiting on endpoint.

### Test Coverage Commentary (2026-03-22)
**Good:** Unit tests for middleware, context, services; Playwright ITS for UI components; FluentAssertions for readability.
**Gaps:**
- No full OAuth flow test (redirect → token exchange → cookie set)
- No token refresh failure scenarios
- No mobile bundle edge cases (special chars in app name, concurrent generation)
- No OIDC key rotation test (forces 401; should retry fresh metadata)

### Authorization Inconsistency (2026-03-22)
**Current model:**
- User isolation: `PrismTenantHandler` checks `user.EntraTenantId == currentTenant.EntraTenantId`
- Admin gate: `PrismAdminHandler` checks Umbraco *local* group membership (not Entra groups)

**Issue:** Admin users may be synced from Entra, but policy checks local Umbraco groups. Potential for permission drift.
**Recommendation:** Standardize on Entra groups for consistency.
