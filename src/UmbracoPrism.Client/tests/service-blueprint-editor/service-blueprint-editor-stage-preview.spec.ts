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

      if (/\/api\/service-blueprint-authoring\/service-blueprints\/.+\/project$/.test(url)) {
        await new Promise(resolve => window.setTimeout(resolve, delay));
      }

      return originalFetch(input, init);
    };
  }, delayMs);
}

async function openPreviewForDeclaration(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: /Declaration, Applicant queue/i }).dblclick();
  const previewTab = page.getByRole('tab', { name: 'Preview' });
  await expect(previewTab).toBeVisible();
  await previewTab.click();
  await expect(previewTab).toHaveAttribute('aria-selected', 'true');
}

test.describe('ServiceBlueprint editor stage preview', () => {
  test('renders a read-only runtime preview for the selected planning stage', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await openPreviewForDeclaration(page);

    const preview = page.locator('[data-prism-stage-preview]');
    await expect(page.getByRole('heading', { name: 'Stage preview' })).toBeVisible();
    await expect(preview).toBeVisible();
    await expect(preview.locator('[data-prism-preview-stage-name]')).toHaveText('Declaration');
    await expect(preview.locator('[data-prism-preview-shell]')).toContainText('Question shell');
    await expect(preview.locator('[data-prism-preview-readonly]')).toBeVisible();
    await expect(preview).toContainText('Applicant name');
    await expect(preview).toContainText('Site address');
    await expect(preview.locator('.govuk-input').first()).toBeDisabled();
    await expect(preview.locator('.govuk-textarea').first()).toBeDisabled();
    await expect(preview.locator('[data-prism-preview-action="continue"]')).toBeDisabled();
    await expect(preview.locator('[data-prism-preview-assignment]')).toContainText('Assigned to Applicant');
    await expect(preview.locator('[data-prism-preview-selector]')).toHaveCount(0);
  });

  test.fixme('updates the preview when stage edits change the projected runtime', async ({ page }) => {
    // slowProjectPreview intercepts fetch calls, but projection is now local and synchronous.
    // The loading state flash happens before Lit renders. Needs async projection or a different approach.
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });

    await slowProjectPreview(page, 1_500);
    await openPreviewForDeclaration(page);
    await expect(page.locator('[data-prism-preview-loading]')).toContainText('Rendering preview');
    await expect(page.locator('[data-prism-preview-stage-name]')).toHaveText('Declaration');

    await page.getByRole('tab', { name: 'Canvas' }).click();
    const titleInput = page.getByLabel('Title');
    await titleInput.fill('Declaration preview');
    await titleInput.press('Tab');
    const laneInput = page.locator('[data-prism-stage-queue]');
    await laneInput.fill('reviewer');
    await laneInput.press('Tab');
    await page.getByRole('tab', { name: 'Preview' }).click();

    await expect(page.locator('[data-prism-preview-stage-name]')).toHaveText('Declaration preview');
    await expect(page.locator('[data-prism-preview-assignment]')).toContainText('Assigned to Reviewer');
    await expect(page.locator('[data-prism-preview-surface-panel]')).toBeVisible();
  });
});
