/**
 * Workflow graph — keyboard navigation contract
 *
 * These tests run against Storybook (baseURL: http://127.0.0.1:6006) and assert
 * the keyboard accessibility contracts documented in:
 *   docs/design/workflow-editor-v1/01-authoring-ux.md §2.1 (keyboard shortcuts)
 *   docs/design/workflow-editor-v1/01-authoring-ux.md §2.3 (dual-mode, WCAG 2.1.1)
 *
 * Actual component hooks shipped by Isabelle (data-prism-* attributes):
 *   data-prism-component="workflow-graph"   → inner root div (shadow DOM)
 *   data-prism-mode="graph|linear"          → reflects current view mode
 *   data-prism-stage="{stageKey}"           → individual stage node/card
 *
 * Role-based selectors are used where possible — they pierce shadow DOM and are
 * more resilient than attribute selectors. WCAG 2.1.1 requires all functionality
 * to be available from the keyboard; these tests assert that contract.
 *
 * Story IDs (verified against src/UmbracoPrism.Client/src/workflow-editor/*.stories.ts):
 *   workflow-editor-workflow-graph--populated-workflow → graph mode, STUB_WORKFLOW loaded
 */

import { test, expect } from '@playwright/test';

/** Storybook iframe URL for a given story. */
function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

// ---------------------------------------------------------------------------
// 1. Stage-list (linear) mode keyboard navigation
// ---------------------------------------------------------------------------

test.describe('Workflow graph keyboard navigation', () => {
  test(
    'Stage list mode supports arrow-key navigation',
    async ({ page }) => {
      // Use PopulatedWorkflow story — STUB_WORKFLOW loaded, starts in graph mode.
      // Story ID verified from prism-workflow-graph.stories.ts (PopulatedWorkflow export).
      await page.goto(storyUrl('workflow-editor-workflow-graph--populated-workflow'));

      // Wait for the custom element to be defined and rendered
      const storyEl = page.locator('prism-workflow-graph');
      await expect(storyEl).toBeVisible({ timeout: 10_000 });

      // Switch to linear mode via the mode-toggle button.
      // getByRole() pierces shadow DOM — Playwright's aria tree traverses shadow boundaries.
      const toggleBtn = page.getByRole('button', { name: 'List view' });
      await expect(toggleBtn).toBeVisible({ timeout: 5_000 });
      await toggleBtn.click();

      // Linear list is now visible (role="listbox" inside shadow DOM)
      const listbox = page.getByRole('listbox');
      await expect(listbox).toBeVisible({ timeout: 5_000 });

      // Each stage card has role="option" (inside shadow DOM)
      // WCAG 2.1.1 — Tab into the first row, then Arrow to navigate.
      const firstOption = page.getByRole('option').first();
      const secondOption = page.getByRole('option').nth(1);

      await firstOption.focus();
      await expect(firstOption).toBeFocused();

      // ArrowDown moves focus to the next stage
      await page.keyboard.press('ArrowDown');
      await expect(secondOption).toBeFocused();

      // ArrowUp returns focus to the previous stage
      await page.keyboard.press('ArrowUp');
      await expect(firstOption).toBeFocused();

      // Pressing Enter on a stage fires stage-selected and updates the SR announcer.
      // The announcer has role="status" (aria-live="polite") in shadow DOM.
      await page.keyboard.press('Enter');
      const announcer = page.getByRole('status');
      if (await announcer.count() > 0) {
        await expect(announcer).not.toBeEmpty({ timeout: 2_000 });
      }
    }
  );

  // ---------------------------------------------------------------------------
  // 2. Mode toggle — keyboard accessible, aria-pressed reflects state
  // ---------------------------------------------------------------------------

  test(
    'Mode toggle is keyboard accessible and aria-pressed reflects state',
    async ({ page }) => {
      // Use PopulatedWorkflow story — STUB_WORKFLOW loaded, starts in graph mode.
      // This story does not change mode in its play() function, giving us a clean
      // initial state: graph canvas visible, aria-pressed="false" on toggle button.
      await page.goto(storyUrl('workflow-editor-workflow-graph--populated-workflow'));

      // Wait for the custom element to render
      await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });

      // Graph starts in visual mode — the canvas has role="application" (shadow DOM)
      const graphCanvas = page.getByRole('application');
      await expect(graphCanvas).toBeVisible({ timeout: 5_000 });

      // Toggle button: starts with aria-pressed="false" and label "List view"
      // (label switches to "Graph view" when in linear mode)
      const toggleButton = page.getByRole('button', { name: 'List view' });
      await expect(toggleButton).toBeVisible({ timeout: 5_000 });
      await expect(toggleButton).toHaveAttribute('aria-pressed', 'false');

      // Toggle must be reachable and activatable via keyboard alone (WCAG 2.1.1)
      await toggleButton.focus();
      await expect(toggleButton).toBeFocused();
      await page.keyboard.press('Enter');

      // After toggle: linear-list (listbox) becomes visible, graph canvas removed from DOM
      const linearList = page.getByRole('listbox');
      await expect(linearList).toBeVisible({ timeout: 5_000 });
      await expect(graphCanvas).toHaveCount(0);

      // aria-pressed="true" and button label is now "Graph view" (4.1.2 Name, Role, Value)
      const toggleButtonInLinear = page.getByRole('button', { name: 'Graph view' });
      await expect(toggleButtonInLinear).toHaveAttribute('aria-pressed', 'true');

      // Toggle back via keyboard — graph canvas returns, listbox removed
      await toggleButtonInLinear.focus();
      await page.keyboard.press('Enter');
      await expect(graphCanvas).toBeVisible({ timeout: 5_000 });
      await expect(linearList).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'List view' })).toHaveAttribute('aria-pressed', 'false');
    }
  );
});
