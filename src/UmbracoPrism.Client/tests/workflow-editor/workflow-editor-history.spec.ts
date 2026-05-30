import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function expectStageSelectionDetails(
  page: import('@playwright/test').Page,
  stageKey: string
) {
  const stage = page.locator(`[data-prism-stage="${stageKey}"]`);
  await expect(stage).toBeVisible();
  await expect(stage).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator(`[data-prism-stage-detail="${stageKey}"]`)).toBeVisible();
}

async function pressRedoShortcut(page: import('@playwright/test').Page) {
  const isMac = process.platform === 'darwin';
  await page.locator('prism-workflow-editor').evaluate((element, mac) => {
    element.dispatchEvent(new KeyboardEvent('keydown', {
      key: 'z',
      bubbles: true,
      composed: true,
      shiftKey: true,
      metaKey: mac,
      ctrlKey: !mac,
    }));
  }, isMac);
}

test.describe('Workflow editor undo and redo', () => {
  test('toolbar buttons and keyboard shortcuts replay stage title edits', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('[data-prism-undo]')).toBeDisabled();
    await expect(page.locator('[data-prism-redo]')).toBeDisabled();

    await page.locator('[data-prism-stage="declaration"]').dblclick();
    const titleInput = page.locator('[data-prism-stage-title]');
    await expect(titleInput).toHaveValue('Declaration');
    await titleInput.fill('Declaration updated');
    await titleInput.press('Tab');

    await expect(page.locator('[data-prism-stage="declaration"]')).toContainText('Declaration updated');
    await expect(page.locator('[data-prism-history-status]')).toContainText('1 change available to undo');
    await expect(page.locator('[data-prism-undo]')).toBeEnabled();
    await expect(page.locator('[data-prism-redo]')).toBeDisabled();

    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-stage="declaration"]')).toContainText('Declaration');
    await expect(titleInput).toHaveValue('Declaration');
    await expect(page.locator('[data-prism-redo]')).toBeEnabled();

    await pressRedoShortcut(page);
    await expect(page.locator('[data-prism-stage="declaration"]')).toContainText('Declaration updated');
    await expect(titleInput).toHaveValue('Declaration updated');
    await expect(page.locator('[data-prism-redo]')).toBeDisabled();
  });

  test('stage and transition mutations can be undone and redone from the host editor', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-add-stage]').click();
    const createStageDialog = page.locator('[data-prism-create-stage-dialog]');
    await expect(createStageDialog).toBeVisible();
    await createStageDialog.locator('[data-prism-create-stage-title]').fill('Site visit');
    await createStageDialog.locator('[data-prism-create-stage-key]').fill('site-visit');
    await createStageDialog.locator('[data-prism-create-stage-lane]').fill('reviewer');
    await createStageDialog.locator('[data-prism-create-stage-type]').selectOption('review');
    await createStageDialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(createStageDialog).toBeHidden();

    await expectStageSelectionDetails(page, 'site-visit');
    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-stage="site-visit"]')).toHaveCount(0);
    await page.locator('[data-prism-redo]').click();
    await expectStageSelectionDetails(page, 'site-visit');

    const handle = page.locator('[data-prism-transition-handle="submitted"]');
    await handle.focus();
    await handle.press('Enter');

    const createTransitionDialog = page.locator('[data-prism-create-transition-dialog]');
    await expect(createTransitionDialog).toBeVisible();
    await createTransitionDialog.locator('[data-prism-create-transition-target]').selectOption('application-form');
    await createTransitionDialog.locator('[data-prism-create-transition-label]').fill('return');
    await createTransitionDialog.locator('[data-prism-create-transition-submit]').click();

    await expect(page.locator('[data-prism-transition-detail="submitted-return-application-form"]')).toBeVisible();
    await expect(page.locator('[data-prism-transition]')).toHaveCount(4);

    await page.locator('[data-prism-transition-action]').selectOption('submit');
    await expect(page.locator('[data-prism-transition-detail="submitted-submit-application-form"]')).toBeVisible();

    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-transition-detail="submitted-return-application-form"]')).toBeVisible();
    await page.locator('[data-prism-redo]').click();
    await expect(page.locator('[data-prism-transition-detail="submitted-submit-application-form"]')).toBeVisible();

    await page.locator('[data-prism-transition-delete]').click();
    await expect(page.locator('[data-prism-transition]')).toHaveCount(3);
    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-transition]')).toHaveCount(4);
    await expect(page.locator('[data-prism-transition-detail="submitted-submit-application-form"]')).toBeVisible();
    await pressRedoShortcut(page);
    await expect(page.locator('[data-prism-transition]')).toHaveCount(3);
  });

  test('action adds, parameter edits, reorders, and deletes replay through history', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-stage="declaration"]').dblclick();
    const formDefinitionInput = page.locator('[data-prism-action-param="0-formDefinitionId"]');
    await expect(formDefinitionInput).toHaveValue('planning-declaration');
    await formDefinitionInput.fill('planning-declaration-v2');
    await expect(formDefinitionInput).toHaveValue('planning-declaration-v2');

    await page.locator('[data-prism-open-action-picker]').click();
    await page.locator('[data-prism-action-picker-option="notifications.send-sms"]').click();
    await page.locator('[data-prism-action-picker-add]').click();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(2);

    await page.locator('[data-prism-stage-action="1"]').focus();
    await page.keyboard.press('Alt+ArrowUp');
    await expect(page.locator('[data-prism-stage-action="0"] .action-title')).toContainText('Send SMS');

    await page.locator('[data-prism-stage-action-remove="0"]').click();
    const deleteDialog = page.locator('[data-prism-delete-action-dialog]');
    await expect(deleteDialog).toBeVisible();
    await page.locator('[data-prism-delete-action-confirm]').click();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(1);

    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(2);
    await expect(page.locator('[data-prism-stage-action="0"] .action-title')).toContainText('Send SMS');

    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-stage-action="1"] .action-title')).toContainText('Send SMS');

    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(1);

    await page.locator('[data-prism-undo]').click();
    await expect(formDefinitionInput).toHaveValue('planning-declaration');

    await page.locator('[data-prism-redo]').click();
    await expect(formDefinitionInput).toHaveValue('planning-declaration-v2');
    await page.locator('[data-prism-redo]').click();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(2);
    await page.locator('[data-prism-redo]').click();
    await expect(page.locator('[data-prism-stage-action="0"] .action-title')).toContainText('Send SMS');
    await page.locator('[data-prism-redo]').click();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(1);
  });
});
