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
- 2026-03-29: Implemented biometric enrollment change detection (issue #26). Enrollment state fingerprint (biometryType|isAvailable|strongBiometryIsAvailable) stored in Preferences (not SecureStorage — it's metadata, not a credential). checkEnrollmentChange() runs BEFORE any biometric prompt in authenticate(); on mismatch, credentials are wiped via clearLocalCredentials() and BiometricError('unavailable') is thrown. State saved after both register() and authenticate() success. clearLocalCredentials() also removes enrollment state for the tenant. Two story mocks (register + settings) needed updating.
- 2026-03-30: Fixed duplicate sign-in buttons on HomePage (issue: bug report from Jonny). Root cause: `btn-mobile-signin` anchor was rendered inside the unauthenticated hero alongside the primary `btn-primary` "Sign In" CTA. In desktop/standard mode it was hidden (`display:none`), but in `html.prism-mobile` mode both buttons rendered, resulting in two "Sign In" items in the body plus the nav-bar one. Fix: removed the `btn-mobile-signin` anchor, its two CSS rules, and the unused `mobileAuthHref`/`mobileAuthLabel` C# variables. The primary hero CTA already gets full-width grid layout in mobile mode so no replacement was needed.

## Session: 2026-03-29 — Biometric Flow + Sign-In Dedup

**Task:** Remove duplicate mobile sign-in button (bug report from Jonny)

**Result:** ✅ Complete, build clean

**Context:**
The unauthenticated hero section rendered two "Sign In" buttons when running inside the Prism mobile shell:
1. The primary `btn-primary` CTA (always visible)
2. The hidden-then-revealed `btn-mobile-signin` anchor (visible only under `html.prism-mobile`)

**Decision:** Do not use hidden-then-revealed buttons as a pattern for mobile-specific CTAs. The primary CTA already gets full-width layout in mobile. If a mobile variant is needed (e.g., biometric shortcut), introduce as a distinct named element with unique label.

**Changes:**
- Removed `btn-mobile-signin` anchor element from HomePage.cshtml
- Removed unused `mobileAuthHref` and `mobileAuthLabel` C# variables
- Removed CSS rules: `.btn-mobile-signin { display:none }` and `html.prism-mobile .btn-mobile-signin { display:inline-flex }`

**Decision Record:** `.squad/decisions.md#2026-03-30-remove-btn-mobile-signin-pattern`

**Orchestration Log:** `.squad/orchestration-log/2026-03-29T160329-isabelle.md`
