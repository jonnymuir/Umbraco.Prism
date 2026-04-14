# Changelog

All notable changes to Umbraco Prism are documented here. This project follows [semantic versioning](https://semver.org/).

## [v1.8.0] — 2026-04-14

### New Features

- **Generic OIDC provider support:** Prism now supports any OIDC-compliant identity provider (not just Azure AD). Configure custom OIDC endpoints per tenant for full flexibility in identity routing.
- **Tenant API and model enhancements:** Expanded tenant entity with new fields and API endpoints for runtime tenant management, enabling dynamic provisioning workflows.
- **Workflow and forms capabilities:** Embedded workflow state machine and forms engine for automation and user-friendly data collection within tenant contexts.
- **Mobile app UI polish:** Refined responsive design, improved accessibility across mobile and desktop views, and enhanced component library with sticky action buttons.

### Security Enhancements

- **JWKS and nonce validation:** Implemented per-tenant JWKS validation for ID token signatures and strict nonce replay protection in OIDC flows.
- **Structured auth logging:** Replaced debug output with secure structured logging to prevent accidental exposure of sensitive tenant data in production logs.
- **Key Vault 404 fallback:** Graceful handling of missing secrets in Azure Key Vault with fallback to local config, allowing safe dev/staging workflows.

### Bug Fixes & Improvements

- **Android biometric compatibility:** Fixed GNU sed incompatibility on macOS by using Perl for biometric manifest injection. Auto-upgraded Gradle to support Java 25.
- **Singleton-scoped service resolution:** Fixed `InvalidOperationException` in background services by using `IServiceScopeFactory` for transient scoped service resolution.
- **Modal scrolling and layout:** Restored vertical scrolling in maximized modals and preserved `uui-dialog-layout` component with full accessibility compliance.
- **Design system:** Refactored to ITCSS for improved cascade management, added responsive CSS variables with inheritance chain indicators, and comprehensive ARIA improvements.

---

## [v1.7.1] — 2026-04-06

### Security Improvements

- **ID token signature validation:** ID tokens are now cryptographically validated using per-tenant JWKS endpoints. Signatures must match the tenant's current key set; invalid signatures are rejected with a 401 response.
- **Nonce validation enforcement:** Nonce values in ID tokens are validated against the original authorization request nonce. Mismatches are treated as a hard failure and prevent token acceptance, closing the window for replay attacks.
- **Structured logging for auth flows:** Replaced debug console output (which inadvertently exposed tenant information) with structured logging via `ILogger<T>`. Auth flows now emit proper telemetry without exposing sensitive data to stdout.

---

## [v1.7.0] — 2026-04-05

### New Features

- **Mobile/desktop CSS variable inheritance:** Responsive design system with `chain` and `broken-chain` UI indicators showing inheritance state across device breakpoints.
- **Design system showcase:** New interactive demo of design tokens, styles, and component library. Sticky action buttons for improved UX in long-form documentation.
- **ITCSS style organization:** Refactored stylesheets using Inverted Triangle CSS methodology for cleaner cascade management and maintainability.
- **Accessibility audit fixes:** Enhanced keyboard focus management, sticky dialog headers, and comprehensive ARIA improvements for better screen reader support.

### Bug Fixes & Improvements

- **Modal scrolling:** Restored vertical scrolling in maximized modals with `overflow-y: auto` on host element.
- **Dialog layout preservation:** Restored `uui-dialog-layout` component while preserving accessibility fixes without breaking layout.
- **Dialog headline padding:** Removed unnecessary left/right padding from dialog headlines for improved visual hierarchy.
- **Mobile inheritance toggle:** Synced test assertions with mobile inheritance UI refactor.
- **Modal header UX:** Updated header styling, accessibility attributes, and test coverage for better visual consistency and usability.

---

## [v1.6.1] — 2026-04-03

### Fixed
- **Android bootstrap**: Replaced GNU sed with `perl -i -pe` for biometric manifest injection to fix BSD sed incompatibility on macOS. Added automatic Gradle wrapper upgrade to 8.14 to support Java 25 (class file major version 69).
- **DI scoped-in-singleton**: `LimitedEditionDropNotifier` now resolves `IPrismNotificationService` via `IServiceScopeFactory` per invocation, fixing an `InvalidOperationException` on app startup when scoped services were consumed from a singleton `BackgroundService`.
- **Key Vault 404 fallback**: `PrismKeyVaultConfigureOptions` now distinguishes 404 (secret not found) from 403 (access denied). A 404 logs a warning and returns, allowing config-bound dev secrets to remain in effect. A 403 still throws.

### Changed
- **UA demo popup**: Moved the mobile user-agent demo widget higher above the navigation bar (increased bottom offset to `calc(5rem + 1.5rem)` in the `html.prism-mobile` context). Added a × close button that dismisses the widget for the remainder of the browser session.

---

## [v1.6.0] — 2026-07-24

### New Features

- **Push notification system:** Send push notifications to mobile app members via Firebase Cloud Messaging (FCM) and Apple Push Notification service (APNs). Includes device token registration, subscription management, and topic-based routing.
- **Device token registration API:** Endpoints for members to register and manage push tokens (`POST /umbraco/prism/push/register`, `DELETE /umbraco/prism/push/register`).
- **Genre-based subscriptions:** Members can subscribe to content-based notifications by genre or topic (`POST /umbraco/prism/push/subscribe`, `DELETE /umbraco/prism/push/subscribe`).
- **Vinyl Vault demo content:** Test site now includes a fully-seeded product catalog with 7 music genres and 28 vinyl records for showcasing notifications in a realistic e-commerce scenario.
- **Push notifications bundle option:** Added `pushNotificationsEnabled` flag to mobile app generation. When enabled, the generated Capacitor app includes the `@capacitor/push-notifications` plugin and native iOS/Android push setup instructions.
- **Capacitor push plugin integration:** Mobile apps now support `@capacitor/push-notifications` plugin for native push delivery on both iOS (APNs) and Android (FCM).
- **Limited edition drop notifier:** Background scheduled service that monitors limited edition releases and triggers push notifications to interested members.
- **Back-in-stock notification API:** Endpoint for triggering back-in-stock alerts (`POST /umbraco/prism/vinyl/back-in-stock`).

### Improved

- **Rate limiting on notification endpoints:** Notification registration and subscription endpoints are rate-limited (10 registrations, 20 subscriptions per hour per user) to prevent abuse.
- **Push token validation:** Device tokens are validated for length and format before storage; invalid tokens are rejected with clear error messages.
- **Genre input sanitization:** Subscription topics are sanitized and validated to prevent injection attacks.
- **Stale token cleanup:** Tenant-scoped background job automatically removes expired or invalid push tokens from the database, keeping the notification queue clean and efficient.

---

## [v1.5.1] — 2026-04-11

### Bug Fixes

- **Bundle download on Safari:** Removed `target='_blank'` and `rel='noopener noreferrer'` attributes from the download anchor. Safari was incorrectly opening a new tab instead of triggering the browser download dialog.
- **Bundle download SecurityError fix:** Changed from `anchor.click()` to `dispatchEvent(new MouseEvent('click', { bubbles: false }))` to bypass Umbraco's SPA global click interceptor, which was throwing SecurityError when handling blob: URLs.

## [v1.5.0] — 2026-04-10

### What's new

- **Zero-config Azure Key Vault integration:** `PrismKeyVaultConfigureOptions` provides automatic Key Vault integration via `IConfigureOptions<PrismBiometricOptions>`. Consumers no longer need `builder.AddPrismKeyVault()` in Program.cs.

### Improved

- **Key Vault error messages:** Now distinguish 401 (identity/auth), 403 (permissions), 404 (missing secrets), and transient errors with actionable guidance.

### Added

- `CONTRIBUTING.md` — contributor guidelines for the project.
- `.github/FUNDING.yml` — sponsorship information.

### Internal

- `PrismKeyVaultExtensions.AddPrismKeyVault()` retained as optional explicit opt-in for fail-fast startup behaviour.

## [v1.4.0] — 2026-04-09

### New Features

- **Configurable media library icons for mobile navigation:** Mobile navigation items now support a `navIcon` property backed by a media picker. Icons are sourced from the Umbraco media library instead of being hardcoded, enabling backoffice control over nav appearance. Icons are seeded automatically into a "Prism Navigation Icons" media folder with sample SVG files on first run.

### Bug Fixes & Improvements

- **Mobile nav demo widget UX:** The "Demo PrismMobile UserAgent" popup widget now properly stacks above the mobile navigation bar (fixed z-index). It repositions itself automatically when mobile mode is activated, preventing overlap with nav items.
- **Streamlined demo site:** Removed redundant inline "Simulate PrismMobile" checkbox from hero buttons (functionality now provided by popup widget). Removed the "Prism mobile mode active" banner notice (widget indicates mode state more intuitively).
- **Block list draft state fixed:** Added the required `expose` array to seeded block list JSON, ensuring block items are immediately live in Umbraco v14+ without requiring manual "Create" button clicks to unpublish the draft state.
- **Settings node persistence:** Fixed seeder not persisting the Settings node when the Block List data type was configured in the same run.
- **Media key persistence:** Fixed media icon keys being regenerated across seeder runs; icons now correctly reuse existing media items and avoid duplication.
- **Mobile Nav Item property descriptions:** Corrected `navLabel` and `navUrl` property descriptions that were showing as "null" in the backoffice.
- **Block list label template:** Updated label template to use Umbraco v17+ syntax (`{=navLabel}`) instead of deprecated Angular-style `{{navLabel}}`.
- **Code cleanup:** Removed unnecessary backwards-compatibility patching logic for block label formats (Prism targets v17+ exclusively, no legacy upgrade path).

## [v1.3.2] — 2026-03-31

### New Features

- **Biometric auto-login (server-side injection):** `PrismBrandingMiddleware` now injects an auto-login script into unauthenticated mobile HTML pages. The script checks SecureStorage for a biometric token, prompts Face ID/Touch ID, exchanges the token for a `PrismMemberCookie` session, and reloads the page. Falls back gracefully to the normal login page if no token exists or biometry is declined.
- **Biometric credential revoke endpoint:** New `DELETE /umbraco/prism/mobile/biometric/revoke` endpoint soft-revokes credential records. Without a deviceId, revokes all credentials for the user on the current tenant (logout path). With a deviceId, revokes a specific device. Returns 204 idempotently.

### Bug Fixes & Improvements

- **Stale token detection after app reinstall:** iOS Keychain persists across app deletion. The enrollment and auto-login scripts now verify that `localStorage.ENROLL_KEY` exists alongside any Keychain token. If a token exists but no ENROLL_KEY (fresh install), the stale token is cleared and re-enrollment is triggered.
- **Credential clearing on logout:** Implemented capture-phase click listener that detects navigation to logout/signout URLs and clears: SecureStorage token, biometric enrollment state, device ID from localStorage, plus calls the server-side revoke endpoint.
- **Fixed missing `prism_device_id` logout:** `prism_device_id` is now properly cleared from localStorage on logout (was previously omitted).
- **Fixed middleware auth check:** `context.User.Identity?.IsAuthenticated` was always returning false in `PrismBrandingMiddleware.InvokeAsync`. Corrected by ensuring middleware runs after the authentication middleware in the pipeline.
- **Secure signing key pattern:** `PrismBrandingMiddleware` constructor now accepts `ILogger<PrismBrandingMiddleware>` for proper logging. Biometric signing key is now managed via .NET User Secrets (dev) and Azure Key Vault (production), configured as `Prism:Biometric:SigningKey` (minimum 32 characters).

### Upgrade Notes

If you are using biometric auto-login, ensure the signing key is set before deploying:
- **Development:** Run `dotnet user-secrets set "Prism:Biometric:SigningKey" "<your-key>"` (minimum 32 characters).
- **Production:** Store the signing key in Azure Key Vault and ensure `AddAzureKeyVault()` is called in your app startup.

## [v1.3.1] — 2026-03-30

### Chores

- Moved `DownstreamDemoController` from Core package to TestSite — demo code should not ship in the NuGet package.

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
