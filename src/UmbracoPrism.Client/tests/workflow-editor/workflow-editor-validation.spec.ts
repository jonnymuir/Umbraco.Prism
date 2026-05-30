import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('Workflow editor validation rail', () => {
  test('keeps detailed warning copy in Validation instead of repeating it across the canvas', async ({ page }) => {
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

    const validationRail = page.locator('[data-prism-validation-rail]');
    await expect(validationRail).toContainText('Connect it through a gateway so authors can reach it.');
    await expect(validationRail).toContainText('Site visit');
    await expect(page.locator('[data-prism-save]')).toBeDisabled();
    await expect(page.locator('[data-prism-canvas-health-hint]')).toContainText('Open Validation');

    const canvasWarnings = await page.locator('prism-workflow-graph').evaluate(graphElement => {
      const graph = graphElement as HTMLElement;
      const shadowRoot = graph.shadowRoot;
      if (!shadowRoot) {
        throw new Error('Graph shadow root not found');
      }

      return {
        title: shadowRoot.querySelector('.validation-banner-title')?.textContent?.trim() ?? '',
        issues: Array.from(shadowRoot.querySelectorAll('.validation-link')).map(issue => issue.textContent?.trim() ?? ''),
      };
    });

    expect(canvasWarnings.title).toBe('');
    expect(canvasWarnings.issues).toEqual([]);

    const validationTab = page.getByRole('tab', { name: 'Validation' });
    await expect(validationTab).toBeVisible();
    await page.locator('[data-prism-open-validation]').click();
    await expect(validationTab).toHaveAttribute('aria-selected', 'true');
    await page.locator('[data-prism-validation-issue]').filter({ hasText: 'Site visit' }).first().click();
    await expect(page.locator('[data-prism-stage-detail="site-visit"]')).toBeVisible();
  });

  test('shows plain-language issues and jumps to the affected stage or field', async ({ page }) => {
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

    const validationTab = page.getByRole('tab', { name: 'Validation' });
    await validationTab.click();
    await expect(validationTab).toHaveAttribute('aria-selected', 'true');
    await page.locator('[data-prism-validation-issue*="declaration-action-0-formDefinitionId"]').click();
    await expect(page.locator('[data-prism-stage-detail="declaration"]')).toBeVisible();
    await expect(actionInput).toBeFocused();
  });
});
