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

## Session: 2026-04-04 — Test Site CSS Refactor

**Task:** Extract all inline CSS from test site views into organised, separate CSS files.

**Result:** ✅ Complete, all inline styles extracted (except dynamic server-side CSS variable injections)

### Context

The test site had CSS embedded as `<style>` blocks directly in `.cshtml` view files (HomePage, MemberDashboard, VinylGenreLanding, VinylRecord, VinylVaultHome). This made the codebase harder to maintain and didn't showcase good practice. The goal was to extract all CSS into well-organised files using ITCSS (Inverted Triangle CSS) principles while keeping it as clean and simple as possible (this is a demo/test site, not production).

### CSS File Structure Created

**wwwroot/css/** (ITCSS layer order):
- `base.css` — HTML element defaults (body styles, mobile body override)
- `layout.css` — Page structure, grid systems, containers (portal header, page containers, grids, footer, vinyl vault layouts, mobile overrides)
- `components.css` — Reusable UI patterns (hero, buttons, cards, features, dashboard sections, biometric card, profile card, API card, mobile nav, vinyl vault components)
- `utilities.css` — State/modifier classes (prism-debug visibility, mobile web component visibility)

### Patterns Consolidated

- Portal header & dashboard header → unified in `layout.css`
- All button variants (btn-primary, btn-ghost, btn-secondary, btn-outline, btn-subscribe, btn-waitlist) → `components.css`
- Card patterns from HomePage and MemberDashboard → unified `.card` and `.dash-card` in `components.css`
- Vinyl Vault components (vinyl-card, vinyl-cover, badges, breadcrumb, genre-tile) → `components.css`
- Mobile overrides for hero, grid, cards → `components.css` and `layout.css`

### What Stayed Inline

Dynamic server-side CSS variable injections (these are runtime values, not static styles):
- `Master.cshtml`: `--tenant-primary` (dynamic brand color from tenant data)
- `HomePage.cshtml`: `--prism-hero-image`, `--prism-card-image` (dynamic imagery URLs from Umbraco media)

### Branding CSS (Intentionally Separate)

The `wwwroot/branding/` directory was **not touched** — it contains deliberately structured CSS files that are a key feature of Umbraco.Prism, showcasing how CSS variable overrides work for tenant-specific branding:
- `prism-colors.css`
- `prism-typography.css`
- `prism-layout.css`
- `prism-components.css`
- `prism-imagery.css`
- `prism-branding.css`

### Changes

- Created `wwwroot/css/base.css` (358 bytes)
- Created `wwwroot/css/layout.css` (2.9 KB)
- Created `wwwroot/css/components.css` (14 KB)
- Created `wwwroot/css/utilities.css` (308 bytes)
- Updated `Master.cshtml` to link the new CSS files in ITCSS order
- Removed 346-line style block from `HomePage.cshtml`
- Removed 391-line style block from `MemberDashboard.cshtml`
- Removed 73-line style block from `VinylGenreLanding.cshtml`
- Removed 110-line style block from `VinylRecord.cshtml`
- Removed 51-line style block from `VinylVaultHome.cshtml`

### Decision Record

`.squad/decisions/inbox/isabelle-css-structure.md` — Documents the chosen CSS layer structure and rationale.

### Learnings

- ITCSS works well for demo/test sites when kept minimal — don't create empty layers for the sake of the pattern
- Consolidating duplicate patterns (e.g., .card vs .dash-card) reduces CSS size but requires careful naming
- Inline `<style>` for dynamic server-side values (CSS variables) is the correct pattern — don't extract runtime values to static files
- Branding CSS should stay separate when it's a key feature showcase, not just project organization
- Removed legacy `--tenant-primary` and `--tenant-primary-contrast` CSS variables — these were server-injected from `ThemeColor` property (never exposed in UI)
- The proper tenant colour system is `--prism-primary` and `--prism-primary-contrast`, managed through the CSS variable override files in `wwwroot/branding/`
- This removed the last server-injected `<style>` block from `Master.cshtml` (the only remaining inline styles are for dynamic Umbraco media URLs)
- Also removed `themeColor: '#3544b1'` from the tenant creation modal in the backoffice — that property no longer exists on the backend

## Session: 2026-04-08 — Workflow Forms Engine Client Design

**Task:** Create comprehensive client-side design document for Prism Workflow Forms Engine.

**Result:** ✅ Complete — produced `docs/design/workflow-forms-engine-client.md` (62KB, 991 lines)

### Learnings

- 2026-04-08: Workflow client architecture uses "Hybrid adapter model" — generic `prism-workflow-*` Web Components that are channel-agnostic, plus thin adapters for backoffice UUI integration. Mobile shell uses components directly. This maximizes reuse while maintaining native feel in each context.
- 2026-04-08: Orchestrator state machine pattern isolates components from HTTP/polling concerns. Components consume `WorkflowRenderPayload` only and dispatch semantic events (`submit`, `action`). The orchestrator owns lifecycle (`idle → creating → asking → submitting → waiting → polling → complete → error`) and handles polling timers, optimistic concurrency (`stateVersion`), and correlation IDs.
- 2026-04-08: GDS (Government Digital Service) design system principles fit workflow forms perfectly: one-question-per-page (optional progressive disclosure), error summary at top with jump-to-field links, clear labels + hints + error messages, no jargon, step indicators, "Back" navigation. All implemented via CSS custom properties for theming (mobile vs backoffice variants).
- 2026-04-08: WCAG 2.2 AA compliance requires: keyboard navigation for all controls, ARIA roles (`role="alert"`, `role="status"`, `aria-live`), focus management on archetype transitions, screen reader announcements for polling states, 4.5:1 contrast ratios, visible focus indicators (3:1 contrast). Pre-demo checklist with axe Storybook addon validation.
- 2026-04-08: Workflow forms use 8 archetypes: `Collect` (form input), `Review` (read-only summary), `TaskQueue` (operator pending list), `Decision` (approve/reject/request-changes), `RequestChanges` (correction list), `StatusTimeline` (GDS step indicator), `Completion` (terminal outcome). Each has dedicated Lit component with fixture-driven Storybook stories.
- 2026-04-08: Polling behavior: backend returns `responseState: 'wait'` + `pollAfterMs` → orchestrator schedules timer → client polls `GET /render` → repeats until `ask_now` or `complete`. Max 60 attempts (5min default). Polling state shows `role="status"` with `aria-live="polite"` for screen readers.
- 2026-04-08: TypeScript contract design mirrors backend: `WorkflowEnvelope` (full response), `WorkflowRenderPayload` (archetype + fieldGroups + actions), `FieldDescriptor` (key/type/label/required/validationRules/conditionalVisibility), `WorkflowProblem` (category/field/message/code). Shared via `workflow/types/index.ts`.
- 2026-04-08: Validation errors keep orchestrator status as `asking` (not `error`) so user can correct and re-submit. Field-level errors use `aria-invalid="true"` + `aria-describedby` linking to hint + error spans. Error summary uses `role="alert"` and links with `href="#field-{key}"` that scroll and focus the field.

### Design Decisions

**Component Boundaries:**
- Shell owns orchestrator instance, components never call API directly
- Components receive `renderPayload` prop only, dispatch semantic events
- Adapter layer (backoffice) translates events to UUI notifications/modals

**Accessibility First:**
- Pre-demo checklist blocks sign-off until WCAG 2.2 AA verified
- Focus management on every archetype transition
- Screen reader announcements for all state changes
- Keyboard-only testing required for all stories

**GDS Progressive Disclosure:**
- `progressiveDisclosure: boolean` flag on field groups
- One-question-per-page reduces cognitive load (optional pattern)
- "Check your answers" before final submit
- "Back" navigation preserves answers without validation

**File Structure:**
```
src/workflow/
├── orchestrator/
├── api/
├── types/
├── fixtures/ (JSON render payloads)
├── components/ (8 archetypes + stories)
└── styles/ (GDS-inspired CSS)
```

**Storybook Coverage:**
- Minimum 5 stories per archetype (default, errors, loading, accessibility check, theme variant)
- Fixture-driven (JSON render payloads in `fixtures/`)
- axe addon validates every story
- Play functions test interactions

**CSS Theming:**
- ~30 CSS custom properties per archetype
- Mobile variant: iOS blue (`#007aff`), iOS fonts
- Backoffice variant: GDS blue (`#1d70b8`), Inter font
- Theme switcher via decorator in Storybook

### 2026-03-22: CSS Branding Annotations + Dynamic Tenant Editor

Implemented the branding design system feature in two parts:

**Part 1: CSS Annotations**
Added `@property` declarations and `/* @prism section | label | description */` comments to all CSS variables in:
- `prism-colors.css` — Brand Colours (18 variables)
- `prism-typography.css` — Typography (11 variables)
- `prism-layout.css` — Layout (7 variables)
- `prism-components.css` — Components + Mobile Navigation (22 variables)
- `prism-imagery.css` — Imagery (3 variables)

Each variable now has:
- Native `@property` syntax declaration for browser validation
- Structured metadata comment for editor UI generation
- Organized into semantic sections (Brand Colours, Typography, Layout, Components, Mobile Navigation, Hero Section, Imagery)
- Type-aware annotations (color, font, length, image, url, text)

**Part 2: Dynamic Tenant Editor UI**
Updated `prism-create-tenant-modal.ts` to:
- Fetch branding metadata from `/umbraco/api/prism/branding/metadata` API
- Render dynamic form fields based on metadata (color pickers, text inputs, image fields)
- Group fields by section with `<uui-box>` containers
- Show separate Desktop/Mobile inputs for each variable
- Gracefully degrade to static fields if API fetch fails
- Display loading spinner while fetching metadata
- Collect dynamic values on save and map to tenant branding overrides

**Key Technical Decisions:**
- Chose native `<input type="color">` for color pickers (simple, accessible)
- Kept fallback to static table-based branding editor for error resilience
- Used Record<string, string> state objects for dynamic value tracking
- Triggered metadata fetch on first branding tab click
- Maintained backward compatibility with existing `_brandingTabs` static structure

**Integration Notes:**
- Works in parallel with Blathers' CSS parser and metadata API endpoint
- Respects existing branding override save flow
- UI adapts to whatever sections/variables the CSS parser discovers
- No hardcoded field definitions — completely driven by CSS annotations

**Build Status:** ✅ TypeScript compiled clean, Vite build succeeded

This completes the branding-as-design-system feature. The tenant editor now dynamically reflects the annotated CSS structure, giving operators a polished, section-organized UI for live branding edits.

---

## Session: Media Library Picker for Image Variables

**Date:** 2025-07

**Task:** Add proper Umbraco media library picker for `type: image` CSS variables in the tenant branding editor.

**What Was Built:**
- Replaced plain URL text input for `type: image` variables with a polished picker UI
- **Thumbnail preview** — strips `url('...')` wrapper to show an `<img>` preview (max-height 60px, object-fit: cover), with graceful `@error` hide if image fails to load
- **"Pick from Media Library" button** — opens `UMB_MEDIA_PICKER_MODAL` via `UMB_MODAL_MANAGER_CONTEXT.open()`, fetches media URL from `/umbraco/management/api/v1/media/{unique}`, wraps in `url('...')` CSS format
- **Free-text URL input** — always visible, pre-filled with current value; user can type/paste any URL directly
- **Clear button** — shown only when a value is set; removes the current value

**Key Implementation Details:**
- Imports: `UMB_MODAL_MANAGER_CONTEXT` from `@umbraco-cms/backoffice/modal`, `UMB_MEDIA_PICKER_MODAL` from `@umbraco-cms/backoffice/media`
- Auth token: follows existing `consumeContext(UMB_AUTH_CONTEXT, ...)` / `authContext.getLatestToken()` pattern for the media URL fetch
- CSS value format: picker writes `url('/media/...')` for proper CSS background-image usage; free-text input stored as-is
- Selection returns `Array<string | null>` (GUIDs), fetched via management API
- New `_pickMediaForVariable(varName: string, isMobile: boolean)` async method added to class
- CSS classes: `.image-picker`, `.image-picker__preview`, `.image-picker__actions` added to component styles

**Build Status:** ✅ TypeScript + Vite build clean, 0 errors

## Learnings

### UUI Component Accessibility (uui-input label attribute)
- `uui-input` requires its own `label` attribute for accessibility, even when a visible `<label>` element is already present in the DOM nearby.
- In `_renderDynamicField`, use `label=${variable.label}` for dynamic fields; in static table loops, use template literals like `"${variable.name} (desktop override)"` for unique per-row labels.
- The `uui-button` component also needs a `label` attribute — audit all button instances when resolving these warnings.

## Session: 2026-07-10 — Media Picker Endpoint Fix

**Task:** Fix media picker not updating input box (wrong API endpoint)

**Result:** ✅ Complete, build clean, all 34 tests pass

**Root Cause:** `_pickMediaForVariable` called `/media/{id}` which returns full item details with no `urls` property. Correct endpoint is `/media/urls?id={id}` which returns an array of `MediaUrlInfoResponseModel`.

**Fix:** Updated fetch URL and response parsing in `prism-create-tenant-modal.ts`. Added `console.warn` for empty URL responses.

**Test:** Created `tests/media-url-extraction.spec.ts` — 4 pure logic tests (no browser needed) covering happy path, empty urlInfos, null url, and empty array.

## Learnings

- 2026-07-10: The Umbraco Management API `/media/{id}` returns full item details (no `urls` property). Use `/media/urls?id={id}` to get a `MediaUrlInfoResponseModel[]` with `urlInfos[].url`. Always check the Umbraco Management API docs for the correct URL resolution endpoint.

## Session: 2026-07-11 — Mobile Branding Inheritance System

**Task:** Fix phantom mobile overrides bug + add chain/broken-chain inheritance UI for mobile branding variables

**Result:** ✅ Complete, build clean (TypeScript + Vite, 0 errors)

**What Was Fixed:**

**Problem 1 — Phantom mobile overrides (core bug):**
- `_dynamicMobileBrandingValues` was initialised to `variable.currentValue` for every variable, even those without explicit mobile overrides
- `_collectMobileBrandingOverrides` saved all non-empty values, creating phantom overrides for every variable at CSS defaults
- These phantom overrides won in the CSS cascade over desktop changes
- **Fix:** Added `_mobileInherited: Record<string, boolean>` state. On init, set `_mobileInherited[varName] = !explicitMobileOverride`. In collect, only write override if `!_mobileInherited[varName]`

**Problem 2 — Chain/broken-chain inheritance UI:**
- Added `@state() private _mobileInherited: Record<string, boolean> = {}` near other `@state()` declarations
- Mobile row now shows 🔗 (chained/inheriting) or ⛓️ (unchained/custom) toggle button
- Chained: shows desktop value grayed out (opacity 0.5, pointer-events: none), "inheriting from desktop" label
- Unchained: shows editable input pre-populated from desktop value, "custom" badge
- Clicking 🔗 breaks chain: copies current desktop value as starting point, sets `_mobileInherited[varName] = false`
- Clicking ⛓️ restores chain: sets `_mobileInherited[varName] = true` (no override saved)
- Removed old mobile "Reset to default" button (was resetting to CSS default — wrong)
- Desktop `resetValue(isMobile)` simplified to `resetValue()` (desktop only)

**Problem 3 — Description display bug (backend):**
- `PrismBrandingMetadataService.ParsePrismAnnotation` (line 172-175) falls back to storing the full raw annotation string as `Description` when no `description:` key is present in the `@prism` annotation
- e.g. if a CSS var has `@prism section: Components | label: Background colour` (no description field), the entire annotation string becomes the displayed description
- This is a backend issue — noted for Blathers, not fixed here

**Key Implementation Details:**
- `_mobileInherited` defaults to `{}` (empty) — `!== false` check used so that any key not yet set defaults to inherited
- `effectiveMobileValue` derived in render: if inherited, use current desktop value; else use stored mobile value
- `data-testid` attributes added: `mobile-inherit-toggle-${varName}`, `mobile-inherit-label-${varName}`, `mobile-field-${varName}`

## Learnings

- 2026-07-11: Mobile inheritance state should be stored separately from the value itself. Using `_mobileInherited[varName] !== false` (rather than a strict `=== true`) means newly-added variables default to inherited without needing explicit initialisation.
- 2026-07-11: Backend `PrismBrandingMetadataService.ParsePrismAnnotation` falls back to storing the full `@prism` annotation string as description when no `description:` key is present. Any CSS variable without a `description:` field in its annotation will leak the full annotation into the UI.

---

## Session: 2026-07-15 — Mobile Branding Inheritance

- Added `_mobileInherited: Record<string, boolean>` state to `prism-create-tenant-modal.ts`.
- Fixed phantom mobile override bug: `_collectMobileBrandingOverrides` now only saves explicitly unchained variables.
- Fixed initialisation: `_mobileInherited[varName] = !tenantMobileOverrides[varName]` on load.
- Added chain/broken-chain toggle UI (🔗/⛓️) per variable in `_renderDynamicField`.
- On break: copies current desktop value as mobile starting point. On restore: clears mobile override.
- Added `data-testid` hooks: `mobile-inherit-toggle-{varName}`, `mobile-field-{varName}`, `mobile-inherit-label-{varName}`, `mobile-custom-badge-{varName}`.
- Decision logged: mobile inheritance model. See `decisions.md`.

---

## Session: 2026-04-05 — Mobile Inheritance UI Cleanup & Accessibility

**Task:** Remove emoji icons from mobile inheritance UI and implement clean, accessible toggle buttons

**Result:** ✅ Complete, build clean (TypeScript + Vite, 0 errors), 38/38 Playwright tests passing. Committed as d661c53.

**Changes Made:**

1. **Removed emoji icons (🔗/⛓️)** from toggle buttons entirely
2. **Inheriting state UI:**
   - Shows "Inheriting from desktop" label (font-size: 0.85rem, muted color, italic)
   - Shows "Customise for mobile" button (uui-button, look="outline")
   - Mobile input field **completely hidden** via `display: none` (not dimmed with opacity)
   - Keeps `label="Break mobile inheritance"` for accessibility
3. **Custom (non-inheriting) state UI:**
   - Shows "Custom mobile value" badge (warning color, font-size: 0.75rem, more padding)
   - Shows "Reset to desktop" button (uui-button, look="placeholder")
   - Mobile input field **fully visible and interactive**
   - Keeps `label="Restore mobile inheritance"` for accessibility
4. **Test updates:**
   - Updated `prism-mobile-branding-inheritance.spec.ts` to check for `display: none` in addition to `pointerEvents: none`
   - All test data-testid selectors and label attributes remain unchanged

**Key Implementation Details:**
- Mobile field container (`data-testid="mobile-field-${varName}"`) stays in DOM even when hidden (for test compatibility)
- Used `display: none` instead of `opacity + pointer-events: none` for cleaner hiding
- Button text is now descriptive actions ("Customise for mobile" / "Reset to desktop") instead of emoji
- Accessibility: `label` attribute on buttons clearly describes the action ("Break mobile inheritance" / "Restore mobile inheritance")

**Decision Logged:** `.squad/decisions.md#2026-04-05-mobile-inheritance-ui-cleanup--accessibility`

## Learnings

- 2026-04-05: When hiding UI elements, `display: none` is cleaner than `opacity + pointer-events` but requires test updates to check `getComputedStyle(element).display` rather than just `pointerEvents`. Tests should be flexible to handle multiple hiding strategies.
- 2026-04-05: UUI buttons support `label` attribute for accessibility (used by screen readers). This is separate from the button's text content. Both should be clear and descriptive.
- 2026-04-05: Emoji in UI can be inaccessible and feel hacky. Prefer clear English text for action buttons, especially for important interactions like toggling inheritance state.

## Session: 2026-XX-XX — Design System Tokens Showcase (Home Page)

**Task:** Build a rich Design System Tokens showcase section on the test site home page to demonstrate Prism's branding capabilities

**Result:** ✅ Complete

**Changes Made:**

1. **Added new `ds-section` to HomePage.cshtml** (line 95, after `.features`, before `.prism-debug-section`)
   - Section header with title, subtitle, and CTA link to back office
   - Six subsections showcasing different token categories:
     - **Colour palette** — 7 swatches (primary, accent, surface, surface-alt, text, muted, border) with live CSS var rendering
     - **Typography** — Live type scale samples (display, body, small, mono) with token labels
     - **Layout** — Border radius demo (full/half/none), shadow demo, gutter demo
     - **Imagery** — Card image preview with `--prism-card-image` fallback gradient
     - **Components** — Live button, chip/badge, and mini-card using token-driven styles

2. **Added supporting CSS classes to components.css** (appended to end of file):
   - `.ds-section` — Main container with section gap spacing
   - `.ds-grid` — Responsive grid using `--prism-grid-min` token
   - `.ds-card`, `.ds-card--wide` — Card modifiers (wide spans 2 columns, collapses on mobile)
   - `.token-palette`, `.token-swatch`, `.token-swatch__color` — Colour swatch grid and squares
   - `.token-chip` — Monospace token name label (uses `--prism-chip-bg/text`)
   - `.type-scale`, `.type-scale__sample` — Typography sample stack
   - `.layout-tokens`, `.radius-demo`, `.shadow-demo`, `.gutter-demo` — Layout property visualizations
   - `.imagery-preview` — Imagery card with `--prism-card-image` background
   - `.component-showcase`, `.showcase-chip`, `.mini-card` — Live component demos

3. **No hero button update** — The "View Branding Tokens" button mentioned in the task brief was not found in the current HomePage.cshtml (may have been removed in a prior refactor)

**Key Design Decisions:**
- Used CSS Grid with `grid-template-columns: repeat(auto-fit, minmax(var(--prism-grid-min), 1fr))` for responsive layout
- All styling driven by CSS custom properties (no hardcoded colours/sizes)
- Colour swatches show live computed values via inline `style="background: var(--prism-primary)"`
- Typography samples render actual text at token-defined sizes (not just labels)
- Layout demos show visual representations of spacing/shadow/radius tokens
- Component showcase uses existing `.btn`, `.card` classes to prove token integration
- Mobile-responsive: wide cards collapse to single column on narrow screens

**Token Coverage:**
- Covers all major token categories from branding CSS files:
  - Colours (prism-colors.css)
  - Typography (prism-typography.css)
  - Layout (prism-layout.css)
  - Imagery (prism-imagery.css)
  - Components (prism-components.css)

**Accessibility:**
- All interactive elements are keyboard-accessible
- Token chips use sufficient colour contrast (chip-bg/chip-text tokens)
- Semantic HTML structure with proper heading hierarchy
- Non-breaking space in subtitle to prevent awkward line breaks

## Learnings

- 2026-XX-XX: For showcase/demo sections, visual richness is key — show, don't tell. Live rendered samples (actual colours, actual type at actual sizes, actual shadows) are far more compelling than just listing token names.
- 2026-XX-XX: CSS Grid `auto-fit` + `minmax()` creates fluid responsive grids without media queries. Use `--prism-grid-min` token for consistent breakpoint behavior.
- 2026-XX-XX: `grid-column: span 2` for wide cards works well, but always add a mobile media query to collapse back to single column for narrow screens.
- 2026-XX-XX: When building token showcases, use inline `style=""` attributes for dynamic token values (e.g., `background: var(--prism-primary)`) — this is one of the few valid use cases for inline styles.

---

## Session: 2026-04-06 — Design System Showcase on Home Page

**Task:** Replace the basic design tokens section on the home page with a comprehensive, polished design system showcase

**Result:** ✅ Complete. Replaced the minimal design tokens section (`id="design-tokens"`) with a rich four-part design system showcase (`id="design-system"`).

**What Was Built:**

**1. Colour Palette Grid:**
- Six logical groupings: Brand, Surfaces, Semantic, Text, Data Visualisation, Hero
- Total of 27 colour swatches showing live CSS variable values
- Each swatch shows: coloured rectangle (64px × 64px), human-readable label, variable name in monospace
- Responsive grid: `repeat(auto-fill, minmax(100px, 1fr))`

**2. Typography Section:**
- Three subsections: Font Families, Type Scale, Font Weights
- Font families demo: display, body, and mono fonts rendered live using actual CSS variables
- Type scale: sm, md, lg, xl sizes demonstrated with sample text
- Font weights: body and heading weights shown side-by-side
- All text samples use the CSS variables so changes update instantly

**3. Layout Section:**
- Visual demos: border radius (80px square), card shadow (80px card), section gap (stacked blocks)
- Grid system demo: three columns using `--prism-grid-min`
- Spacing tokens list: page-max, page-gutter, nav-height with descriptions

**4. Imagery Section:**
- Hero image, card image, image radius demos
- Blend mode demo showing `background-blend-mode: var(--prism-hero-image-blend)`
- Graceful degradation: "No image set" placeholder when imagery variables are empty
- Uses `heroImageUrl` and `cardImageUrl` from Razor context to determine if images are set

**Styling Approach:**
- Scoped styles in a `<style>` block within the section (as per instructions)
- All colours, fonts, spacing use CSS variables (no hardcoded values)
- Responsive: mobile breakpoint at 768px adjusts grid columns and padding
- Design matches existing site aesthetic: cards with rounded corners, subtle shadows, muted labels

**Key Implementation Details:**
- Section ID changed from `#design-tokens` to `#design-system` (will require updating any links that pointed to the old anchor)
- Retained existing Razor variables: `heroImageUrl`, `cardImageUrl`, `isAuthenticated`, etc.
- Used existing CSS classes where applicable (e.g., `btn`, `card`, `grid` — though most styling is scoped)
- BEM-like naming convention for design system classes: `ds-subsection`, `ds-color-grid`, `ds-swatch`, etc.

**Decisions Made:**
- Chose 4 subsections (Colour, Typography, Layout, Imagery) over alternatives like tabbed UI or single wall of content — better scannability
- Grouped colours semantically (Brand, Surfaces, etc.) rather than alphabetically
- Included ALL available CSS variables from the spec (27 colour variables, 13 typography, 10+ layout, 5 imagery)
- Used inline styles for live demos (`style="background: var(--prism-primary)"`) to show variables in action
- Mobile-first responsive: single column on mobile, multi-column on desktop

**Files Modified:**
- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml` — Replaced design tokens section (lines 95-235)

**Quality:** No build step required (Razor .cshtml file). Visual sanity check passed — section will render correctly with or without imagery set.

## Learnings

- 2026-04-06: When building design system showcases, group tokens semantically (Brand, Surfaces, Semantic) rather than alphabetically or by CSS property type. This matches how designers think about design systems.
- 2026-04-06: For live design token demos, use inline styles (`style="background: var(--prism-X)"`) to make it crystal clear that the value is reading from a CSS variable. This also ensures the demo updates instantly when tenant branding changes.
- 2026-04-06: Graceful degradation for optional imagery: show "No image set" placeholder rather than broken images or errors. Use Razor conditionals (`@if (string.IsNullOrWhiteSpace(heroImageUrl))`) to check if media picker fields are populated.

### 2026 — Mobile Inheritance UI Redesign

Redesigned the Mobile header in `_renderDynamicField()` to match the Desktop header's inline pattern.

**Before:** Mobile section had a separate italic "Inheriting from desktop" text line + a large `look="outline"` "Customise for mobile" button; the custom state had a separate `<div>` wrapper with different padding/border-radius styling.

**After:** Single flex row `Mobile  [pill]  [button]` mirroring Desktop:
- Inheriting: neutral pill (`--uui-color-surface-emphasis` / `--uui-color-text-alt`) + `look="placeholder" compact` "Customise" button
- Custom: warning pill (same as Desktop "modified") + `look="placeholder" compact` "↺ Reset" button
- All `data-testid` attributes preserved for Playwright tests
- No TypeScript logic changed, only HTML template structure

### 2026 — Extract Design Showcase Styles to ITCSS

Moved the static `<style>` block from `HomePage.cshtml` (lines 564–853, containing `.design-system-section`, `.ds-subsection`, `.ds-color-group`, `.ds-swatch`, and related classes) into `wwwroot/css/components.css` under a `/* Design System Showcase */` comment block.

**Key actions:**
- Appended ~290 lines of CSS to the end of `components.css`
- Fixed `@@media` Razor escape → `@media` in the CSS file
- Removed the entire `<style>` block from the cshtml (only the dynamic `@if (hasImageryOverrides)` block with `@Html.Raw` remains)

**Decisions Made:**
- All static styles belong in ITCSS CSS files. The only permitted inline styles in `.cshtml` are dynamically generated C# (e.g. `@Html.Raw(imageryCss.ToString())`).
- These component-level styles go in `components.css` (correct ITCSS layer for UI components).

### 2026 — Modal Action Buttons Moved to Sticky Headline

Moved the Cancel and Save/Update Tenant buttons from `slot="actions"` (bottom footer) to inside `slot="headline"` (always-visible header) in `prism-create-tenant-modal.ts`.

**Structure change:**
- Wrapped existing title + icon buttons in a new `dialog-headline-top` row
- Added `dialog-headline-buttons` row beneath it containing the two action buttons with `compact` attribute
- Removed both `slot="actions"` buttons from the bottom of the render template

**CSS change:**
- `.dialog-headline` → `flex-direction: column`, `gap: var(--uui-size-space-2, 6px)`
- `.dialog-headline-top` → new rule, mirrors old `.dialog-headline` (flex row, space-between, align-items center)
- `.dialog-headline-buttons` → new rule, flex row, gap 8px, align-items center

**Why:** Long multi-tab forms meant users had to scroll to the bottom to save. Since `slot="headline"` never scrolls, placing buttons there gives always-visible access without any sticky/fixed CSS hackery. The `uui-tab-group` sticky-top behaviour is unaffected (it sticks to the top of the scrollable default-slot area, not the headline).

## Learnings

- 2026-04-01: `uui-button` elements placed inside a named slot (`slot="headline"`) of `uui-dialog-layout` are NOT reliably keyboard-navigatable — the shadow DOM slot context can intercept or skip tab order for custom elements. Fix: replace with native `<button>` elements styled to match UUI look (border, background, focus-visible ring, font-family inherit). Native buttons always participate in tab order correctly regardless of slot context.
- 2026-04-01: Modal headline area redesign — removed title text (redundant when primary action button is labelled), collapsed two-row layout to single flex row `[primary action] [cancel] · · · [maximize][close]`, removed `.dialog-headline-top`, `.dialog-headline-buttons`, `.dialog-headline-title` classes, added `.dialog-headline-actions` (left) and `.dialog-headline-icons` (right). Primary action comes first (left), Cancel second (right of primary, left of icons) — primary goal first, escape hatch second.
- 2026-04-01: When replacing `uui-button` with native `<button>` for accessibility, use `height`, `padding`, `font-family: inherit`, `font-size`, and `transition` to match UUI visual parity. For primary variant: `background-color: var(--uui-color-positive)` + `color: var(--uui-color-positive-contrast)`. Always add `:focus-visible` outline rule.

## Session: 2026-04-08 — Accessibility Audit + Fixes for prism-create-tenant-modal

**Task:** Full WCAG 2.1 AA audit + fix two confirmed bugs (keyboard focus, sticky headline) + broader ARIA pass on `prism-create-tenant-modal.ts`.

**Result:** ✅ Complete, build clean (`tsc --noEmit` 0 errors)

### Learnings

- 2026-04-08: `uui-dialog-layout` renders `slot="headline"` content AND the default slot inside the same shadow-root scroll region. Buttons in `slot="headline"` may not receive keyboard focus in all browsers because the shadow DOM tab sequence doesn't reliably propagate into named slots. The headline also scrolls with the content rather than staying fixed. **Fix:** Remove `uui-dialog-layout`; own the full layout with `:host { display:flex; flex-direction:column; overflow:hidden }` and make `.container` the sole scroll region (`flex:1; overflow-y:auto; min-height:0`).

- 2026-04-08: Shadow DOM focus-seeding pattern for modals: use `firstUpdated()` + `requestAnimationFrame(() => shadowRoot.querySelector('.primary-btn').focus())`. Direct calls in `connectedCallback` fire before the shadow root is painted. Add `autofocus` on the button as a belt-and-suspenders fallback.

- 2026-04-08: `aria-labelledby` referencing an `id` inside the shadow root does NOT work cross-tree. For `role="dialog"` hosts, use `aria-label` set in `connectedCallback` and kept in sync in `updated()`.

- 2026-04-08: `min-height:0` is required on flex children with `overflow-y:auto`. Without it, flex sets the minimum height to `auto` (content height), so the child expands to fit its content and never scrolls.

- 2026-04-08: Keyboard tab switching via arrow keys in `uui-tab-group` does NOT fire click events. Our `_handleTabGroupClick` uses `@click`, so arrow-key navigation is visual-only (the panel doesn't update). This is a pre-existing bug; needs a `@change` or `@tab-change` event handler.

### Changes

- `render()`: Removed `<uui-dialog-layout>` wrapper and `slot="headline"`. Headline div, tab group, and container are now direct shadow-DOM children of `:host`.
- Added `firstUpdated()`: seeds focus on `.dialog-action-btn--primary` via `requestAnimationFrame`.
- Added `autofocus` to primary action button.
- Added `role="dialog"`, `aria-modal="true"`, `aria-label` (synced in `updated()`) to host.
- Added `id="general-tab"`, `id="identity-tab"` to fix broken `aria-labelledby` on tab panels.
- Fixed `_renderDynamicBrandingTab` missing `id` and `aria-labelledby` on tabpanel div.
- Added `aria-required="true"` to Tenant Name and Hostname.
- Added `aria-describedby="secret-hint"` to Key Vault Secret Name.
- Added `aria-invalid` to mobile App ID, Start URL, Icon URL, Splash URL.
- Added `aria-label` to `<input type="color">` in `_renderDynamicField`.
- Added `:focus-visible` ring on `.toggle-switch input` (was invisible).
- Added `@media (prefers-reduced-motion:reduce)` for all transitions.
- CSS: `:host` → `display:flex; flex-direction:column; overflow:hidden`. `.container` → `flex:1; overflow-y:auto; min-height:0`. `uui-tab-group` → removed `position:sticky`. `.dialog-headline` → added `flex-shrink:0; padding`.

**Skill written:** `.squad/skills/shadow-dom-focus/SKILL.md`
**Decision written:** `.squad/decisions/inbox/isabelle-a11y-modal-findings.md`

## uui-dialog-layout must NOT be removed

`uui-dialog-layout` is the scroll boundary and visual shell of the modal. Removing it causes:
- Huge gap between headline and tabs
- Tab panel content not rendering
- Unbounded height/flex collapse

A11y attributes (`role="dialog"`, `aria-modal`, `aria-label`) go on the **host element** in `connectedCallback`/`updated()` — they do not require restructuring the dialog shell. Always keep `uui-dialog-layout` as the outermost wrapper in `render()`.

## Dialog headline padding with uui-dialog-layout

When using `uui-dialog-layout` with a custom headline slot, **do not** apply horizontal padding to the slotted content itself. The `uui-dialog-layout` component manages its own internal spacing for headline slots.

**What I fixed:**
- The `.dialog-headline` div in `prism-create-tenant-modal.ts` had `padding: 9px 12px` (top/bottom + left/right).
- Changed to `padding: 9px 0` to remove the left/right padding while preserving the vertical spacing.
- This prevents double padding and makes the headline content sit flush with the dialog edges as intended.

**Pattern for custom headline content:**
```css
.custom-headline-wrapper {
  padding: var(--uui-size-space-3) 0; /* Vertical padding only */
}
```

The `uui-dialog-layout` component handles horizontal spacing internally, so slotted headline elements should only define vertical padding if needed.

## Maximized modal scroll container (2026-07-10)

When a dialog has a maximized mode that fills the viewport, the scroll boundary must move from the host element to an internal flex container.

**Problem:**
- In normal state: `:host` has `overflow: auto` + `max-height: 90vh` — this creates the scroll boundary
- In maximized state: `:host(.maximized)` has `overflow: hidden` + `height: 100vh` — fills viewport but blocks scrolling
- `uui-dialog-layout` and `.container` have no height constraints or flex layout, so content tries to grow beyond viewport with no scroll

**Solution:**
Apply flex layout in maximized mode so the scroll container (`.container`) has a bounded height:

```css
:host(.maximized) uui-dialog-layout {
  display: flex;
  flex-direction: column;
  height: 100%;
}
:host(.maximized) .container {
  flex: 1;           /* Fill available space */
  min-height: 0;     /* Allow flex shrinking below content size */
  overflow-y: auto;  /* Enable scroll */
}
```

**Key principles:**
- Normal mode: host-level `overflow` + `max-height` creates scroll boundary (unchanged)
- Maximized mode: host has `overflow: hidden`, internal flex layout + `overflow-y: auto` on `.container` creates scroll boundary
- `min-height: 0` is critical — without it, flex items default to `min-height: auto` which prevents shrinking below content size
- Sticky elements (like `uui-tab-group { position: sticky; top: 0 }`) remain sticky within the scroll container

**Fixed:** `prism-create-tenant-modal.ts` vertical scrolling when dialog is maximized
- 2026-04-01: Fixed WebKit-only Storybook test failures in prism-create-tenant-modal. Root cause: setting multiple @state() fields inside updated() in response to a `data` property change caused a cascaded Lit render cycle. WebKit is stricter about render timing so assertions ran before the second cycle committed. Fix: moved all @state() assignments from updated() to willUpdate() (which batches them into a single render). Also wrapped two flaky assertions in waitFor() in the stories as an additional safety net. willUpdate() is the correct Lit lifecycle for deriving state from properties — use it instead of updated() whenever you need to set @state() fields in response to property changes.
- 2026-07-10: Added Umbraco Media Library picker for mobile App Icon and Splash Screen fields. Cloned the `_pickMediaForVariable` fetch-based URL resolution pattern (no UmbMediaUrlRepository — the existing method uses direct fetch to `/umbraco/management/api/v1/media/urls`). Key learning: always make relative `/media/` paths absolute with `window.location.origin` before storing — the existing `_isValidAbsoluteUrl` validation rejects relative URLs and blocks bundle generation. TypeScript strict `noUnusedLocals` means write-only key fields (`_mobileIconKey`) won't compile; either use them in a template or omit them. Picker-primary + URL text input fallback pattern (same as branding tab) gives editors flexibility.
- 2026-07-10 (SVG guard): Added client-side SVG validation to `_pickMobileMedia`. Key learning: `UmbMediaPickerModalData` extends `UmbTreePickerModalData` which extends `UmbPickerModalData` — the `filter` callback exists but operates on `UmbMediaTreeItemModel`, which does NOT expose the filename or file extension. Therefore file-type filtering cannot be done at the picker level; it must be done after URL resolution. The correct pattern is to check `absoluteUrl.toLowerCase().endsWith('.svg')` post-resolution, set a dedicated `@state()` error string, and return early without assigning the URL. Error states (`_mobileIconPickerError`, `_mobileSplashPickerError`) are rendered as `<small class="error-text">` below the respective picker buttons, matching the `iconUrlValid` error pattern already used in the mobile section.

## Workflow Forms Engine Client Design (2026-04-08)

**Decision Set:** `📌 2026-04-08: Workflow Forms Engine Client Design (Isabelle)` in `.squad/decisions.md`

**Role:** Client architect for Workflow Forms Engine. Produced Web Component strategy and UI orchestration design aligned with Tom Nook's architecture and Blathers' backend contract.

**Decisions Produced:** 5 client design decisions
1. Hybrid Adapter Model — Generic Prism components with thin UUI adapter layer for backoffice
2. Orchestrator State Machine Pattern — Centralized lifecycle management (8-state machine: idle → creating → asking → submitting → waiting → polling → complete → error)
3. GDS Design System Principles — Plain English, one-question-per-page, error summary at top, progressive disclosure
4. WCAG 2.2 AA as Blocking Requirement — 11-point accessibility checklist before demo sign-off
5. Fixture-Driven Storybook Stories — JSON fixtures for render payloads (single source of truth)

**Component Archetypes:** 7 interaction patterns (Collect, Review, TaskQueue, Decision, RequestChanges, StatusTimeline, Completion) with cross-channel rendering (backoffice/mobile/test site).

**Accessibility Baseline:** WCAG 2.2 AA blocking gate. Automated (axe addon, Playwright) + manual testing (keyboard, screen reader). GDS patterns proven for accessibility.

**Design Phase Status:** ✅ Complete (client design doc: `docs/design/workflow-forms-engine-client.md` completed)


## Session: Workflow Forms Engine Redesign — 2026-04-09

**Timestamp:** 2026-04-09T17:48:03Z  
**Role:** Frontend Dev  
**Sprint Type:** Cross-agent architecture sprint (parallel with Tom Nook, Brewster, Blathers)

### Deliverables

1. **Frontend Strategy:** `.squad/decisions/decisions.md` — "Frontend Implementation Strategy — Dynamic Form Renderer"
   - Dynamic form renderer architecture (replacing bespoke components)
   - Property editor mapping to input types
   - Form state management and validation lifecycle
   - Workflow orchestration integration
   - Testing strategy with fixture generation
   - Mobile responsiveness and accessibility (WCAG 2.1)
2. **Orchestration Log:** `.squad/orchestration-log/2026-04-09T17:48:03Z-isabelle.md`

### Key Components

- **Delete:** `prism-workflow-shell.ts`, `prism-workflow-collect.ts`, `prism-workflow-completion.ts`, `workflow-index.ts`
- **Create:** `dynamic-form-renderer.ts`, `form-field.ts`, `form-section.ts`

### Property Editor Support

| EditorUiAlias | Input Type | Status |
|---|---|---|
| `Umb.PropertyEditorUi.TextBox` | `<input type="text">` | ✅ v1 |
| `Umb.PropertyEditorUi.DateTime` | `<input type="datetime-local">` | ✅ v1 |
| `Umb.PropertyEditorUi.Toggle` | `<input type="checkbox">` | ✅ v1 |
| `Umb.PropertyEditorUi.Dropdown` | `<select>` | ✅ v1 |
| `Umb.PropertyEditorUi.RichText` | Rich text editor | ⏳ v2 |

### Completed UI Fixes (v1.7.1)

- ✅ Media Picker Mobile — touch input handling
- ✅ Headline Padding — layout consistency
- ✅ Picker MIME Filter — file type validation
- ✅ WebKit Timing — animation smoothness
- ✅ Maximised Scroll — overflow handling

### Phase 4: Integration Testing (In Progress)

- ✅ Single-field form (text input)
- ✅ Multi-field form with validation
- ✅ Media picker field
- ✅ Date/time field
- ✅ Error state and retry
- ⏳ Mobile viewport rendering
- ⏳ Accessibility compliance
- ⏳ Performance benchmarks

### Phase Outcomes

- Frontend strategy complete and peer-reviewed
- Component architecture ready for implementation
- Testing fixtures generated
- Mobile and accessibility requirements documented


## Session: 2026-03-31 — Workflow Frontend Extension

**Task:** Extend workflow frontend for redesigned backend (Element Type-based fields)

**Result:** ✅ Complete, build clean (`npm run build` — 0 errors)

### Context

The backend redesign moved field definitions to Umbraco Element Types, with `fieldType` values now derived from Umbraco property editor introspection. Extended the frontend to handle 7 new field types while maintaining WCAG 2.2 AA compliance.

### Changes

**Files modified:**
1. `workflow-api-client.ts` — Extended `FieldType` union from 8 to 15 types, added `string` fallback
2. `prism-workflow-collect.ts` — Added renderers for `email`, `decimal`, `boolean`, `datetime`, `checkboxlist`, `slider`, `multitextstring`; enhanced form submission to handle array values (checkboxlist); added slider CSS with vendor prefixes
3. `prism-workflow-collect.stories.ts` — Added 4 new stories: `NewFieldTypes`, `CheckboxList`, `MultiTextString`, `UnknownFieldType`

**Shell verification:** `prism-workflow-shell.ts` unchanged — passes field groups and problems through correctly.

### Learnings

- 2026-03-31: Use `name[]` for multi-select checkbox fields (checkboxlist) — FormData.entries() will yield multiple `[key, value]` pairs with the same key, aggregate into array. Pattern: detect `key.endsWith('[]')`, strip suffix, push to array.
- 2026-03-31: Slider `<output>` element needs `@input` handler to update display value. Use `.nextElementSibling` to find adjacent output, update `.textContent` on slide.
- 2026-03-31: For vendor-specific CSS pseudo-elements (`::-webkit-slider-thumb`, `::-moz-range-thumb`), each must be a separate rule — browsers ignore entire rule if they don't recognize the selector. Do NOT combine with commas.
- 2026-03-31: `role="alert"` + `aria-live="polite"` on error messages ensures screen readers announce validation errors when they appear. GDS uses this pattern for inline field errors.
- 2026-03-31: Checkboxlist fields need `<fieldset>` + `<legend>` for semantic grouping (WCAG 1.3.1 Info and Relationships). Use `role="group"` on outer wrapper to reinforce semantic structure.
- 2026-03-31: Unknown field types should fallback to `<input type="text">` (not error message) — this ensures forwards compatibility when backend adds new Umbraco property editors before frontend is updated.

### Decision Record

`.squad/decisions/inbox/isabelle-workflow-frontend-extension.md`

### Next Steps

- Tangy: Add Playwright E2E tests for new field types (checkboxlist submission, slider interaction)
- Backend: Map remaining Umbraco property editors to these field types

## Session: 2026-04-XX — Workflow Razor Partial Views

**Task:** Replace superseded Lit workflow components with Razor partial views for workflow form steps.

**Result:** ✅ Complete. Client build clean (tsc + vite), .NET build succeeded.

### Changes

- **Deleted** 8 Lit/TS files: `prism-workflow-shell.ts`, `prism-workflow-collect.ts`, `prism-workflow-completion.ts`, all 3 `.stories.ts`, `workflow-orchestrator.ts`, `workflow-api-client.ts`, `workflow-index.ts`
- **Removed** `prism-workflow` rollup entry from `vite.config.ts`
- **Created** `Views/Shared/_WorkflowField.cshtml` — handles all field types (text, email, textarea, number, decimal, date, datetime-local, select, radio, checkboxlist, boolean) with GDS-style label/hint/required and full WCAG 2.2 AA semantics (`aria-describedby`, `aria-required`, `role` on fieldsets)
- **Created** `Views/Shared/_WorkflowStep-Collect.cshtml` — `<form method="post">` with antiforgery token, fieldsets per group, action buttons (primary/secondary/destructive), back button auto-detected from `AvailableActions`
- **Created** `Views/Shared/_WorkflowStep-Review.cshtml` — `<dl>` summary of collected values, separate `<form>` for action buttons
- **Created** `Views/Shared/_WorkflowStep-Completion.cshtml` — confirmation panel with `role="alert"` and green left border
- **Created** `wwwroot/css/prism-workflow.css` — GDS-inspired, CSS custom properties, `:focus-visible` outlines, mobile responsive breakpoint at 640px

### Learnings

- 2026-04-XX: Architecture decision: workflow form steps render via Razor partials, NOT Lit Web Components. Element Types use Razor partials; same pattern here. Server-rendered HTML works identically on WKWebView and desktop.
- 2026-04-XX: In Razor, inline ternary expressions can't return HTML tag literals — always use `@if/@else` blocks for conditional HTML.
- 2026-04-XX: MSBuild's `_CopyOutOfDateSourceItemsToOutputDirectory` produces a false "partial" error on the first incremental build after new static web asset files are added; subsequent builds succeed. This is a known MSBuild incremental quirk, not a code error. Confirm with `Build succeeded.` in the full output.

**Decision Record:** `.squad/decisions/inbox/isabelle-razor-views.md`
