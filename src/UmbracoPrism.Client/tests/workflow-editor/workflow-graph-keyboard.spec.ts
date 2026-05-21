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
});
