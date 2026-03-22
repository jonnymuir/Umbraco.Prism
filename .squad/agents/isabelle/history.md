# Isabelle — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Tom Nook: Architecture, scope, code review, leadership
- Blathers: C# backend, services, databases, auth
- Tangy: Testing methodology, edge cases, test coverage
- Scribe: Session logging, decisions, team memory

## Frontend Landscape

**Web Components:**
- `prism-create-tenant-modal` — Modal for creating new tenants (stories + Playwright tests)
- `prism-dashboard` — Dashboard component (stories defined)
- Located: `/src/UmbracoPrism.Client/src/`

**Build & Test:**
- Vite for bundling → static assets to `App_Plugins/UmbracoPrism/`
- Storybook for component-driven development
- Playwright for E2E tests
- No linting configured; Storybook's axe handles WCAG compliance

**Mobile Detection:**
- Query flag: `?prismMobile=1`
- User-agent marker: `PrismMobile`
- Cookie-based fallback
- CSS class `prism-mobile` for safe-area styling on notched devices

## Learnings

_(none yet)
