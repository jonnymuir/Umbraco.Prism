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
  await page.locator('prism-service-blueprint-editor').evaluate((element, mac) => {
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

test.describe('ServiceBlueprint editor undo and redo', () => {
  test('toolbar buttons and keyboard shortcuts replay stage title edits', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
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
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-add-stage]').click();
    const createStageDialog = page.locator('[data-prism-create-stage-dialog]');
    await expect(createStageDialog).toBeVisible();
    await createStageDialog.locator('[data-prism-create-stage-title]').fill('Site visit');
    await createStageDialog.locator('[data-prism-create-stage-key]').fill('site-visit');
    await createStageDialog.locator('[data-prism-create-stage-queue]').fill('reviewer');
    await createStageDialog.locator('[data-prism-create-stage-type]').selectOption('review');
    await createStageDialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(createStageDialog).toBeHidden();

    await expectStageSelectionDetails(page, 'site-visit');
    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-stage="site-visit"]')).toHaveCount(0);
    await page.locator('[data-prism-redo]').click();
    await expectStageSelectionDetails(page, 'site-visit');

    // Route mutations are now scoped to the gateway inspector's outgoing-routes panel
    // (Slice 3b.1: transition creation/edit was removed from the canvas; routes are
    // only authored from existing gateway transitions). Switch fixtures to exercise
    // route label edits and route deletion undo/redo on the gateway story.
    await page.goto(storyUrl('service-blueprint-editor-editor-host--gateway-representation'));
    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-gateway="review-split"]').click();
    const labelInput = page
      .locator('[data-prism-gateway-route] [data-prism-route-label]')
      .first();
    await expect(labelInput).toBeVisible();
    const originalLabel = await labelInput.inputValue();

    await labelInput.fill('continue applicant branch (edited)');
    await labelInput.press('Tab');
    await expect(
      page
        .locator('[data-prism-gateway-route] [data-prism-route-label]')
        .first()
    ).toHaveValue('continue applicant branch (edited)');

    await page.locator('[data-prism-undo]').click();
    await expect(
      page
        .locator('[data-prism-gateway-route] [data-prism-route-label]')
        .first()
    ).toHaveValue(originalLabel);
    await pressRedoShortcut(page);
    await expect(
      page
        .locator('[data-prism-gateway-route] [data-prism-route-label]')
        .first()
    ).toHaveValue('continue applicant branch (edited)');

    const routesBefore = await page.locator('[data-prism-gateway-route]').count();
    await page
      .locator('[data-prism-gateway-route] [data-prism-route-delete]')
      .first()
      .click();
    await expect(page.locator('[data-prism-gateway-route]')).toHaveCount(routesBefore - 1);

    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-gateway-route]')).toHaveCount(routesBefore);
    await pressRedoShortcut(page);
    await expect(page.locator('[data-prism-gateway-route]')).toHaveCount(routesBefore - 1);
  });

  test('action adds, parameter edits, reorders, and deletes replay through history', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });

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
