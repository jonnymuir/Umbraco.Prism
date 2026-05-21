import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('Workflow transition editor', () => {
  test('dragging a graph handle opens the transition label prompt and adds a route', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    await expect(page.locator('prism-workflow-graph')).toBeVisible({ timeout: 10_000 });

    const handle = page.locator('[data-prism-transition-handle="waiting-for-review"]');
    const target = page.locator('[data-prism-stage="confirmation"]');
    const handleBox = await handle.boundingBox();
    const targetBox = await target.boundingBox();
    expect(handleBox).not.toBeNull();
    expect(targetBox).not.toBeNull();

    await page.mouse.move(handleBox!.x + handleBox!.width / 2, handleBox!.y + handleBox!.height / 2);
    await page.mouse.down();
    await page.mouse.move(targetBox!.x + targetBox!.width / 2, targetBox!.y + targetBox!.height / 2, { steps: 12 });
    await page.mouse.up();

    const dialog = page.locator('[data-prism-create-transition-dialog]');
    await expect(dialog).toBeVisible();
    await dialog.locator('[data-prism-create-transition-label]').fill('approve');
    await dialog.locator('[data-prism-create-transition-condition-mode]').selectOption('guard');
    await dialog.locator('[data-prism-create-transition-condition-value]').fill('case.readyForDecision == true');
    await dialog.getByRole('button', { name: 'Create transition' }).click();

    await expect(dialog).toBeHidden();
    await expect(page.locator('[data-prism-transition]')).toHaveCount(6);
    await expect(page.locator('[data-prism-transition]').last()).toContainText('approve');
  });

  test('keyboard transition editing retargets, validates connectivity, and deletes cleanly', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    const handle = page.locator('[data-prism-transition-handle="submitted"]');
    await handle.focus();
    await handle.press('Enter');

    const dialog = page.locator('[data-prism-create-transition-dialog]');
    await expect(dialog).toBeVisible();
    await dialog.locator('[data-prism-create-transition-target]').selectOption('application-form');
    await dialog.locator('[data-prism-create-transition-label]').fill('return');
    await dialog.getByRole('button', { name: 'Create transition' }).click();

    await expect(page.locator('[data-prism-transition]')).toHaveCount(4);
    await expect(page.locator('[data-prism-transition-detail="submitted-return-application-form"]')).toBeVisible();

    await page.locator('[data-prism-transition-action]').selectOption('submit');
    await page.locator('[data-prism-transition-target]').selectOption('check-answers');
    await page.locator('[data-prism-transition-condition-mode]').selectOption('event');
    await page.locator('[data-prism-transition-condition-value]').fill('application-resubmitted');
    await page.locator('[data-prism-transition-condition-value]').press('Enter');

    await page.locator('prism-workflow-graph').getByRole('button', { name: 'List view' }).click();
    const submittedRow = page.locator('[data-prism-list-row="submitted"]');
    await expect(submittedRow.locator('[data-prism-list-transition]')).toContainText('submit → Check your answers (Event: application-resubmitted)');

    await submittedRow.locator('[data-prism-list-transition]').click();
    await expect(page.locator('[data-prism-transition-detail="submitted-submit-check-answers"]')).toBeVisible();
    await page.locator('[data-prism-transition-delete]').click();

    await expect(submittedRow.locator('[data-prism-list-transition]')).toHaveCount(0);
    await page.locator('prism-workflow-graph').getByRole('button', { name: 'Graph view' }).click();
    await expect(page.locator('[data-prism-transition]')).toHaveCount(3);
  });
});
