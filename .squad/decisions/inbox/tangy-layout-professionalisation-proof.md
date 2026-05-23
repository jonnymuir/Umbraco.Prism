---
author: tangy
date: 2026-05-23T08:30:10.563+01:00
status: behavioral-proof-landed
area: workflow-editor-ux
---

# Decision: Layout Professionalisation Behavioral Proof

## Context

User directive (2026-05-23): "Clean this up... make reference host as easy to use as possible... the actual editing surface itself is very small... don't put the tabs under the editor, have the editor as a tab itself."

The current shell (`prism-workflow-editor-shell.ts`) foregrounds bulky explanatory chrome:
- Large hero section with marketing copy
- Launch card with form controls for API base
- Integration rail with "Why this host stays thin" guidance
- Section headings and kickers around the editor
- Editor frame occupies ~60% of viewport (target: ≥80%)

This makes the reference host feel like a sales page with an embedded demo, not a usable editor.

## Decision

I've landed the behavioral proof for layout professionalization in `tests/workflow-editor/layout-professionalization.spec.ts`.

### Five Proof Dimensions

#### 1. Host chrome minimization
- Hero section should occupy ≤15% of viewport (currently ~20-30%)
- Explanatory prose should be removed (moved to docs)
- Integration rail/sidebar should not be visible by default

#### 2. Simplified launch flow
- Authoring API base should not be a mainline control (settings/badge/hidden)
- Launch card should collapse to compact workflow switcher or remove entirely
- Workflow selection should feel like tab-switching, not app-launching

#### 3. Editor surface prioritization
- Editor should occupy ≥80% of viewport height (currently ~60%)
- Editor should be the page, not a section within chrome
- Section headings/kickers about the editor should be removed

#### 4. Keyboard and screen reader accessibility
- Skip link remains functional
- Tab order: utility controls → editor (within 5 tabs)
- Existing keyboard shortcuts preserved ('e', 'v', undo/redo)
- Main landmark contains editor directly (not nested sections)

#### 5. Editor functionality preservation
- Outline navigation remains accessible
- Graph/list toggle functional
- Stage selection and inspector functional
- Confidence tabs (validation/preview/simulation) accessible
- Role-first swim lanes visible and semantic

### Semantic Hooks for Isabelle

**Critical (must implement):**
- `.hero` max-height constraint or removal
- Remove explanatory prose blocks
- Hide `.integration-rail` by default
- Remove/hide `input[id="authoring-api-base"]`
- Remove `.launch-card` or collapse to compact switcher
- Remove `.section-kicker`, `.section-note`, section headings
- `.editor-frame` should occupy ≥80% viewport height

**Optional (if workflow selection remains visible):**
- `[data-prism-workflow-selector]` — compact switcher semantic hook

**Already present (verify still functional):**
- Skip link → `#workflow-editor-reference-main`
- `[data-prism-workflow-outline]` — outline navigation
- `[data-prism-confidence-tabs]` — tabbed surfaces
- `[data-prism-stage]` — stage cards
- Graph/list toggle buttons

### Test Status

**Expected until Isabelle's implementation:**
- All tests in `layout-professionalization.spec.ts` will FAIL
- Existing tests (`workflow-editor-shell.spec.ts`, planning walkthrough) remain GREEN

**After Isabelle's implementation:**
- All layout tests should pass
- Existing editor functionality tests should remain GREEN
- Run full validation gate (5 commands)

### Validation Commands

```bash
# 1. Build
cd src/UmbracoPrism.Client && npm run build

# 2. Storybook accessibility
npm run test-storybook:ci:all

# 3. Existing keyboard tests
npx playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line

# 4. Planning walkthrough
npm run test:playwright:planning-smoke

# 5. NEW: Layout professionalization proof
npx playwright test tests/workflow-editor/layout-professionalization.spec.ts --reporter=line
```

## Working in Parallel

Isabelle is implementing the shell refactor. This proof:
- Documents exact behavioral expectations inline with test comments
- Provides concrete measurement thresholds (viewport ratios, tab counts)
- Preserves existing functionality verification
- Runs independently without blocking her implementation

## Alternatives Considered

### Update existing shell spec instead of new file
- **Rejected:** `workflow-editor-shell.spec.ts` tests the mature editor improvements (outline, tabs, selection sync). Layout professionalization is a separate concern with distinct acceptance criteria. Separate specs maintain clear behavioral boundaries.

### Wait for Isabelle's implementation before writing tests
- **Rejected:** Tests-first approach documents expectations precisely and prevents scope drift. Isabelle gets clear acceptance criteria before coding.

### Test only viewport ratios (skip semantic hooks)
- **Rejected:** Viewport ratios prove space allocation, but semantic hooks prove the chrome removal. Both dimensions needed for complete behavioral proof.

## References

- Directive: `.squad/decisions/inbox/copilot-directive-2026-05-23T08-30-10.md`
- Implementation file: `src/workflow-editor/prism-workflow-editor-shell.ts`
- Existing shell proof: `tests/workflow-editor/workflow-editor-shell.spec.ts`
- Planning walkthrough: `tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`
