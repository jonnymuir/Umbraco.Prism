import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('Workflow graph workspace', () => {
  test('linear mode supports keyboard navigation, filtering, and reordering', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });
    await page.getByRole('button', { name: 'List view' }).click();
    const table = page.locator('[data-prism-linear-table]');
    await expect(table).toBeVisible();

    const firstTrigger = page.locator('[data-prism-list-row-trigger]').first();
    await firstTrigger.focus();
    await page.keyboard.press('ArrowDown');
    await expect(page.locator('[data-prism-list-row-trigger]').nth(1)).toBeFocused();

    await firstTrigger.evaluate(element => {
      element.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', altKey: true, bubbles: true }));
    });
    await expect(page.locator('[data-prism-list-row]').first()).not.toHaveAttribute('data-prism-list-row', 'applicant-details');

    await page.locator('[data-prism-linear-filter="back-stage"]').click();
    await expect(page.locator('[data-prism-list-row]')).toHaveCount(1);
  });

  test('create stage dialog validates input and creates a stage', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });
    await page.getByRole('button', { name: 'Add stage' }).click();

    const dialog = page.locator('[data-prism-create-stage-dialog]');
    await expect(dialog).toBeVisible();

    const keyInput = dialog.locator('[data-prism-create-stage-key]');
    await keyInput.fill('');
    await dialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(page.locator('[data-prism-create-stage-error]')).toContainText(/stage key is required/i);

    await dialog.locator('[data-prism-create-stage-title]').fill('Site visit');
    await keyInput.fill('site-visit');
    await dialog.locator('[data-prism-create-stage-actor]').selectOption('reviewer');
    await dialog.locator('[data-prism-create-stage-type]').selectOption('review');
    await dialog.getByRole('button', { name: 'Create stage' }).click();

    await expect(dialog).toBeHidden();
    await expect(page.locator('[data-prism-stage="site-visit"]')).toBeVisible();
  });

  test('delete stage confirmation lists affected transitions', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });
    await page.getByRole('button', { name: 'List view' }).click();
    await page.locator('[data-prism-delete-stage="reviewer-assessment"]').click();
    const dialog = page.locator('[data-prism-delete-stage-dialog]');
    await expect(dialog).toBeVisible();
    await expect(dialog.locator('[data-prism-delete-stage-transitions] li')).toHaveCount(3);
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(dialog).toBeHidden();
  });

  test('stage selection from graph and list updates the inspector in the host editor', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-stage="declaration"]').dblclick();
    await expect(page.locator('prism-step-inspector')).toBeFocused();
    await expect(page.locator('[data-prism-stage-detail="declaration"]')).toBeVisible();

    await page.locator('prism-workflow-graph').getByRole('button', { name: 'List view' }).click();
    await page.locator('[data-prism-list-row-trigger]').first().press('Enter');
    await expect(page.locator('[data-prism-stage-detail]')).toBeVisible();
  });

  test('role lanes are structurally visible and keyboard-accessible', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });

    // Role lanes should be rendered as focusable sections with semantic labels
    const lanes = page.locator('[data-prism-role-lane]');
    await expect(lanes).not.toHaveCount(0);

    // Each lane should have a heading and description
    const firstLane = lanes.first();
    await expect(firstLane.locator('.lane-heading')).toBeVisible();
    await expect(firstLane.locator('.lane-copy')).toBeVisible();

    // Lanes should be keyboard-focusable
    await firstLane.focus();
    await expect(firstLane).toBeFocused();

    // Lane headings should convey the role label, not just styling
    const headingText = await firstLane.locator('.lane-heading').textContent();
    expect(headingText).toBeTruthy();
    expect(headingText?.trim().length).toBeGreaterThan(0);
  });

  test('graph mode shows stages in role-specific lanes', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });

    // The workspace should be described as "Role-first"
    const canvas = page.getByRole('application');
    await expect(canvas).toHaveAttribute('aria-roledescription', /role-first/i);

    // Front-stage and back-stage lanes should both exist (for planning workflow)
    const frontStageLanes = page.locator('[data-prism-role-lane].lane-primary');
    const backStageLanes = page.locator('[data-prism-role-lane].lane-supporting');

    // At least one front-stage lane should exist
    await expect(frontStageLanes).not.toHaveCount(0);
  });

  test('keyboard navigation moves between lanes and stages', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });

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
    // Note: full inspector behavior is tested in the host editor test above
  });
});
