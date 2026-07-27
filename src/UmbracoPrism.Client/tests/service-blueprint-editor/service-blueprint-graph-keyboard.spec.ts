import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('ServiceBlueprint graph workspace', () => {
  /**
   * These tests intentionally focus on the graph canvas only.
   * The simpler editor contract is graph-first: no list fallback is required for keyboard proof.
   */
  test('graph mode supports keyboard selection and the inspector shortcut', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });

    const declarationStage = page.locator('[data-prism-stage="declaration"]');
    await declarationStage.focus();
    await expect(declarationStage).toBeFocused();

    await declarationStage.press('Enter');
    await expect(declarationStage).toHaveAttribute('aria-pressed', 'true');

    await declarationStage.press('e');
    await expect(page.locator('prism-step-inspector')).toBeFocused();
    await expect(page.locator('[data-prism-stage-detail="declaration"]')).toBeVisible();
  });

  test('create stage dialog validates input and creates a stage from graph mode', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--workspace-canvas'));

    await expect(page.locator('prism-service-blueprint-graph')).toBeVisible({ timeout: 10_000 });
    await page.getByRole('button', { name: 'Add stage' }).click();

    const dialog = page.locator('[data-prism-create-stage-dialog]');
    await expect(dialog).toBeVisible();

    const keyInput = dialog.locator('[data-prism-create-stage-key]');
    await keyInput.fill('');
    await dialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(page.locator('[data-prism-create-stage-error]')).toContainText(/stage key is required/i);

    await dialog.locator('[data-prism-create-stage-title]').fill('Site visit');
    await keyInput.fill('site-visit');
    await dialog.locator('[data-prism-create-stage-queue]').fill('reviewer');
    await dialog.locator('[data-prism-create-stage-type]').selectOption('review');
    await dialog.getByRole('button', { name: 'Create stage' }).click();

    await expect(dialog).toBeHidden();
    await expect(page.locator('[data-prism-stage="site-visit"]')).toBeVisible();
  });

  test('delete stage confirmation can be opened from a graph stage by keyboard', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--workspace-canvas'));

    await expect(page.locator('prism-service-blueprint-graph')).toBeVisible({ timeout: 10_000 });

    const stage = page.locator('[data-prism-stage="reviewer-assessment"]');
    await stage.focus();
    await stage.press('Delete');

    const dialog = page.locator('[data-prism-delete-stage-dialog]');
    await expect(dialog).toBeVisible();
    expect(await dialog.locator('[data-prism-delete-stage-transitions] li').count()).toBeGreaterThan(0);
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(dialog).toBeHidden();
  });

  test('role lanes are structurally visible and keyboard-accessible (vertical orientation)', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--workspace-canvas'));

    await expect(page.locator('prism-service-blueprint-graph')).toBeVisible({ timeout: 10_000 });

    const lanes = page.locator('[data-prism-role-queue]');
    await expect(lanes).not.toHaveCount(0);

    const firstLane = lanes.first();
    await expect(firstLane.locator('.lane-heading')).toBeVisible();
    await expect(firstLane.locator('.lane-meta')).toBeVisible();

    await firstLane.focus();
    await expect(firstLane).toBeFocused();

    const headingText = await firstLane.locator('.lane-heading').textContent();
    expect(headingText?.trim().length).toBeGreaterThan(0);
  });

  test('keyboard navigation moves between lanes and stages (vertical orientation)', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--workspace-canvas'));

    await expect(page.locator('prism-service-blueprint-graph')).toBeVisible({ timeout: 10_000 });

    const firstLane = page.locator('[data-prism-role-queue]').first();
    await firstLane.focus();

    await page.keyboard.press('Tab');
    const firstStage = page.locator('[data-prism-stage]').first();

    await firstStage.press('Enter');
    await expect(firstStage).toHaveAttribute('aria-pressed', 'true');

    await firstStage.press('e');
  });
});
