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

## Learnings & Handoff (2026-03-22)

**From Tom Nook Architecture Review:**
- Branding UI integration validation needed
- Modal receives `data.tenant` + `data.brandingTabs` from Backoffice API
- Branding injection happens **server-side** (PrismBrandingMiddleware), not client-side
- Ensure WCAG compliance for color overrides (check contrast ratios)
- Document CSS class names (`prism-mobile`, `prism-branding`, etc.) in backoffice UI
- CSS discovery auto-populated from CSS variables found in app CSS files
- Mobile detection: check for `prism-mobile` CSS class on page (set by middleware)
- Consider accessibility for safe-area styling on notched devices

**Next:** Validate branding tab UI against latest API; add WCAG color contrast checks to Storybook tests

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.
- 2026-03-28: Fixed SecurityError in "Produce Mobile" download — Umbraco's SPA router was intercepting the programmatic anchor click and trying to navigate to the blob: URL. Solution: Add `target="_blank"` and `rel="noopener noreferrer"` to download anchors, plus `preventDefault()` / `stopPropagation()` on button handlers. This prevents the router from capturing the click event.
- 2026-03-28: Implemented biometric bridge (issue #22) using Aparajita packages (@aparajita/capacitor-biometric-auth, @aparajita/capacitor-secure-storage) — these have different API signatures than @capacitor-community equivalents. SecureStorage.set(key, value) not {key, value}, and returns DataType directly not {value}. Always check node_modules definitions when working with Capacitor plugins.
- 2026-03-29: Added unenrolBiometric() to BiometricBridge (issue #24) — authenticated DELETE to /biometric/unenrol/{deviceId} with credentials: 'include'. Key distinction: revokeDevice() is unauthenticated + sends token in POST body; unenrolBiometric() is cookie-authenticated + uses deviceId in URL. Also added initBiometricLoginListener() and wired prismBiometricLoginComplete event into MobileBundleService's generated www/index.html (conditionally injected when biometricAuthEnabled). Always update story mocks when adding new methods to BiometricBridge interface.
