# Orchestration Log: Isabelle — White Theme Mobile Nav Defaults

**Timestamp:** 2026-04-02T23:26:51Z

## Spawn Summary

- **Commit:** `37e9975` — feat(mobile-nav): update defaults to Apple iOS white style
- **Branch:** main (committed directly per solo-workflow directive)
- **Artifacts:**
  - `src/UmbracoPrism.Client/src/components/prism-mobile-nav.ts`
  - `src/UmbracoPrism.Client/src/components/prism-mobile-nav.stories.ts`
  - `src/UmbracoPrism.TestSite/wwwroot/css/prism-components.css`
- **Description:** Updated `prism-mobile-nav` component defaults from dark glass (navy `rgba(15,23,42,0.94)`) to Apple iOS-inspired white frosted glass (`rgba(255,255,255,0.95)`). Updated component CSS custom property fallbacks, Storybook decorator, renamed LightTheme story to DarkTheme, and documented white nav vars in TestSite branding CSS. Build passed.

## Decision Merged

- **File:** `.squad/decisions/inbox/isabelle-white-nav.md`
- **Title:** prism-mobile-nav defaults to Apple iOS white style
- **Status:** Proposed → Merged

### Key Points
- Component defaults changed to iOS palette: white background, iOS blue active color (`#007aff`), reduced label weight (500 → 600)
- Storybook `mobileDecorator` background changed to `#f2f2f7` (iOS system background)
- `LightTheme` story renamed to `DarkTheme` with explicit dark glass overrides
- White nav CSS variables documented in TestSite branding for tenant discoverability
- Dark glass style fully supported via CSS custom property overrides
- Visually breaking change — tenants relying on dark defaults must add explicit CSS variable overrides

## History Updated

- `.squad/agents/isabelle/history.md` appended with spawn and decision summary
