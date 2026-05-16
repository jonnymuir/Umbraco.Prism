/**
 * Workflow graph — keyboard navigation contract
 *
 * These tests run against Storybook (baseURL: http://127.0.0.1:6006) and assert
 * the keyboard accessibility contracts documented in:
 *   docs/design/workflow-editor-v1/01-authoring-ux.md §2.1 (keyboard shortcuts)
 *   docs/design/workflow-editor-v1/01-authoring-ux.md §2.3 (dual-mode, WCAG 2.1.1)
 *
 * Selector contract (from 01-authoring-ux.md §10 Test Hooks):
 *   data-testid="workflow-graph"     → <prism-workflow-graph> root
 *   data-testid="linear-list"        → <prism-linear-list> root (stage-list mode)
 *   data-testid="toolbar-list-view"  → mode toggle button (aria-pressed reflects state)
 *   data-node-id="{id}"             → individual stage node (in graph mode)
 *
 * Tests that depend on hooks not yet implemented by Isabelle are skipped with
 * a clear TODO comment. Do not invent selectors — wait for Isabelle's hooks.
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
      // TODO: Replace story ID once Isabelle publishes the linear-list story.
      // Expected story: 'workflow-editor-workflow-graph--linear-mode'
      // The story must render <prism-linear-list data-testid="linear-list"> with
      // at least two stage rows, each focusable and navigable via ArrowUp/ArrowDown.
      //
      // Until the story exists, this test is skipped at the navigation step.

      // TODO: unskip when Isabelle's linear-mode graph story is available
      test.skip(
        true,
        'TODO: Awaiting Isabelle\'s workflow-graph linear-mode Storybook story (workflow-editor-workflow-graph--linear-mode)'
      );

      await page.goto(storyUrl('workflow-editor-workflow-graph--linear-mode'));

      const linearList = page.locator('[data-testid="linear-list"]');
      await expect(linearList).toBeVisible();

      // The list must be keyboard-operable: Tab into the first row, then Arrow to navigate.
      // WCAG 2.1.1 — all functionality available from keyboard.
      const rows = linearList.getByRole('row');
      const firstRow = rows.first();
      const secondRow = rows.nth(1);

      await firstRow.focus();
      await expect(firstRow).toBeFocused();

      // ArrowDown moves focus to the next stage
      await page.keyboard.press('ArrowDown');
      await expect(secondRow).toBeFocused();

      // ArrowUp returns focus to the previous stage
      await page.keyboard.press('ArrowUp');
      await expect(firstRow).toBeFocused();

      // Screen-reader announcement region must update on focus change
      // (data-testid="graph-announcer" per Isabelle's hook contract)
      const announcer = page.locator('[data-testid="graph-announcer"]');
      if (await announcer.count() > 0) {
        await expect(announcer).not.toBeEmpty();
      }
      // If the announcer hook is not yet present, we skip the SR assertion silently —
      // Isabelle will add it when the linear-list component ships.
    }
  );

  // ---------------------------------------------------------------------------
  // 2. Mode toggle — keyboard accessible, aria-pressed reflects state
  // ---------------------------------------------------------------------------

  test(
    'Mode toggle is keyboard accessible and aria-pressed reflects state',
    async ({ page }) => {
      // TODO: Replace story ID once Isabelle publishes the dual-mode graph story.
      // Expected story: 'workflow-editor-workflow-graph--default'
      // The story must render both:
      //   - <prism-workflow-graph data-testid="workflow-graph"> (visual mode, default)
      //   - a toggle button: data-testid="toolbar-list-view" with aria-pressed="false"
      // Pressing the toggle must:
      //   - Switch to <prism-linear-list data-testid="linear-list">
      //   - Set aria-pressed="true" on the toggle button
      //
      // This implements WCAG criterion 2.1.1 Keyboard + 4.1.2 Name, Role, Value.

      // TODO: unskip when Isabelle's dual-mode graph story is available
      test.skip(
        true,
        'TODO: Awaiting Isabelle\'s dual-mode workflow-graph Storybook story (workflow-editor-workflow-graph--default)'
      );

      await page.goto(storyUrl('workflow-editor-workflow-graph--default'));

      // Graph starts in visual mode
      const graph = page.locator('[data-testid="workflow-graph"]');
      await expect(graph).toBeVisible();

      const toggleButton = page.locator('[data-testid="toolbar-list-view"]');
      await expect(toggleButton).toBeVisible();
      await expect(toggleButton).toHaveAttribute('aria-pressed', 'false');

      // Toggle must be reachable and activatable via keyboard alone
      await toggleButton.focus();
      await expect(toggleButton).toBeFocused();
      await page.keyboard.press('Enter');

      // After toggle: linear-list becomes visible, graph canvas hides
      const linearList = page.locator('[data-testid="linear-list"]');
      await expect(linearList).toBeVisible();
      await expect(graph).toBeHidden();

      // aria-pressed must reflect the new state
      await expect(toggleButton).toHaveAttribute('aria-pressed', 'true');

      // Toggle back — graph returns
      await page.keyboard.press('Enter');
      await expect(graph).toBeVisible();
      await expect(linearList).toBeHidden();
      await expect(toggleButton).toHaveAttribute('aria-pressed', 'false');
    }
  );
});
