import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('Workflow editor validation rail', () => {
  test('shows workflow-friendly issues, links to affected items, and blocks save only for critical errors', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-stage="declaration"]').dblclick();
    const actionInput = page.locator('[data-prism-action-param="0-formDefinitionId"]');
    await expect(actionInput).toHaveValue('planning-declaration');
    await actionInput.evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = '';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });

    await expect(page.locator('[data-prism-action-errors="0"]')).toContainText('Form definition id is required');
    await expect(page.locator('[data-prism-validation-rail]')).toContainText(
      'Stage “Declaration” has an action that needs attention: “Load form” — Form definition id is required.'
    );
    await expect(page.locator('[data-prism-save]')).toBeEnabled();

    await page.locator('[data-prism-add-stage]').click();
    const createStageDialog = page.locator('[data-prism-create-stage-dialog]');
    await expect(createStageDialog).toBeVisible();
    await createStageDialog.locator('[data-prism-create-stage-title]').evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = 'Site visit';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });
    await createStageDialog.locator('[data-prism-create-stage-key]').evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = 'site-visit';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });
    await createStageDialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(createStageDialog).toBeHidden();

    await expect(page.locator('[data-prism-validation-rail]')).toContainText('is orphaned');
    await expect(page.locator('[data-prism-save]')).toBeDisabled();

    const handle = page.locator('[data-prism-transition-handle="site-visit"]');
    await handle.focus();
    await handle.press('Enter');

    const createTransitionDialog = page.locator('[data-prism-create-transition-dialog]');
    await expect(createTransitionDialog).toBeVisible();
    await createTransitionDialog.locator('[data-prism-create-transition-target]').selectOption('submitted');
    await createTransitionDialog.locator('[data-prism-create-transition-label]').fill('complete-site-visit');
    await createTransitionDialog.getByRole('button', { name: 'Create transition' }).click();
    await expect(createTransitionDialog).toBeHidden();

    await expect(page.locator('[data-prism-validation-rail]')).toContainText(
      'Stage “Site visit” is unreachable from the workflow start. Add or retarget a transition so authors can get there.'
    );

    await page.locator('[data-prism-validation-issue="stage-unreachable-site-visit"]').click();
    await expect(page.locator('[data-prism-stage-detail="site-visit"]')).toBeVisible();

    await page.locator('[data-prism-validation-issue*="declaration-action-0-formDefinitionId"]').click();
    await expect(page.locator('[data-prism-stage-detail="declaration"]')).toBeVisible();
    await expect(actionInput).toBeFocused();
  });
});
