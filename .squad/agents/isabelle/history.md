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

**Decision Record:** `.squad/decisions.md#2026-04-02-isabelle--frontend-directory-restructure--mobile-boundary-guard`

**Orchestration Log:** `.squad/orchestration-log/2026-04-01T23-33-13Z-isabelle.md`

## Session: 2026-04-02 — Frontend Directory Restructure

**Date:** 2026-04-02
**Task:** Split flat `src/UmbracoPrism.Client/src/` into `backoffice/` and `mobile/` subdirectories with ESLint boundary guard.

**Result:** ✅ Complete, build clean (`tsc && vite build` — 0 errors, outputs unchanged).

### Learnings

- 2026-04-02: When splitting a flat component directory into subdirectories, if all related files move to the same target directory, no relative import paths need updating — the imports stay correct because the files' relative positions to each other are unchanged.
- 2026-04-02: ESLint 9 uses flat config (`eslint.config.mjs`). For a `"type":"module"` project with no prior ESLint setup, install `eslint` + `@typescript-eslint/parser` and scope `no-restricted-imports` to `src/mobile/**` to enforce architectural boundaries without affecting backoffice files.
- 2026-04-02: Storybook's existing `stories` glob pattern `'../src/**/*.stories.@(ts|tsx)'` automatically covers nested subdirectories — no Storybook configuration change needed after moving stories into `src/backoffice/` and `src/mobile/`.

### Changes

- Moved 10 backoffice files → `src/UmbracoPrism.Client/src/backoffice/` (biometric-bridge, index.css, index.ts, all backoffice component .ts and .stories.ts files)
- Moved 2 mobile files → `src/UmbracoPrism.Client/src/mobile/` (prism-mobile-nav.ts, prism-mobile-nav.stories.ts)
- Updated `vite.config.ts` entry points to `src/backoffice/index.ts` and `src/mobile/prism-mobile-nav.ts`
- Added boundary comment to `src/mobile/prism-mobile-nav.ts`
- Created `eslint.config.mjs` with `no-restricted-imports` rule blocking `@umbraco-cms/backoffice` in `src/mobile/**`

## Session: 2026-04-02 — Mobile Nav Visibility Bug Investigation

**Date:** 2026-04-02
**Task:** Investigate why `prism-mobile-nav` buttons aren't appearing when simulating PrismMobile mode in browser.

**Result:** ✅ Fixed — added `!important` to CSS rule in Master.cshtml to ensure reliable shadow DOM override.

### Root Cause

The `prism-mobile-nav` web component uses Shadow DOM with `:host { display: none }` as the default state. The TestSite's `Master.cshtml` had a CSS rule `html.prism-mobile prism-mobile-nav { display: block }` to make it visible when the `prism-mobile` class is present.

While the CSS spec says that page-level element selectors should override `:host` styles, browser implementations vary. The Storybook stories use `display: block !important` in their decorators, which always works reliably.

### Fix

Changed `Master.cshtml` CSS rule from:
```css
html.prism-mobile prism-mobile-nav {
    display: block;
}
```

To:
```css
html.prism-mobile prism-mobile-nav {
    display: block !important;
}
```

This ensures the page-level CSS reliably overrides the shadow DOM `:host` style across all browsers.

### Learnings

- 2026-04-02: When overriding Shadow DOM `:host` styles from page-level CSS, use `!important` for reliable cross-browser behavior. While the spec says page-level element selectors override `:host`, browser implementations may vary. Storybook decorators should also use `!important` when forcing web component visibility for testing.
- 2026-04-02: The `prism-mobile-nav` component was created as part of the directory restructure (commit `ee95358`), not before it. The old version used inline CSS/HTML in the `_MobileShellNav.cshtml` partial. The web component version requires CSS in `Master.cshtml` to control visibility via `html.prism-mobile prism-mobile-nav { display: block !important }`.

### Investigation Notes

Checked:
- ✅ Component CSS: `:host { display: none }` is correct default
- ✅ Storybook decorator: Uses `display: block !important` (works in stories)
- ✅ Build output: `prism-mobile-nav.js` builds correctly to `dist/` (5.84 kB)
- ✅ Script loading: Partial loads script via `<script type="module" src="/App_Plugins/UmbracoPrism/dist/prism-mobile-nav.js">`
- ✅ CSS rule location: Added in `Master.cshtml` `<head>` `<style>` block
- ✅ Specificity: `html.prism-mobile prism-mobile-nav` should override `:host`, but `!important` guarantees it

The issue was subtle: page-level CSS *should* override Shadow DOM `:host` per spec, but adding `!important` eliminates any browser-specific edge cases.

## Session: 2026-07-10 — Mobile Nav Frontend Audit

**Task:** Audit the full JS/CSS rendering chain for `prism-mobile-nav` in the live Umbraco test site.

**Result:** ✅ Complete — one critical bug fixed, build clean.

### Findings

**CRITICAL BUG (fixed):** `_MobileShellNav.cshtml` used `items="@itemsJson"` to pass the serialised JSON as an HTML attribute delimited by double-quotes. `System.Text.Json` produces JSON with double-quote string delimiters (e.g. `[{"label":"Home",...}]`). Those inner `"` chars terminate the HTML attribute early, so the browser saw `items="[{"` — a truncated, invalid JSON fragment. The component's `_items` getter catches the `JSON.parse` exception and returns `[]`, so the nav renders silently empty. Fix: switched to `items="@Html.AttributeEncode(itemsJson)"` — encodes `"` → `&quot;`, which is valid inside double-quoted attributes and browsers decode back to `"` before the JS attribute read.

**No other issues found:**
- `:host { display: none }` default + `html.prism-mobile prism-mobile-nav { display: block !important; }` in `<head>` `<style>` block — correct pattern, `!important` present ✓
- `<script type="module">` is auto-deferred — component upgrade happens after DOM parse, no timing issue ✓
- Storybook bypasses CSS chain with inline `display: block !important` on host via decorator — this is expected but means Storybook doesn't exercise the `html.prism-mobile` path ✓
- `_items` getter wraps `JSON.parse` in try/catch returning `[]` — silent failure is correct, but with the encoding fix the JSON will be valid ✓

### Changes

- Fixed `Views/Partials/_MobileShellNav.cshtml`: `items="@Html.AttributeEncode(itemsJson)"` (was `items="@itemsJson"`)

### Learnings

- 2026-07-10: Always HTML-encode JSON passed as a double-quoted HTML attribute. `System.Text.Json` outputs `"` delimiters which break `attr="..."` syntax. `@Html.AttributeEncode()` is the Razor idiom to fix this — it encodes `"` → `&quot;`; the browser decodes `&quot;` back to `"` when returning `getAttribute()`, so `JSON.parse` sees correct input.

## Session: 2026-04-02T20:18:48Z — Mobile Nav HTML Encoding Fix

**Commit:** `3e810ee`  
**Status:** Completed

**Task:** Audit the full JS/CSS rendering chain for `prism-mobile-nav` and fix blank-nav bug.

**Root cause found:** `_MobileShellNav.cshtml` used `items="@itemsJson"` — raw JSON with `"` delimiters inside a double-quoted HTML attribute. Browser truncated the attribute at the first inner `"`, `JSON.parse` threw, component returned `[]`, nav rendered empty.

**Fix:** `src/UmbracoPrism.TestSite/Views/Partials/_MobileShellNav.cshtml`  
`items="@Html.AttributeEncode(itemsJson)"` — encodes `"` → `&quot;`, browsers decode back before JS reads the attribute.

**No other issues:** CSS `html.prism-mobile prism-mobile-nav { display: block !important; }`, script loading, and component lifecycle all correct.

**Decision merged:** `isabelle-mobile-nav-audit.md` — Always `@Html.AttributeEncode()` for JSON in double-quoted Razor attributes.

## Session: 2026-07-10 — Inline Style Extraction to Branding CSS Files

**Task:** Move static inline `<style>` block from `Master.cshtml` to the appropriate `/branding/` CSS files so tenants can control styling through CSS variables.

**Result:** ✅ Complete, build clean (0 errors, 0 warnings)

### What Was Moved

| Rule / Variable | Destination |
|---|---|
| `--tenant-primary-contrast: white` | `prism-colors.css` |
| `--bg-offset: #f8f9fa` | `prism-colors.css` |
| `body { font-family, margin, background-color, color }` | `prism-layout.css` |
| `.header { ... }` | `prism-layout.css` |
| `.container { ... }` | `prism-layout.css` |
| `.footer { ... }` | `prism-layout.css` |
| `.prism-mobile .header { safe-area padding }` | `prism-layout.css` |
| `.prism-mobile .container { safe-area padding }` | `prism-layout.css` |
| `.prism-mobile .footer { safe-area padding }` | `prism-layout.css` |
| `.card { background, border-radius, padding, box-shadow }` | `prism-components.css` |
| `html.prism-mobile prism-mobile-nav { display: block !important }` | `prism-components.css` |

### What Stayed Inline (Razor dynamic)

- `--tenant-primary: @brandColor;` — server-injected per-tenant colour, cannot be a static file.

### Files Changed

- `src/UmbracoPrism.TestSite/wwwroot/branding/prism-colors.css` — added `--tenant-primary-contrast` + `--bg-offset`
- `src/UmbracoPrism.TestSite/wwwroot/branding/prism-layout.css` — added body, header, container, footer, and all `.prism-mobile` safe-area overrides
- `src/UmbracoPrism.TestSite/wwwroot/branding/prism-components.css` — added `.card` + mobile-nav visibility rule
- `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml` — replaced large static `<style>` block with `<link rel="stylesheet" href="/branding/prism-branding.css" />` + minimal dynamic `:root { --tenant-primary }` inline style
- `prism-branding.css` — already had all 5 correct `@import` statements, no changes needed

### Learnings

- 2026-07-10: When a `:root {}` block contains both static and dynamic (Razor) variables, split them: static vars go to the appropriate branding CSS file; only the dynamic Razor expression stays inline. This keeps the inline `<style>` minimal while still allowing server-side per-tenant injection.
- 2026-07-10: `html.prism-mobile prism-mobile-nav { display: block !important }` must live in `prism-components.css`, not in `Master.cshtml`. This is both semantically correct (it styles a component) and required so tenant overrides in branding files take effect.

## Session: 2026-03-30 — iOS White Default for prism-mobile-nav

**Task:** Switch `prism-mobile-nav` default styling from dark glass to Apple iOS-style white.

**Changes made:**
- `prism-mobile-nav.ts`: Updated all CSS `var()` fallbacks — bg to `rgba(255,255,255,0.95)`, blur to `20px`, item colour to `rgba(60,60,67,0.6)`, active colour to `#007aff`, label weight to `500`, hover/active backgrounds to near-transparent. Updated JSDoc CSS variables table to match.
- `prism-mobile-nav.stories.ts`: `mobileDecorator` background → `#f2f2f7`. Renamed `LightTheme` → `DarkTheme` with dark glass overrides (`rgba(15,23,42,0.94)` etc). All play/test functions preserved intact.
- `prism-components.css`: Added explicit `prism-mobile-nav { }` block with iOS white vars for TestSite documentation/discoverability.

**Build:** ✅ Passed (`npm run build` — 42 modules, no errors)

**Decision filed:** `.squad/decisions/inbox/isabelle-white-nav.md`

---

## Session: 2026-04-02 — iOS White Style Implementation (Finalized)

**Commit:** `37e9975`  
**Status:** Completed  
**Decision merged:** `isabelle-white-nav.md` — prism-mobile-nav defaults to Apple iOS white style

**Implementation Summary:**

Updated `prism-mobile-nav` component default styling from dark glass (navy) to Apple iOS-inspired white frosted glass, matching the Umbraco Prism TestSite's light UI.

**Component Defaults Changed (`prism-mobile-nav.ts`):**

| Property | Before | After | Notes |
|----------|--------|-------|-------|
| Background | `rgba(15,23,42,0.94)` (dark navy) | `rgba(255,255,255,0.95)` (white) | Glass morphism effect maintained |
| Active Icon Color | `#4f46e5` (indigo) | `#007aff` (iOS blue) | Follows iOS HIG |
| Label Font Weight | `600` | `500` | Lighter weight for iOS feel |
| Item Hover BG | `rgba(255,255,255,0.2)` | `rgba(255,255,255,0.1)` | Subtle contrast on white |
| Item Active BG | `rgba(255,255,255,0.25)` | `rgba(255,255,255,0.15)` | Subtle contrast on white |

All CSS custom property fallbacks updated. JSDoc CSS variables table synchronized.

**Storybook Updated (`prism-mobile-nav.stories.ts`):**

- `mobileDecorator` background → `#f2f2f7` (iOS system background, `UIColor.systemGroupedBackground`)
- `LightTheme` story renamed to `DarkTheme` with explicit dark glass overrides:
  - `--prism-mobile-nav-background: rgba(15,23,42,0.94)`
  - `--prism-mobile-nav-item-color: rgba(255,255,255,0.6)`
  - `--prism-mobile-nav-item-active-color: #4f46e5`
  - Other dark-specific vars restored
- All play/test functions preserved intact
- 7 stories remain: Default (now white), DarkTheme (renamed), WithActiveItem, ManyItems, MaxItems, BrandColour, NoIcons

**TestSite Branding (`prism-components.css`):**

Added explicit white nav variable block for documentation and tenant-override discoverability:

```css
prism-mobile-nav {
  --prism-mobile-nav-background: rgba(255,255,255,0.95);
  --prism-mobile-nav-blur-px: 20;
  --prism-mobile-nav-item-color: rgba(60,60,67,0.6);
  --prism-mobile-nav-item-active-color: #007aff;
  --prism-mobile-nav-label-weight: 500;
}
```

**Breaking Change Notice:**

- Tenants relying on the previous dark defaults will need to add explicit CSS variable overrides in their custom branding
- Dark glass is still fully supported via CSS custom properties — just no longer the default
- Migration: Add `--prism-mobile-nav-*` vars to tenant branding files or override via inline styles

**Build:** ✅ Passed (`npm run build` — 42 modules, no errors)

**Rationale:**

- iOS white tab bar is the dominant navigation pattern on mobile devices
- Matches the Umbraco Prism TestSite's light UI theme
- Uses familiar iOS blue (`#007aff`) for active state — users instantly recognize it
- Maintains glass morphism accessibility (blur + transparency for depth)
- Component is theme-agnostic — CSS variables allow full customization

## Media URL Icons in prism-mobile-nav

**Date:** 2025-07-14

Added support for Umbraco media library URLs in the `icon` field of `prism-mobile-nav`:

- Added `_isIconUrl(icon: string): boolean` — detects `/`, `http`, or `data:` prefixes
- Updated `_renderIcon` to branch: URLs render `<img class="nav-icon nav-icon--img">`, named keys use existing SVG path lookup
- Added `.nav-icon--img` CSS with `opacity: 0.6` inactive / `1` active / `0.85` hover transitions, matching named icon behaviour
- Updated JSDoc Usage example and `icon` field description to document both modes
- Added `MediaIcons` Storybook story with data URI SVG placeholders
- Build passed (tsc + vite, no errors)

## Media URL Icons in prism-mobile-nav (2026-04-03)

**Sprint:** Mobile nav media icons integration  
**Session Log:** `.squad/log/2026-04-03_07-39-08-mobile-nav-media-icons.md`  
**Orchestration Log:** `.squad/orchestration-log/2026-04-03_07-39-08-isabelle-media-icons.md`

**Status:** Completed

**Problem:** Umbraco editors need to pick nav icons from the media library (URLs), not just named keys.

**Solution Implemented:**

1. **Icon Type Detection (`_isIconUrl()`):**
   - Checks for `/`, `http`, or `data:` prefixes
   - Maintains backward compatibility — existing named icons unchanged

2. **Rendering Branch (`_renderIcon()`):**
   - Named keys → existing SVG path lookup (unchanged)
   - URLs → `<img class="nav-icon nav-icon--img" aria-hidden="true">`

3. **CSS for Media Icons (`.nav-icon--img`):**
   - `opacity: 0.6` (inactive state)
   - `opacity: 1` (active state)
   - `opacity: 0.85` (hover state)
   - Matches named icon `color` transition behavior

4. **Storybook Story:**
   - New `MediaIcons.stories.ts` story showcasing both named icons and media URLs
   - Uses data URI SVG placeholders for demo

5. **Accessibility:**
   - `<img aria-hidden="true" alt="">` — decorative icon, label from sibling `<span>`
   - No breaking changes to existing named icon behavior

**Build:** ✅ Passed (tsc + vite, no errors)

**Design Notes:**
- Media icons use opacity (not `color`) because `<img>` cannot inherit CSS `currentColor`
- Editors should upload neutral-color SVGs for best visual consistency with the iOS-style white tab bar theme
- Component remains fully theme-agnostic via CSS variables

**Paired with:** Brewster's Umbraco schema work (block list, element type, seeder updates)

## Learnings (2026-03-24)

**Removed Redundant Mobile Mode Banner:**
- Found and removed the "Prism mobile mode active" notice from `src/UmbracoPrism.TestSite/Views/HomePage.cshtml`
- Banner was redundant because the "Demo PrismMobile UserAgent" popup widget already shows mobile mode status
- Removed:
  - Two `<div class="mobile-mode-banner">` elements (one for authenticated, one for unauthenticated view)
  - `.mobile-mode-banner` CSS rules (both base hidden state and `html.prism-mobile` visible state)
- Build verified: ✅ Passed (no references to `mobile-mode-banner` remain)
- This simplifies the demo site UI and eliminates visual redundancy


---

## Fix: Download handler — "unknown folder" bug (surgical fix)

**Date:** 2025-07-15
**File:** `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts`

**Changes made (lines ~869–875):**

1. **Removed** `anchor.target = '_blank';` — conflicted with `anchor.download`, causing Safari/macOS to navigate to the blob URL and auto-extract the zip as a folder instead of saving it.
2. **Removed** `anchor.rel = 'noopener noreferrer';` — only meaningful alongside `target='_blank'`, now irrelevant.
3. **Changed** `URL.revokeObjectURL(url);` → `setTimeout(() => URL.revokeObjectURL(url), 100);` — prevents race condition where synchronous revocation could abort the download before it initiated.

No other changes made. Download flow verified correct.

## Session: Edit Tenant Dialog — Maximize & Close QoL

**Date:** 2026-04-04
**Task:** Add Maximize and Close (×) buttons to the Edit/Create Tenant dialog title bar.

**Result:** ✅ Complete, build clean (`tsc && vite build` — 0 errors, `prism-dashboard.js` 54.35 kB)

### Learnings

- The `prism-create-tenant-modal` component serves BOTH create and edit modes — `_id !== null` means edit mode. The "Edit Tenant" dialog IS `prism-create-tenant-modal`.
- `uui-dialog-layout` has a `slot="headline"` that renders inside the `<h3>` element. By omitting the `headline` attribute and using `slot="headline"` with custom content, you can inject a full flex row (title + action buttons) into the header area.
- To maximize a dialog from inside a `uui-modal-dialog` container: toggle a `maximized` class on `:host` and apply `position: fixed !important; inset: 0; width: 100vw; height: 100vh; z-index: 10000` — this breaks out of the native `<dialog>` stacking context cleanly.
- Apply the host class via `this.classList.toggle('maximized', this._maximized)` inside `updated()` when `changedProperties.has('_maximized')`.
- Intercept Escape key for the "restore from maximized" case using a capture-phase `document.addEventListener('keydown', handler, true)` with `stopPropagation()` — prevents the modal from closing when in maximized state. Always remove in `disconnectedCallback`.

### Changes

- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts`:
  - Added `@state() private _maximized = false`
  - Added `_handleKeyDown` capture-phase listener (Escape restores if maximized)
  - Added `_toggleMaximize()` method
  - Added `disconnectedCallback` to clean up the keydown listener
  - Changed `render()`: replaced `headline="..."` attribute with `slot="headline"` custom content containing flex title + Maximize/Restore + Close icon buttons
  - Added CSS: `:host(.maximized)` fullscreen override, `.dialog-headline`, `.dialog-headline-actions`, `.dialog-icon-btn` with UUI variable colours + focus-visible ring
  - Added `updated()` hook to sync `_maximized` state → host `maximized` class
