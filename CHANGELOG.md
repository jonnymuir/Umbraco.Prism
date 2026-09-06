# Changelog

All notable changes to Umbraco Prism are documented here. This project follows [semantic versioning](https://semver.org/).

## [v5.0.1] — 2026-09-06

### Bug Fixes & Improvements

- Fixes a security default: forwarded-header trust (`X-Forwarded-For`) used to be unconditionally enabled for every proxy, letting a client spoof its own IP address past anything that reads it (for example, the anonymous biometric-exchange rate limiter). Trusting a proxy is now opt-in via `Prism:ForwardedHeaders` config — if you deploy behind a real reverse proxy and rely on the original client IP being visible, set `KnownProxies`/`KnownNetworks` there.
- The reference site (`UmbracoPrism.TestSite`) now targets `Wayfinder.Umbraco` 1.0.0 and `Wayfinder`/`Wayfinder.Engine` 0.9.2 — if you're following it as a pattern for your own host, match those versions.

---

## [v4.0.0] — 2026-08-21

### Breaking Changes

- **`UmbracoPrism.Core` now has zero Wayfinder dependency.** Service design capability — citizen journeys, caseworker worklists, blueprint authoring — lives entirely in `Wayfinder.Umbraco` now, hosted in-process by a site's own TestSite/host project, not proxied through Prism. Removed `AddPrismProcessManager()`, `BusinessAppProcessManagerClient`/`IBusinessAppProcessManagerClient`, the `ServiceRequestPolling` policy registration, and the `stagePage`/`serviceBlueprintDemoPage`/`serviceRequestHub` content-type seeding from `PrismContentTypeSeeder`. `UmbracoPrism.uSync` also has zero Wayfinder dependency (confirmed unused — dead weight removed).
- **`UmbracoPrism.MockBusinessApp` is no longer a workflow/service-blueprint engine host.** It's narrowed to a genuine, separate downstream application: proving Prism's own Bearer-token identity propagation (`GET /api/backoffice/me`) and a generic downstream support-system reference implementation (`POST /submissions`, `GET /submissions/{id}`, `GET /queue`, `POST /queue/{id}/decide` — mirrors the core Wayfinder repo's own `SafetyNetUnderwriting`). Removed `/admin/service-desk`, `/api/service-request/*`, `/service-blueprint-editor`, `/mockapp/service-blueprints/*`, the five generic reference blueprints, and the Actions-catalog subsystem (1,067 lines) — all superseded by Wayfinder.Umbraco's own in-process engine and editor.
- **`UmbracoPrism.TestSite`'s citizen/caseworker pages are now Block Grid-composed**, not package-owned routes. Deleted `StagePageController`, `PublicServiceRequestPageController`, `ServiceRequestHubController`, and every old demo blueprint (community-enquiry, planning, payment-demo, information-request, licence-transfer) outright. A single `wayfinderServicePage` content type now carries Wayfinder.Umbraco's packaged Block Grid stage/worklist areas, seeded for two worked examples: "Apply for a juggling licence" (anonymous-first citizen journey) and "Submit contributions file" + a caseworker queue for a fictional NJF Contributions Team, backed by a real downstream support-system call to Mock Business App.

### New Features & Improvements

- Requires `Wayfinder.Umbraco` 0.7.0+ (the published Block Grid stage/worklist elements) and `Wayfinder`/`Wayfinder.Engine` 0.7.2+.
- Two distinct signed-in personas on the reference site, not one: a plain Prism member and an NJF Contributions Team caseworker (a real, explicit roster — not "every authenticated member"), each seeing different dashboard content.

### Bug Fixes

- Fixed a real access-control regression this branch's own earlier commit introduced: a signed-in Prism member had lost the ability to apply for a juggling licence and receive their membership discount, because the new caseworker-profile resolution didn't account for the citizen journey remaining reachable while signed in. Fixed without reopening the caseworker-side data leak it was closing.
- Fixed the reference site's debug panel Sign Out button doing nothing — it posted a GET to an endpoint that requires POST + a CSRF token.
- Fixed four dead links (`/admin/service-desk`, `/service-blueprint-editor`, two MCP endpoint variants) still advertised on the Aspire dashboard after the routes behind them were removed.

## [v3.0.0] — 2026-08-09

### Breaking Changes

- **Wayfinder extracted into its own package family.** Service blueprint domain models, the calculation/state-machine engine, the visual blueprint editor, and Umbraco backoffice integration have moved out of `UmbracoPrism.Core` into four separately-versioned, separately-published packages: `Wayfinder`, `Wayfinder.Engine` (plus `.Engine.Api`/`.Engine.Mcp`), `Wayfinder.Editor`, and `Wayfinder.Umbraco`. `UmbracoPrism.Core` is now a thin Prism-specific layer on top of these. Code referencing the old `UmbracoPrism.*` namespaces for domain models, the engine, or the editor needs to move to the new `Wayfinder.*` namespaces; anything using the CMS-hosted Service Blueprint feature specifically should now reference `Wayfinder.Umbraco` directly.
- **Package distribution moved from GitHub Packages to nuget.org only.** A host still pulling from the old GitHub Packages feed needs to switch its NuGet source.
- **Tenant management moved into Umbraco's own built-in Settings section**, out of its previous location in the backoffice navigation.

### New Features & Improvements

- **Calculations tab**: visual authoring for a service blueprint's `calculations` block (fields, lookup tables, series) directly in the properties panel — live computed-value preview, automatic field-declaration ordering (a field referencing another declared later is reordered automatically, with the reasoning explained), and syntax highlighting with inline diagnostics on every expression.
- Calculation validation (unknown references, name collisions, circular dependencies, unknown `lookup()` tables) is now unified into one shared check, reused identically by the Calculations tab, the Definition tab's raw-JSON linting, and the Validation tab — a real problem is now caught and explained consistently everywhere, and proactively blocks Save before it would otherwise fail server-side.
- New screenshot-backed documentation library for the editor (`docs/skills/`) — one guide per tab (Canvas, Calculations, Validation, Preview, Simulation, Definition, Help), generated by the same behavioural tests that verify the behaviour they document.
- The component catalog (`ComponentTypeRegistry`, renamed from `ComponentTypeCatalog`) is now runtime-extensible — a host can register its own custom component types alongside the built-in catalog.

### Bug Fixes

- Fixes a real collision/validation bug: a summary-list child that echoes a calculated field's own value (the standard check-your-answers pattern) was wrongly treated as a genuine second input. This could throw a hard "Field 'x' collides with an input or earlier field" error during evaluation, and separately let a genuinely broken summary-list field binding go unflagged by validation.
- Fixes gateway-routed advances no longer firing custom stage/transition actions.
- Fixes assorted stale references left behind by the Wayfinder extraction (document-type-alias views, cookie names, endpoint paths, config keys).

---

## [v2.0.0] — 2026-07-28

### Breaking Changes

- **"Workflow" is now "Service Design" vocabulary throughout the package.** Types, namespaces, wire format field names, MCP/REST routes, and backoffice labels have all been renamed end-to-end, to stop colliding with Umbraco's own official "Workflow" package (a completely different concept — content-editor approval). There are no compatibility shims, so saved definitions, MCP clients, and custom code built against the old contract all need to move to the new one:
  - Wire format: `touchpoints`/`touchpointKey`/`touchpointType`/`initialTouchpointKey` are now `stages`/`stageKey`/`stageType`/`initialStage`.
  - C# types: `WorkflowDefinitionFile` → `ServiceBlueprint`, `WorkflowInstanceState` → `ServiceRequest`, `IWorkflowRuntimeEngine` → `IProcessManager`, and equivalent renames across stores, controllers, and the authoring toolkit (now `Wayfinder.Engine`, `.ProcessManager.Api`, `.ProcessManager.Mcp`).
  - MCP tools and REST routes: e.g. `list_workflows` → `list_service_blueprints`, `/prism/workflow-authoring` → `/prism/service-blueprint-authoring`.
- **"Touchpoint" is now "Stage"** as the generic term for a service blueprint node — a touchpoint specifically means a point of customer contact, which doesn't describe every kind of node. "Touchpoint" still appears where it's accurate, such as queue display names.
- **Removed the experimental `AuthoredServiceBlueprint`/`IServiceBlueprintProjector` authoring pipeline.** It was dead code — every real authoring surface (the backoffice editor, the MCP/REST toolkit, and the reference business app) already saved the runtime model directly, so this is a cleanup with no capability loss.

### Bug Fixes & Improvements

- Fixes the backoffice canvas silently dropping every stage's key on save, which could make a service blueprint impossible to save at all.
- Fixes saving a service blueprint from the editor silently blanking its configured start stage.
- Fixes a copy-paste bug on the canvas that could leave a pasted stage without a valid key.
- Fixes the CMS backoffice canvas's queue label ignoring a saved display name change — it was reading a fixed value rather than the blueprint's own queue metadata.
- Fixes false "unreachable stage" validation errors being raised against stages that were genuinely reachable.
- Fixes one of the reference demo service blueprints ("Request Information") never actually loading at startup, due to a JSON formatting issue that silently broke it on every app start.

---

## [v1.15.0] — 2026-07-25

### New Features

- **Anonymous CMS Workflow journeys survive signing in:** Start a CMS Workflow without signing in, then sign in partway through, and your in-progress journey now carries over automatically — it shows up as resumable under "My Workflows" instead of being silently lost when the anonymous session expires.

### Bug Fixes & Improvements

- "My Workflows" now shows in-progress and completed journeys from both workflow implementations (the business-app workflow client and Prism's own CMS Workflow) merged into a single list, instead of only the business-app ones.
- An authenticated member's CMS Workflow instance no longer expires on a sliding 30-minute window — it persists until the journey is completed, so coming back days later resumes exactly where you left off.

---

## [v1.14.0] — 2026-07-25

### New Features

- **Real-time upload progress on file-upload fields:** Choosing a file now uploads it immediately in the background with an accessible progress bar, instead of riding along with the whole stage's form submission and leaving the visitor with no feedback until the page reloads.
- **New reference workflow — Transfer a Professional Juggling Licence:** A second complete CMS Workflow demo, alongside "Apply for a juggling licence," covering the member-aware, calculation-driven journey pattern end-to-end.

### Bug Fixes & Improvements

- Fixes the CMS Workflow editor being unable to save any workflow that has components — every save failed with a generic "One or more validation errors occurred" and no further detail.
- Fixes save-time validation errors (e.g. a broken field binding or calculation) being visible only in the browser console — they now appear directly in the editor, with a clickable link to the exact stage that needs fixing.
- Fixes editing a workflow via the Definition tab silently discarding its `calculations` block and canvas layout on save, breaking any conditional field or calculated value the workflow depended on.
- Fixes the CMS Workflow workspace editor clipping tall workflows with no scrollbar.
- Rejects an unrecognised stage type at save time on every authoring surface (backoffice, REST, MCP), instead of letting it through silently and only failing later when the workflow is opened in the editor.
- Fixes an unhandled server error when uploading a file at or near the configured size limit.
- Updates Umbraco to 17.5.3, fixing a high-severity Content Delivery API vulnerability that could leak Public-Access-protected content to unauthenticated requests (relevant to hosts with the Delivery API enabled).
- Updates several transitive NuGet dependencies (Microsoft.OpenApi, System.Security.Cryptography.Xml, MessagePack) to patched versions, closing known vulnerability advisories.

---

## [v1.12.0] — 2026-07-19

### New Features

- **Prism CMS Workflow — workflow journeys hosted entirely inside Umbraco:** A second, complete implementation of the workflow engine that needs no separate business app. Author a workflow with the visual editor mounted natively in the Umbraco backoffice, save it straight to the database (uSync-portable, so a backoffice edit exports and re-imports cleanly elsewhere), and run it as a public-facing journey — anonymous by default, but the same definition can also pull in a logged-in member's own data (e.g. pre-filling a form field and applying a discount) with zero special-casing in the workflow JSON. Ships with a full "Apply for a juggling licence" reference demo covering both journeys end-to-end, and its own MCP authoring surface secured by real Umbraco backoffice admin auth (distinct from the open, local-dev-only MCP surface on the reference business app).
- **AI-ready workflow authoring toolkit (REST + MCP):** Workflow list/read/validate/save/simulate is now a reusable toolkit any host app can wire in — as plain REST endpoints or as MCP tools an AI agent can call directly — both hosted in-process so a save reaches the live engine immediately with no restart required. Adds optimistic-concurrency version conflicts (a human and an AI editing the same workflow no longer silently overwrite each other — the editor now shows a "changed elsewhere" banner with a reload action), save-time validation for unreachable dead-end routes and for components a queue's host can't actually render, and reference docs (calculation language, authoring guide, service-design principles) exposed as MCP resources an agent can read directly.
- **Declarative calculations and fully live stages:** Workflow definitions can now own their own business maths — a `calculations` block (lookup tables, calculated fields, chart series) evaluated identically by matching C# and TypeScript engines, kept in sync by a shared conformance test suite. Any stage can bind generic components (sliders, stat groups, charts, conditionally-shown fields) to that calculated data with zero bespoke per-workflow client code — a general-purpose live-form runtime re-evaluates the definition as a visitor changes inputs. An editable field can also default from a calculated or service-sourced value instead of only a static literal, with GDS-style "this was pre-filled from…" messaging.
- **Redesigned workflow canvas:** The workflow editor's graph is rebuilt on React Flow with an Automate-style visual refresh — drag-to-connect, shift-marquee multi-select and group drag, subgraph copy/paste, a minimap, and manually-arranged layouts that persist with the definition — plus a batch of graph-correctness fixes (fan-out/fan-in routing, Join gateways, cycle detection).
- **Per-row Change links on Check Your Answers:** Summary-list rows can each declare their own source stage, so a review screen's "Change" link routes back to whichever stage actually captured that specific field, instead of one link for the whole list.

### Bug Fixes & Improvements

- Fixes workflow "Change"/jump links silently failing to navigate on any gateway-routed workflow — effectively all of them, since Prism requires every stage route to pass through a gateway.
- Fixes the workflow graph rendering a phantom edge between gateways when a single stage routes to more than one.
- Adds save-time validation for two previously-silent failure modes: a summary list saved with no rows, and a gateway saved with no outgoing routes (both used to pass validation and then fail or render nothing at runtime).

---

## [v1.11.0] — 2026-07-03

### New Features

- **Environment token resolution for tenants:** Tenant identity fields (OIDC authority, client ID, secret references, etc.) can now hold a `{{TOKEN_NAME}}` placeholder that resolves from app settings or environment variables at runtime, instead of storing secrets and per-environment values directly on the tenant record. A new "token-status" endpoint and an Environment Tokens tab in the tenant create/edit UI show which placeholders are configured.
- **uSync support for tenants:** Adds a `UmbracoPrism.uSync` project so tenants can be exported and imported via uSync, with the hostname field resolved at import time.

### Bug Fixes & Improvements

- Fixes stage creation in the workflow editor writing an invalid identifier, which could break saving a new stage.
- Fixes route creation reusing an existing split gateway instead of creating a duplicate.
- Fixes copy/paste in the workflow editor losing pasted stage actions.
- Fixes workflow definition linting incorrectly flagging valid "Waiting" stages.
- Fixes the stage preview showing internal gateway routing instead of the user-facing "continue" action.
- Removes the legacy lane-based workflow compatibility layer now that the queue model (introduced in v1.10.0) is the only supported format. Workflow definitions still using `lane`/`laneKey` fields must migrate to `queue`/`queueName`.

---

## [v1.10.1] — 2026-06-27

### Changed

- **Squad is no more:** Due to cost changes in github copilot (move to AI credits) our faithful squad became too expensive. I have manually deferred some of the workflow work - into fixme stuff while I explore how to bring the work in line with either claude code, or something else.
---

## [v1.10.0] — 2026-06-08

### Changed

- **Major Worfklow changes:** Workflow reworded to include gateways and queues. State can only move between states via a gateway. Note workflow is still pretty nascent at the moment. Expect many more changes over the next few month, it is still very experimental.
---

## [v1.9.1] — 2026-05-08

### Changed

- **Marketplace package readme:** NuGet packages now ship the generated `MARKETPLACE.md` as the package readme so the Umbraco Marketplace listing can render the marketplace-friendly copy instead of the GitHub-oriented README.
- **Marketplace sync guardrails:** Added repo scripts and CI/release checks to keep `MARKETPLACE.md` generated from `README.md` and stop releases if the marketplace copy is out of date.

---

## [v1.9.0] — 2026-05-04

### Added

- **Workflow v2.0 atomic schema replacement:** Complete rewrite of workflow field definitions to first-class component hierarchy. All workflow fields now inherit from `PrismComponent` with polymorphic type resolution, enabling better composition and runtime extensibility.
- **Workflow information-request demo page:** New comprehensive workflow demo page showcasing information-request patterns with full Playwright coverage and seeded sample data.
- **Business API arrival instrumentation:** Added diagnostic middleware to log Business API request arrival (method, path, trace ID, auth status) before authentication. Safe caller trace ID forwarding via `X-Prism-Caller-TraceId` header enables cross-service request correlation.
- **Enhanced downstream timeout diagnostics:** Expanded diagnostics surface with explicit backchannel usage indicators, target path details, timeout window metadata, and cancellation source context. Helps operators disambiguate public-tunnel vs. backchannel wiring issues.
- **Transport and backchannel diagnostics in downstream demo:** Exposed transport type, backchannel presence, base URL, and scheme diagnostics to all downstream demo responses for better operator visibility.
- **Full-URL status page on Codespaces startup:** Codespaces now prints the complete status page URL on application startup for direct access to diagnostics dashboard.
- **Keycloak JWKS backchannel discovery:** JWKS endpoint discovery now bypasses `jwks_uri` in Keycloak discovery document and uses HTTP probe for TestSite, allowing safe retrieval via backchannel URLs in Codespaces.
- **Polymer field type in workflows:** Added decimal field type validation with planning confirmation reference number support for finance workflow use cases.
- **Conditional fields documentation refresh:** Updated architecture docs for v2.0 schema with conditional field patterns and component composition examples.
- **Keycloak security documentation:** Comprehensive documentation for OIDC/Keycloak security setup with CI loopback certificate trust patterns.

### Changed

- **Codespaces recovery tooling:** Added robust recovery utilities and enhanced diagnostics script for Codespaces environments (shell-only, no Python dependency).
- **Keycloak JWKS backchannel URLs handling:** Escaped URLs are now properly rewritten in auth flows to work with backchannel requests. `X-Forwarded` headers are injected on backchannel refresh to fix `invalid_grant` errors.
- **Workflow seed migration:** Migrated all stale workflow seed JSONs to polymorphic v2.0 schema with roundtrip guard tests to prevent schema drift.
- **MockBusinessApp diagnostics:** Arrival middleware now logs authentication status (success/failure) alongside request context without exposing credentials or internal URLs.
- **Codespaces port forwarding:** Added `forwardPorts` array to `.devcontainer` for automatic port configuration in GitHub Codespaces.
- **Dynamic BusinessApp endpoint discovery:** Codespaces now uses dynamic endpoint discovery (port 7245) for BusinessApp backchannel instead of hardcoded URL.
- **TestSite InMemoryAuto models builder:** Switched to `InMemoryAuto` for ModelsBuilder to prevent view conflicts in local development.

### Fixed

- **Workflow backchannel auth:** Fixed workflow API calls in Codespaces to use `BUSINESSAPP_BACKCHANNEL_URL` instead of public endpoint. Added 401 regression tests for null auth header scenarios. Aligned workflow handlers to `Results.Problem` for consistent error responses.
- **CancellationToken fragility in tests:** Replaced fragile `CancellationToken` matchers with `It.IsAny<CancellationToken>()` to fix environment-variable race conditions in CI and local test collection ordering.
- **Dashboard backchannel 401 diagnostics:** Enhanced 401 error diagnostics to clarify backchannel connection issues with actionable guidance.
- **Downstream demo JSON validation:** Added strict JSON response validation with HTML rejection to prevent error pages being returned as application data.
- **Keycloak data directory permissions:** Pre-created Keycloak data directory with world-writable permissions to fix CI initialization failures.
- **CI route warmup:** Added pre-creation of all authored routes in localhost-auth readiness gate to ensure first-render Razor compile timeout is sufficient.
- **CI first-render timeout:** Raised `expect` timeout to 30s for first-render Razor compile to handle CI warmup overhead.
- **BusinessApp localhost backchannel:** Restored proper BusinessApp localhost backchannel configuration after Codespaces URL changes.
- **npm vulnerability patches:** Applied `npm audit fix` to patch critical handlebars CVE and 10 high-severity vulnerabilities in npm dependency tree.
- **CookieSecurePolicy enforcement:** Hardened `PrismMemberCookie` to use `CookieSecurePolicy.Always` for all environments.

### Security

- **Workflow state authorization:** Added authorization to `WorkflowPollController` to prevent unauthenticated access to workflow state (SEC-001).
- **HTML injection in workflow components:** Introduced `IWorkflowContentSanitizer` with GDS-aligned allowlist to sanitize dynamic HTML in workflow display components, closing XSS attack surface (SEC-003).
- **Structured logging injection protection:** Replaced string interpolation with structured logging in `PrismTenantMiddleware` to prevent log injection attacks (SEC-009).
- **Unsafe aria attributes encoding:** Added HTML encoding to `aria-describedby` attributes to prevent attribute injection attacks (SEC-011).
- **Proxy-aware rate limiting:** Added `ForwardedHeadersMiddleware` to respect `X-Forwarded-For` headers for accurate biometric rate limiting behind reverse proxies (SEC-007).

---

## [v1.8.0] — 2026-04-30

### Added

- **Generic OIDC provider support:** Prism now supports any OIDC-compliant identity provider (not just Azure AD). Configure custom OIDC endpoints per tenant for full flexibility in identity routing.
- **Tenant API and model enhancements:** Expanded tenant entity with new fields and API endpoints for runtime tenant management, enabling dynamic provisioning workflows.
- **Workflow and forms capabilities:** Embedded workflow state machine and forms engine for automation and user-friendly data collection within tenant contexts.
- **Mobile app UI polish:** Refined responsive design, improved accessibility across mobile and desktop views, and enhanced component library with sticky action buttons.
- **JWKS and nonce validation:** Implemented per-tenant JWKS validation for ID token signatures and strict nonce replay protection in OIDC flows.
- **Structured auth logging:** Replaced debug output with secure structured logging to prevent accidental exposure of sensitive tenant data in production logs.
- **Key Vault 404 fallback:** Graceful handling of missing secrets in Azure Key Vault with fallback to local config, allowing safe dev/staging workflows.

### Changed

- **CookieSecurePolicy hardening:** Updated `PrismMemberCookie` to use `CookieSecurePolicy.Always` to ensure the `Secure` flag is set in all environments, preventing transmission over unencrypted HTTP.

### Fixed

- **Android biometric compatibility:** Fixed GNU sed incompatibility on macOS by using Perl for biometric manifest injection. Auto-upgraded Gradle to support Java 25.
- **Singleton-scoped service resolution:** Fixed `InvalidOperationException` in background services by using `IServiceScopeFactory` for transient scoped service resolution.
- **Modal scrolling and layout:** Restored vertical scrolling in maximized modals and preserved `uui-dialog-layout` component with full accessibility compliance.
- **Design system:** Refactored to ITCSS for improved cascade management, added responsive CSS variables with inheritance chain indicators, and comprehensive ARIA improvements.
- **Workflow state exposure:** Added authorization to `WorkflowPollController` to prevent unauthenticated access to workflow state (SEC-001).
- **HTML injection in workflow components:** Introduced `IWorkflowContentSanitizer` with GDS-aligned allowlist to sanitize dynamic HTML content in workflow display components, closing XSS attack surface (SEC-003).
- **Committed HMAC signing key:** Rotated HMAC key and moved to `appsettings.Local.json` (gitignored) to prevent secret exposure in version control (SEC-004).
- **npm dependency vulnerabilities:** Applied `npm audit fix` to patch critical handlebars CVE and 10 high-severity vulnerabilities in npm dependency tree (SEC-005).
- **Proxy-aware rate limiting:** Added `ForwardedHeadersMiddleware` to respect `X-Forwarded-For` headers for accurate biometric rate limiting behind reverse proxies (SEC-007).
- **Entra ID credential leakage:** Replaced real Azure Entra tenant/client IDs and PII in MockBusinessApp config with placeholder values (SEC-010).
- **Unsafe aria attributes:** Added HTML encoding to `aria-describedby` attributes to prevent attribute injection (SEC-011).

### Security

- **Upgraded Microsoft.AspNetCore.DataProtection** to patched version to address GHSA-9mv3-2cwr-p262 CVE (SEC-002).
- **Upgraded OpenTelemetry.Api** to 1.12.1+ to address GHSA-g94r-2vxg-569j moderate advisory (SEC-008).
- **Structured logging injection fix:** Replaced string interpolation with structured logging in `PrismTenantMiddleware` to prevent log injection attacks (SEC-009).

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
