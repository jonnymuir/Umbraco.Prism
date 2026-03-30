# Changelog

All notable changes to Umbraco Prism are documented here. This project follows [semantic versioning](https://semver.org/).

## [v1.3.0] — 2026-04-02

### New Features

- **Biometric authentication system:** Complete multiplatform biometric registration and login flow with per-tenant opt-in toggle. Includes Capacitor bridge for fingerprint/face recognition, secure credential storage, and Web Components (prism-biometric-register, prism-biometric-settings).
- **Biometric enrollment change detection:** Automatically wipes credentials when device fingerprint changes, ensuring security across OS updates and enrollment modifications.
- **Biometric rate limiting & audit logging:** Per-token and per-IP rate limiting on the `/exchange` endpoint with structured audit logging for compliance and security monitoring.
- **Biometric multi-tenant validation:** Defence-in-depth boundary validation ensuring users cannot exchange credentials across tenant boundaries.
- **Mobile navigation (Umbraco Settings node):** Configurable bottom navigation bar for mobile apps with support for up to 4 links, managed as a site-wide Umbraco Settings node without code changes.
- **Downstream API demo (fetch-based):** Replaced mock downstream API demo with fetch-based inline result panel for cleaner development experience.
- **Biometric admin controls:** Endpoints for users to unenrol biometric devices and admins to revoke user devices with proper DeviceId scoping.

### Bug Fixes & Improvements

- Resolves OIDC signing key cold-start 401 (IDX10500) errors with synchronous token warmup on MemberDashboardController.
- Fixes biometric route path structure to include `/mobile/` segment for proper routing.
- Prevents cross-user device hijacking in biometric registration through proper tenant and user isolation.
- Scopes biometric unenrol endpoint by DeviceId to prevent wrong-device credential revocation.
- Uses RemoteIpAddress for rate limiting instead of spoofable X-Forwarded-For header.
- Fixes mobile navigation CSS visibility on standalone pages (Layout=null) via partial inclusion.
- Corrects MobileNavLinks property type to use Link model for proper backoffice integration.
- Fixes MultiUrlPicker data type EditorUiAlias for correct backoffice UI rendering.
- Removes duplicate sign-in button from homepage hero section.
- Adds aria-label to biometric toggle checkbox for improved accessibility.
- Fixes deterministic GUID generation and property migration for mobile nav links.
- Sets EditorUiAlias on MultiUrlPicker data type for backoffice UI consistency.

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
