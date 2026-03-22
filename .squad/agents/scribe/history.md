# Project Context

- **Project:** Umbraco.Prism
- **Created:** 2026-03-22

## Core Context

Agent Scribe initialized and ready for work.

## Recent Updates

📌 Team initialized on 2026-03-22
📝 2026-03-22: Copilot instructions file completed and verified. Orchestration logs written. Decisions consolidated. Ready for deployment.

## Learnings

- **Multi-project structure:** Core is a NuGet package; Client is web components (Lit/Storybook); TestSite and MockBackOffice are reference implementations.
- **Testing split:** .NET Core uses XUnit (with Moq, FluentAssertions); Client uses Playwright with Storybook test runner and axe for WCAG compliance.
- **Mobile feature complexity:** MobileBundleService (27KB) generates full Capacitor starter bundles with tenant-specific config; requires Node.js 22.17.1 and npm.
- **Middleware-first architecture:** PrismTenantMiddleware resolves tenant from hostname; PrismBrandingMiddleware injects CSS variable overrides dynamically.
- **Stateless identity:** No local Members; all auth deferred to Entra ID (CIAM) with secrets stored in Azure Key Vault per tenant.
- **Admin policy enforcement:** Tenant management restricted to Umbraco users in configured groups (default: admin); enforced via PrismAdminHandler.
- **CI/CD:** Separate workflows for testing (ci-tests.yml) and packaging (package-release.yml); client build must complete before Core pack.
