---
name: "workflow-editor-role-first-swim-lanes-testing"
description: "Testing pattern for role-first swim lane behavioral contracts in workflow editor"
domain: "testing"
confidence: "high"
source: "observed (2026-05-22T19:33:56.538+01:00 issue #74 validation)"
---

## Context

When validating workflow editor work that uses role-first swim lanes (horizontal lanes per role, not generic node fields), the behavioral contract must prove the swim-lane structure is semantic and keyboard-accessible, not just visual styling.

## Patterns

### Structural Swim Lane Tests

**Test that lanes are semantic sections, not just visual divs:**

```typescript
// ✓ Correct: verify lanes render as focusable sections with semantic labels
const lanes = page.locator('[data-prism-role-lane]');
await expect(lanes).not.toHaveCount(0);

const firstLane = lanes.first();
await expect(firstLane.locator('.lane-heading')).toBeVisible();
await expect(firstLane.locator('.lane-copy')).toBeVisible();

// Lanes should be keyboard-focusable
await firstLane.focus();
await expect(firstLane).toBeFocused();

// Lane headings should convey the role label, not just styling
const headingText = await firstLane.locator('.lane-heading').textContent();
expect(headingText?.trim().length).toBeGreaterThan(0);
```

**Verify the workspace is described as "role-first" for screen readers:**

```typescript
const canvas = page.getByRole('application');
await expect(canvas).toHaveAttribute('aria-roledescription', /role-first/i);
```

### Front-Stage / Back-Stage Distinction

**Test that front/back-stage lanes are structurally distinct:**

```typescript
// Front-stage and back-stage lanes should both exist (for workflows with both)
const frontStageLanes = page.locator('[data-prism-role-lane].lane-primary');
const backStageLanes = page.locator('[data-prism-role-lane].lane-supporting');

await expect(frontStageLanes).not.toHaveCount(0);
// Back-stage lanes may be 0 if the workflow is front-stage only
```

### Keyboard Navigation Across Lanes

**Test that keyboard users can move between lanes and stages:**

```typescript
// Start by focusing the first lane
const firstLane = page.locator('[data-prism-role-lane]').first();
await firstLane.focus();

// Tab should move focus from lane to a stage within that lane
await page.keyboard.press('Tab');
const firstStage = page.locator('[data-prism-stage]').first();

// Verify we can select a stage with Enter
await firstStage.press('Enter');
await expect(firstStage).toHaveAttribute('aria-pressed', 'true');

// The 'e' key should open the inspector (as documented in the hint)
await firstStage.press('e');
```

### Walkthrough Updates

**When updating walkthrough tests for swim lanes:**

```typescript
// ✓ Correct: explicitly check for role-first orientation
const graphCanvas = page.getByRole('application');
await expect(graphCanvas).toHaveAttribute('aria-roledescription', /role-first/i);

// ✓ Correct: verify role lanes are structurally present
await expect(page.locator('[data-prism-role-lane]')).not.toHaveCount(0);

// ✗ Incorrect: only checking that stages are visible (doesn't prove swim lanes)
await expect(graphCanvas.getByText('Declaration')).toBeVisible();
```

## Examples

- `tests/workflow-editor/workflow-graph-keyboard.spec.ts` — role lanes tests
- `tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts` — walkthrough swim lane validation

## Quality Gate Commands

```bash
cd src/UmbracoPrism.Client && npm run build
cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line
cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-visual.spec.ts --reporter=line
cd src/UmbracoPrism.Client && npm run test-storybook:ci:all
```

## Anti-Patterns

- **Testing only that stages are visible** — doesn't prove swim lanes are semantic
- **Checking color alone** — role distinction must work without color (WCAG 2.2 AA)
- **Pointer-only tests** — must prove keyboard users can navigate across lanes
- **Testing aria labels in isolation** — must verify the structural HTML (sections, headings)
- **Assuming lanes are focusable** — must explicitly test focus behavior

## Why This Pattern Works

- **Semantic structure** — Lanes are sections with headings, not just styled divs
- **Keyboard parity** — Tab, Enter, and shortcuts work across lanes
- **Screen reader support** — aria-roledescription and structural labels convey intent
- **Visual independence** — Tests don't rely on color or positioning alone
- **Behavioral focus** — Tests prove what users experience, not implementation details
