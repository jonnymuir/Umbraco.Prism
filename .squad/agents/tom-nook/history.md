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

_(none yet)
