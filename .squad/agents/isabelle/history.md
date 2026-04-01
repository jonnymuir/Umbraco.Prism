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

## Session: 2026-03-31 — prism-mobile-nav Web Component

**Task:** Convert `_MobileShellNav.cshtml` inline Razor nav to a Lit web component `<prism-mobile-nav>`.

**Result:** ✅ Complete, build clean (`tsc && vite build` — 0 errors)

### Learnings

- 2026-03-31: Switching from `build.lib` to `rollupOptions.input` (multi-entry) in vite.config.ts lets you produce multiple standalone ES module bundles in one build. Use `entryFileNames: '[name].js'` to preserve friendly filenames. Rollup automatically creates shared chunks for common deps (e.g. Lit core → `property-[hash].js`); all files land in the same `outDir` so relative imports resolve correctly.
- 2026-03-31: Shadow DOM `:host { display: none }` can be overridden by higher-specificity page-level CSS (e.g. `html.prism-mobile prism-mobile-nav { display: block }`). This is the correct cross-browser pattern when `:host-context()` is not available (WebKit/Safari does not support `:host-context()`).
- 2026-03-31: Storybook `decorators` can be used at the story level to swap out the canvas context for themed variants — cleaner than duplicating story args just to change CSS custom properties.
- 2026-03-31: Do NOT add frontend-only web components (no Umbraco backoffice imports) to `index.ts`. They should be separate Vite entries so they can be loaded independently from the test site without pulling in external Umbraco module references that would fail in a non-backoffice context.
- 2026-03-31: For `ifDefined` from `lit/directives/if-defined.js` — pass `undefined` (not `null` or `false`) to remove the attribute. Pattern: `attr="${ifDefined(condition ? 'value' : undefined)}"`.

### Changes

- Created `src/prism-mobile-nav.ts` — Lit web component with glass-morphism dark default, full `--prism-mobile-nav-*` CSS custom property set, built-in SVG icons (home, dashboard, account, settings, transactions, notifications, more), `aria-current="page"` on active item, safe-area-inset bottom padding
- Created `src/prism-mobile-nav.stories.ts` — 7 stories: Default, WithActiveItem, ManyItems(5), MaxItems(6), LightTheme, BrandColour, NoIcons, AccessibilityCheck
- Updated `Views/Partials/_MobileShellNav.cshtml` — removed all inline `<style>`, serialises `Link[]` to JSON, renders `<prism-mobile-nav items="..." current-path="..." nav-label="...">`, loads `prism-mobile-nav.js` via `<script type="module">`
- Updated `Views/Shared/Master.cshtml` — added `html.prism-mobile prism-mobile-nav { display: block; }` visibility rule
- Updated `vite.config.ts` — replaced `build.lib` (single entry) with `rollupOptions.input` (multi-entry: `prism-dashboard` + `prism-mobile-nav`)

**Decision Record:** `.squad/decisions/inbox/isabelle-mobile-nav-component.md`

## Session: Src Directory Restructure

**Date:** 2026-04-02
**Task:** Split `src/UmbracoPrism.Client/src/` flat structure into `backoffice/` and `mobile/` subdirectories, add ESLint mobile boundary guard.

**Result:** ✅ Complete, build clean (`tsc && vite build` — 0 errors, `prism-dashboard.js` 49.73 kB, `prism-mobile-nav.js` 5.84 kB)

### Learnings

- 2026-04-02: `git mv` was unavailable in the Copilot bash environment (permission error) — plain `mv` + `git add -A` produces identical rename detection in git history, so this is a safe workaround.
- 2026-04-02: When splitting a flat component directory into subdirectories, if all related files move to the same target directory, no relative import paths need updating — the imports stay correct because the files' relative positions to each other are unchanged.
- 2026-04-02: ESLint 9 uses flat config (`eslint.config.mjs`). For a `"type":"module"` project with no prior ESLint setup, install `eslint` + `@typescript-eslint/parser` and scope `no-restricted-imports` to `src/mobile/**` to enforce the Umbraco-free boundary without affecting backoffice files.
- 2026-04-02: Storybook `stories` glob `'../src/**/*.stories.@(ts|tsx)'` automatically covers nested subdirectories — no Storybook config change needed after moving stories into `src/backoffice/` and `src/mobile/`.

### Changes

- Moved 10 backoffice files → `src/UmbracoPrism.Client/src/backoffice/` (biometric-bridge, index.css, index.ts, all backoffice component .ts and .stories.ts files)
- Moved 2 mobile files → `src/UmbracoPrism.Client/src/mobile/` (prism-mobile-nav.ts, prism-mobile-nav.stories.ts)
- Updated `vite.config.ts` entry points to `src/backoffice/index.ts` and `src/mobile/prism-mobile-nav.ts`
- Added boundary comment to `src/mobile/prism-mobile-nav.ts`
- Created `eslint.config.mjs` with `no-restricted-imports` rule blocking `@umbraco-cms/backoffice` in `src/mobile/**`

**Decision Record:** `.squad/decisions/inbox/isabelle-src-restructure.md`
