# Celeste — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**User:** Jonny Muir

## Team Context

- Tom Nook: Architecture, scope, code review
- Isabelle: Web Components, Storybook, UI logic
- Blathers: Backend services, middleware, authentication
- Tangy: Test strategy and reliability coverage
- Copper: Security engineering (CIA, tenant isolation)
- Scribe: Session logging and decisions

## Learnings

- User requested stronger XML-style documentation discipline across code.
- Documentation must support multi-tenant and security-critical reasoning, not generic summaries.
- Security-sensitive flows should document tenant boundaries, trust assumptions, and failure behavior explicitly.
- Practical baseline works best when focused on public/protected API surface in Auth, Services, Middleware, and boundary Models before private internals.
- Parameter and return tags should be explicit for request/tenant/security context values to improve IntelliSense safety during integration work.
- Docs-only passes should stay behavior-neutral and be validated with full build plus Core tests to keep risk minimal.
