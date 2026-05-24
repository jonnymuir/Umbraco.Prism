# Decision: CI Test Contract Alignment (2026-05-24)

**Status:** Implemented  
**Context:** CI red on main - walkthrough heading mismatch + visual regression failures  
**Author:** Tangy (Tester)

---

## Problem

Two classes of CI failure on main:

1. **Walkthrough spec stale heading** — Test expected `/compose the editor into your app/i` but component now has `<h1>Workflow Editor</h1>`
2. **Visual regression platform drift** — macOS baselines vs Linux CI rendering (1732px diff, 0.24% of image)

---

## Root Causes

### 1. Stale heading assertion

The workflow editor shell simplified its heading from a long tagline to just "Workflow Editor" at some point, but:
- The walkthrough test kept asserting the old heading
- The walkthrough docs still documented the old heading

This violates the "Walkthroughs Are Executable Specs" skill — test and docs must stay in lockstep with the component.

### 2. Cross-platform visual rendering

The visual regression tests load deterministic fonts (Inter as base64) and disable font smoothing/subpixel positioning, but minor rendering differences between macOS and Linux still exceed the `maxDiffPixels: 80` threshold:
- macOS baselines: 1280×560 screenshots
- Linux CI: 1732 pixels different (~0.24% of image)
- Font hinting, kerning, and anti-aliasing vary by platform even with the same font data

---

## Decision

### 1. Align walkthrough test contract to reality

**Changed:**
- `planning-workflow-complete.walkthrough.spec.ts` — heading assertions now `/workflow editor/i` (3 occurrences)
- `docs/walkthroughs/planning-workflow-complete.md` — shell heading now "Workflow Editor"
- `docs/walkthroughs/planning-workflow-editor.md` — shell heading now "Workflow Editor"

**Rationale:** Tests assert behaviour, not stale marketing copy. The heading is a semantic landmark for navigation, not a product tagline. The simplified heading matches the component and improves accessibility.

### 2. Platform-specific visual baselines

**Changed:**
- `playwright.config.ts` — screenshot path template now includes `{platform}` segment
- Moved existing baselines to `tests/__screenshots__/darwin/workflow-editor/workflow-graph-visual.spec.ts/`
- CI will generate Linux baselines on first run post-merge

**Rationale:**
- Cross-platform pixel-perfect rendering is not achievable even with deterministic fonts
- Playwright officially supports platform-specific baselines via `{platform}` in pathTemplate
- This approach is more maintainable than constantly tuning `maxDiffPixels` thresholds
- Visual tests remain valuable for catching layout regressions within each platform

**Trade-off:** Requires maintaining separate baseline sets per platform. Accepted because:
1. The alternative (no visual regression tests) loses layout regression coverage
2. Increasing `maxDiffPixels` to 2000+ risks masking real regressions
3. The deterministic font setup already minimizes drift; remaining differences are platform-inherent

---

## How to Generate Linux Baselines

If CI fails with "snapshot not found" after this change:

1. **Local (if you have Linux/Docker):**
   ```bash
   cd src/UmbracoPrism.Client
   docker run --rm -v $(pwd):/work -w /work mcr.microsoft.com/playwright:v1.49.1-noble \
     /bin/bash -c "npm ci && npx playwright test tests/workflow-editor/workflow-graph-visual.spec.ts --update-snapshots"
   ```

2. **CI update mode (recommended):**
   - Add `--update-snapshots` flag to the visual test CI step temporarily
   - Run CI, let it generate Linux baselines
   - Commit the new `tests/__screenshots__/linux/` directory
   - Remove `--update-snapshots` flag

3. **Validate both platforms:**
   ```bash
   # macOS
   npm run test:playwright:workflow-graph-visual

   # Linux (in CI or Docker)
   playwright test tests/workflow-editor/workflow-graph-visual.spec.ts
   ```

---

## Validation

✅ **Client build** — GREEN  
✅ **Visual tests (macOS)** — 2 passed (with platform-specific baselines)  
⏳ **Visual tests (Linux)** — baselines will be generated in next CI run  
⏳ **Walkthrough smoke test** — will validate heading fix in next CI run

---

## Lessons

1. **Behavioural contract discipline** — When component text changes, update tests AND docs in the same commit (per `.copilot/skills/test-discipline/SKILL.md`)
2. **Visual regression platform reality** — Cross-platform pixel-perfect rendering is a false promise; use platform-specific baselines from day one
3. **Quality gate design** — Visual tests guard layout, not rendering; keep thresholds tight within-platform rather than loose cross-platform

---

## Files Changed

- `src/UmbracoPrism.Client/tests/walkthroughs/planning-workflow-complete.walkthrough.spec.ts`
- `src/UmbracoPrism.Client/playwright.config.ts`
- `docs/walkthroughs/planning-workflow-complete.md`
- `docs/walkthroughs/planning-workflow-editor.md`
- `src/UmbracoPrism.Client/tests/__screenshots__/` (restructured by platform)
