import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function slowProjectPreview(page: import('@playwright/test').Page, delayMs: number) {
  await page.evaluate(delay => {
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
      const url =
        typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.href
            : input.url;

      if (/\/api\/workflow-authoring\/workflows\/.+\/project$/.test(url)) {
        await new Promise(resolve => window.setTimeout(resolve, delay));
      }

      return originalFetch(input, init);
    };
  }, delayMs);
}

test.describe('Workflow editor stage preview', () => {
  test('renders a read-only runtime preview for the selected planning stage', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-stage="declaration"]').dblclick();

    // Switch to Preview tab to see the preview panel
    await page.locator('[data-prism-tab="preview"]').click();

    const preview = page.locator('[data-prism-stage-preview]');
    await expect(preview).toBeVisible();
    await expect(preview.locator('[data-prism-preview-stage-name]')).toHaveText('Declaration');
    await expect(preview.locator('[data-prism-preview-shell]')).toContainText('Question shell');
    await expect(preview.locator('[data-prism-preview-readonly]')).toBeVisible();
    await expect(preview).toContainText('Applicant name');
    await expect(preview).toContainText('Site address');
    await expect(preview.locator('.govuk-input').first()).toBeDisabled();
    await expect(preview.locator('.govuk-textarea').first()).toBeDisabled();
    await expect(preview.locator('[data-prism-preview-action="continue"]')).toBeDisabled();
    await expect(preview.locator('[data-prism-preview-surface="public"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(preview.locator('[data-prism-preview-surface="member"]')).toBeEnabled();
    await expect(preview.locator('[data-prism-preview-surface="back-stage"]')).toBeDisabled();
  });

  test('updates the preview when stage edits change the projected runtime and exposes loading feedback', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));
    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    await slowProjectPreview(page, 400);
    await page.locator('[data-prism-stage="declaration"]').dblclick();

    // Switch to Preview tab to see the preview panel
    await page.locator('[data-prism-tab="preview"]').click();

    await expect(page.locator('[data-prism-preview-loading]')).toContainText('Rendering preview');
    await expect(page.locator('[data-prism-preview-stage-name]')).toHaveText('Declaration');

    const titleInput = page.locator('[data-prism-stage-title]');
    await titleInput.fill('Declaration preview');
    await titleInput.press('Tab');

    await expect(page.locator('[data-prism-preview-loading]')).toContainText('Updating preview');
    await expect(page.locator('[data-prism-preview-stage-name]')).toHaveText('Declaration preview');

    await page.locator('[data-prism-stage-actor]').selectOption('reviewer');
    await expect(page.locator('[data-prism-preview-surface="back-stage"]')).toBeEnabled();
    await expect(page.locator('[data-prism-preview-surface="back-stage"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(page.locator('[data-prism-preview-surface-panel="back-stage"]')).toBeVisible();
  });
});
