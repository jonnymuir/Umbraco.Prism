import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('Workflow graph behavioral tests', () => {
  test('graph workspace renders role-based swim lanes with stages and transitions', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    const storyEl = page.locator('prism-workflow-graph');
    await expect(storyEl).toBeVisible({ timeout: 10_000 });
    await page.waitForLoadState('networkidle');
    await storyEl.evaluate(async element => {
      await (element as { updateComplete?: Promise<unknown> }).updateComplete;
    });

    // Verify role lanes are rendered (swim lanes for different actors)
    const lanes = storyEl.locator('[data-prism-role-lane]');
    await expect(lanes.first()).toBeVisible();
    const laneCount = await lanes.count();
    expect(laneCount).toBeGreaterThan(0);

    // Verify stages are rendered as nodes in the graph
    const stages = storyEl.locator('[data-prism-stage]');
    await expect(stages.first()).toBeVisible();
    const stageCount = await stages.count();
    expect(stageCount).toBeGreaterThan(0);

    // Verify transitions are rendered as paths between stages
    const transitions = storyEl.locator('[data-prism-transition]');
    const transitionCount = await transitions.count();
    expect(transitionCount).toBeGreaterThan(0);

    // Verify lane headers show role labels
    const laneHeaders = storyEl.locator('.lane-header');
    await expect(laneHeaders.first()).toBeVisible();

    // Verify the graph canvas is scrollable for overflow
    const graphCanvas = storyEl.locator('.graph-canvas');
    await expect(graphCanvas).toBeVisible();
  });

  test('list mode displays stages in editable table with filtering', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    const storyEl = page.locator('prism-workflow-graph');
    await expect(storyEl).toBeVisible({ timeout: 10_000 });
    await page.waitForLoadState('networkidle');
    await storyEl.evaluate(async element => {
      await (element as { updateComplete?: Promise<unknown> }).updateComplete;
    });

    // Switch to list mode
    await page.getByRole('button', { name: 'List view' }).click();
    await expect(page.getByRole('region', { name: /workflow stages/i })).toBeVisible({ timeout: 5_000 });
    await storyEl.evaluate(async element => {
      await (element as { updateComplete?: Promise<unknown> }).updateComplete;
    });

    // Verify table structure
    await expect(storyEl.locator('[data-prism-linear-table]')).toBeVisible();
    
    // Verify stage rows are rendered
    const rows = storyEl.locator('[data-prism-list-row]');
    await expect(rows.first()).toBeVisible();
    const rowCount = await rows.count();
    expect(rowCount).toBeGreaterThan(0);

    // Verify inline editing fields are present
    await expect(storyEl.locator('[data-prism-inline-field]').first()).toBeVisible();

    // Verify lane filtering options exist
    await expect(page.getByRole('button', { name: 'All stages' })).toBeVisible();
    const filterCount = await storyEl.locator('[data-prism-linear-filter]').count();
    expect(filterCount).toBeGreaterThan(1);

    // Verify action buttons are present (move, delete, insert)
    await expect(storyEl.locator('[data-prism-move-up]').first()).toBeVisible();
    await expect(storyEl.locator('[data-prism-move-down]').first()).toBeVisible();
    await expect(storyEl.locator('[data-prism-insert-before]').first()).toBeVisible();
    await expect(storyEl.locator('[data-prism-insert-after]').first()).toBeVisible();
    await expect(storyEl.locator('[data-prism-delete-stage]').first()).toBeVisible();
  });
});
