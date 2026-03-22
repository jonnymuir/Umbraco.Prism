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

## Learnings

_(none yet)
