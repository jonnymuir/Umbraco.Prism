# Changelog

All notable changes to Umbraco Prism are documented here. This project follows [semantic versioning](https://semver.org/).

## [v1.2.2] — 2026-03-28

### Bug Fixes & Improvements

- Fixes intermittent MockBackOffice build failures by disabling parallel package generation (GeneratePackageOnBuild).
- Prevents SPA router from intercepting blob URL downloads by stopping propagation on mobile app button clicks.
- Updates mobile app config start URL automatically when Cloudflare tunnel hostname changes.

## [v1.2.1] — 2026-03-29

### Bug Fixes & Improvements

- Fixes the GitHub release pipeline to only create releases when triggered by a version tag, preventing errors on manual workflow runs.

## [v1.2.0] — 2026-03-29

### New Features

- **Squad project management:** Introduced Squad — an AI team framework for collaborative development with defined roles, decision tracking, and skill-based routing.
- **Multi-tenant cache metrics & diagnostics:** Added comprehensive cache tracking to tenant service with hit/miss counts, invalidation tracking, and database load monitoring. Enhanced debug output to display real-time tenant cache state.
- **Cloudflared local development automation:** Added `start-trycloudflare` helper script for zero-config tunnel setup during development. Automatically manages Entra redirect URIs and provides public HTTPS URLs for mobile testing without manual certificate management.
- **Mobile app generation & scaffolding:** Implemented full mobile app bundle generation using Capacitor with iOS and Android support. Includes mobile-specific branding overrides, authentication flows, and emulator integration.
- **Prism branding middleware & tenant overrides:** Added `PrismBrandingMiddleware` to inject tenant-specific CSS variables and imagery into HTML responses. Tenants can now customize logos, colours, and layouts without code changes.
- **Authorization planes & tenant isolation:** Implemented role-based authorization with distinct admin and end-user planes. Tenants are cryptographically isolated at the token level; admin actions are restricted to tenant-specific contexts.
- **Storybook integration & accessibility testing:** Added Storybook for Web Components with accessibility testing via Axe. Tests run automatically in CI and locally via `npm run test-storybook`.
- **OpenID Connect tenant-specific configuration:** Enhanced OIDC configuration to support per-tenant token endpoints, signing key validation, and credential refresh. Added resilience patterns for token endpoint failures.
- **Tenant management UI:** Implemented full CRUD operations for tenants in the Umbraco backoffice. Added modal workflows for creating, editing, and deleting tenants with real-time validation.
- **Prism context injection & multi-tenant routing:** Implemented `IPrismContext` for request-scoped tenant resolution. Added middleware to resolve tenant identity from domain, query parameter, or header and inject it into the DI container.
- **Playwright end-to-end tests:** Added Playwright test suite for Web Components with browser automation support (Chromium, Firefox, WebKit).
- **Package metadata & marketplace listing:** Added NuGet marketplace metadata with proper licensing, icons, and descriptions. Project is now discoverable on NuGet.org and Umbraco Marketplace.

### Bug Fixes & Improvements

- Updated Umbraco.Cms to version 17.2.2 with latest stability improvements.
- Improved tunnel temp log handling with fallback strategies for systems with restricted `/tmp` access.
- Fixed OIDC callback path alignment (`/signin-oidc`) across tunnel configuration and redirect URI registration.
- Automated stale redirect URI cleanup for trycloudflare tunnels to prevent Entra pollution during repeated dev sessions.
- Enhanced token refresh resilience with retry pipelines and concurrent error handling.
- Improved accessibility checks in Storybook test runner with better state management and retries.
- Added `.nvmrc` file to ensure consistent Node.js version (22.17.1) across development environments.
- Added WebRouting configuration for proper tenant-aware URL handling.
- Improved modal styling for better responsiveness and usability.
- Enhanced error logging and diagnostics throughout auth and tenant flows.
- Refined CSS variables and branding system for greater customization flexibility.
- Updated package icon to vertical lockup version for consistency across NuGet and marketplace listings.

### Documentation

- Comprehensive README restructure with clear prerequisites, getting started guide, and developer onboarding flow.
- Added local authentication walkthrough with step-by-step Entra setup for development environments.
- Documented mobile workflow with Capacitor commands and emulator setup guides.
- Added Prism Dashboard access instructions and first-tenant creation guide.
- Clarified architecture overview with multi-tenant identity flow diagrams.
- Added troubleshooting and FAQ sections for common setup issues.
- Improved marketplace description to accurately reflect multi-tenancy features and enterprise capabilities.
