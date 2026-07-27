import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('ServiceBlueprint action editor', () => {
  test('stage action picker, generic parameters, forms-backed fields, and validation cover five action schemas', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-step-inspector--action-configuration'));

    await expect(page.locator('prism-step-inspector')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-action-param="0-assigneeValue"]').fill('planning-officers');
    await page.locator('[data-prism-action-param="0-overwriteExisting"]').check();

    await page.locator('[data-prism-action-param="1-title"]').fill('Request missing site evidence');
    await page.locator('[data-prism-action-param="1-dueDate"]').fill('2026-05-28');
    await page.locator('[data-prism-add-form-field="1"]').click();
    await page.locator('[data-prism-form-field-key="1-1"]').fill('supporting-date');
    await page.locator('[data-prism-form-field-label="1-1"]').fill('Evidence due date');
    await page.locator('[data-prism-form-field-type="1-1"]').selectOption('date');

    await page.locator('[data-prism-open-action-picker]').click();
    await page.locator('[data-prism-action-picker-option="case.enqueue"]').click();
    await page.locator('[data-prism-action-picker-add]').click();
    await page.locator('[data-prism-action-param="2-queue"]').fill('planning-intake');
    await page.locator('[data-prism-action-param="2-priority"]').selectOption('high');

    await page.locator('[data-prism-open-action-picker]').click();
    await page.locator('[data-prism-action-picker-option="case.set-status"]').click();
    await page.locator('[data-prism-action-picker-add]').click();
    await expect(page.locator('[data-prism-action-errors="3"]')).toContainText('Status is required');
    await page.locator('[data-prism-action-param="3-status"]').fill('Awaiting more evidence');
    await page.locator('[data-prism-action-param="3-reason"]').fill('The reviewer needs more documents before deciding.');
    await expect(page.locator('[data-prism-action-errors="3"]')).toBeHidden();

    await page.locator('[data-prism-open-action-picker]').click();
    await page.locator('[data-prism-action-picker-context]').selectOption('stage.onExit');
    await page.locator('[data-prism-action-picker-option="case.add-note"]').click();
    await page.locator('[data-prism-action-picker-add]').click();
    await page.locator('[data-prism-action-param="4-note"]').fill('Evidence request sent to applicant.');
    await page.locator('[data-prism-action-param="4-visibility"]').selectOption('public');

    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(5);
    await expect(page.locator('[data-prism-stage-action="0"] .action-summary')).toContainText('Assign to role planning-officers');
  });

  test('transition action picker filters to transition scope and validates email parameters with keyboard input', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-step-inspector--transition-action-configuration'));

    await expect(page.locator('prism-step-inspector')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-open-action-picker]').focus();
    await page.keyboard.press('Enter');

    await expect(page.locator('[data-prism-action-picker-option="case.add-note"]')).toBeVisible();
    await expect(page.locator('[data-prism-action-picker-option="forms.load"]')).toHaveCount(0);

    await page.locator('[data-prism-action-picker-option="notifications.send-email"]').click();
    await page.locator('[data-prism-action-picker-add]').click();

    await page.locator('[data-prism-action-param="1-templateId"]').fill('review-routed');
    await page.locator('[data-prism-action-param="1-recipientEmail"]').fill('not-an-email');
    await expect(page.locator('[data-prism-action-errors="1"]')).toContainText('valid email address');

    await page.locator('[data-prism-action-param="1-recipientEmail"]').fill('planning.officers@council.example');
    await page.locator('[data-prism-action-param="1-subject"]').fill('Application ready for review');
    await expect(page.locator('[data-prism-action-errors="1"]')).toBeHidden();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(2);
  });

  test('keyboard-only authoring supports picker flow, field reorder, and explicit delete confirmation', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-step-inspector--action-configuration'));

    await expect(page.locator('prism-step-inspector')).toBeVisible({ timeout: 10_000 });

    const addActionButton = page.locator('[data-prism-open-action-picker]');
    await addActionButton.focus();
    await page.keyboard.press('Enter');

    const pickerDialog = page.locator('[data-prism-action-picker-dialog]');
    await expect(pickerDialog).toBeVisible();
    await expect(pickerDialog.locator('[data-prism-action-picker-search]')).toBeFocused();

    await page.keyboard.type('SMS');
    await expect(page.locator('[data-prism-action-picker-option="notifications.send-sms"]')).toBeVisible();
    await expect(page.locator('[data-prism-action-picker-option="notifications.send-email"]')).toHaveCount(0);

    await page.locator('[data-prism-action-picker-option="notifications.send-sms"]').click();
    await expect(page.locator('[data-prism-action-picker-option="notifications.send-sms"]')).toHaveClass(/\bselected\b/);
    await page.locator('[data-prism-action-picker-add]').click();

    await expect(pickerDialog).toBeHidden();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(3);

    await page.locator('[data-prism-action-param="2-templateId"]').fill('review-routed-sms');
    await page.locator('[data-prism-action-param="2-recipientNumber"]').fill('+441234567890');
    await expect(page.locator('[data-prism-action-errors="2"]')).toBeHidden();
    await expect(page.locator('[data-prism-stage-action="2"] .action-summary')).toContainText('+441234567890');

    // For buttons inside action-list items, use the double-focus pattern: explicit focus()
    // then locator.press(). The action list's @focusin→requestAnimationFrame can move focus
    // between steps, but locator.press() refocuses the target before dispatching the key,
    // so the key always lands on the intended element.
    const addFieldButton = page.locator('[data-prism-add-form-field="1"]');
    await addFieldButton.focus();
    // Drain the rAF scheduled by _setSelectedAction(1) (focus moved from action 2 to action 1).
    // Without this, the rAF fires between press()'s internal focus and keydown, stealing focus.
    await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => resolve())));
    await addFieldButton.press('Enter');
    await expect(page.locator('[data-prism-form-field="1-1"]')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-form-field-key="1-1"]').fill('supporting-date');
    await page.locator('[data-prism-form-field-label="1-1"]').fill('Evidence due date');
    await page.locator('[data-prism-form-field-type="1-1"]').selectOption('date');

    const moveFieldUpButton = page.locator('[data-prism-form-field="1-1"]').getByRole('button', { name: 'Move up' });
    await moveFieldUpButton.focus();
    await moveFieldUpButton.press('Enter');
    await expect(page.locator('[data-prism-form-field-key="1-0"]')).toHaveValue('supporting-date');

    const actionItem2 = page.locator('[data-prism-stage-action="2"]');
    await actionItem2.focus();
    await actionItem2.press('Alt+ArrowUp');
    await expect(page.locator('[data-prism-stage-action="1"] .action-title')).toContainText('Send SMS');
    // _moveAction calls _setSelectedAction(1), triggering a Lit render → updated() → rAF for
    // _focusActionEditor(1). That rAF can fire between press()'s internal focus CDP call and its
    // keydown CDP call, stealing focus from the remove button. Drain it here so state is stable.
    await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => resolve())));

    const removeButton = page.locator('[data-prism-stage-action-remove="1"]');
    await removeButton.focus();
    await removeButton.press('Enter');

    const deleteDialog = page.locator('[data-prism-delete-action-dialog]');
    await expect(deleteDialog).toBeVisible();
    await expect(deleteDialog).toContainText('Delete Send SMS?');
    await expect(page.locator('[data-prism-delete-action-cancel]')).toBeFocused();
    await page.locator('[data-prism-delete-action-cancel]').press('Escape');

    await expect(deleteDialog).toBeHidden();
    await expect(removeButton).toBeFocused();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(3);
    await deleteDialog.waitFor({ state: 'detached' });

    await removeButton.focus();
    await removeButton.press('Enter');
    await expect(deleteDialog).toBeVisible();
    await page.locator('[data-prism-delete-action-confirm]').press('Enter');

    await expect(deleteDialog).toBeHidden();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(2);
    await expect(page.locator('prism-stage-action-editor').getByText('Send SMS removed.')).toBeVisible();
  });
});
