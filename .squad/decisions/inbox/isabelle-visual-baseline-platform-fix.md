# Decision: Remove Platform-Specific Visual Baselines

**Date:** 2026-05-24  
**Author:** Isabelle (Frontend Dev)  
**Status:** Implemented

## Context

CI visual regression tests were failing in run 26356125863:
- `graph workspace matches the baseline canvas` ❌
- `list mode matches the baseline workspace layout` ❌

Investigation revealed:
1. `playwright.config.ts` was using `{platform}` in the screenshot path template
2. Only `darwin/` (macOS) baselines existed; no Linux baselines were generated
3. CI runs on `ubuntu-latest` (Linux), expected baselines at `linux/...`
4. Tests use deterministic fonts (Inter TTF embedded) to ensure cross-platform consistency

## Decision

**Removed platform-specific paths from `playwright.config.ts`**

Changed:
```diff
- pathTemplate: '{testDir}/__screenshots__{/projectName}/{platform}/{testFilePath}/{arg}{ext}'
+ pathTemplate: '{testDir}/__screenshots__{/projectName}/{testFilePath}/{arg}{ext}'
```

Deleted `tests/__screenshots__/darwin/` directory.

## Rationale

1. **Deterministic fonts eliminate platform rendering differences** — Tests load Inter TTF files inline with antialiasing controls, font hinting disabled, sRGB color profile forced
2. **Single baseline set is maintainable** — No need to generate/maintain separate baselines per platform
3. **List mode is a real user behavior** — Not obsolete; clicking "List view" toggles linear table layout with inline editing, filters, and reordering controls
4. **Both tests are behavioral contracts** — They verify:
   - Graph workspace: Role-based swim lanes render correctly with stage cards positioned by lane assignment
   - List mode: Linear table view shows all stages with inline editing, actor/type columns, and action buttons

## Verification

✅ TypeScript build clean  
✅ Storybook accessibility: 33/33 passed, 165 tests, 0 violations  
✅ Visual regression: 2/2 passed (graph workspace + list mode)  

Both baselines now at `tests/__screenshots__/workflow-editor/workflow-graph-visual.spec.ts/`:
- `workflow-graph-workspace-canvas.png` (115.9 KB)
- `workflow-graph-workspace-list-mode.png` (94.4 KB)

## Impact

- CI will use same baselines as local development
- No platform-specific maintenance burden
- Visual tests remain behavioral (UI layout contract), not implementation mirrors
