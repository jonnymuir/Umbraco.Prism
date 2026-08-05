// Regression coverage: revisiting the transfer-a-juggling-licence "Upload your evidence" stage
// via a Check Answers "Change" link and clicking Continue without re-selecting any file used to
// reject every required file field as missing, even though each already had an uploaded file.
//
// Two independent bugs combined to cause this:
// 1. Wayfinder.Engine's ProcessManagerEngine.Advance built its pre-transition Required check
//    purely from the freshly-posted fieldValues, never falling back to the current stage's own
//    already-persisted instance.FieldValues — so a host correctly omitting an untouched field's
//    key (the only way to represent "unchanged" for a file input, which a browser can never
//    pre-fill) still read as "no answer".
// 2. Wayfinder.Umbraco's ServiceRequestPageController never actually left the key out in the
//    first place: an untouched <input type="file"> still posts as a regular, empty form field
//    under its own name, and that empty string flowed straight into fieldValues sent to the
//    engine, wiping out whatever was already there.
//
// Fixed by (1) merging in the current stage's own persisted values before validating in the
// engine, scoped to just that stage's field keys, and (2) stripping those same empty-string
// keys back out of fieldValues in the controller before forwarding to the engine.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';

const appHost = new LiveAppHost();

test.describe('Transfer licence Upload Evidence retention', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(6 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('revisiting the upload stage via Change and continuing without reselecting retains every file', async ({ page }) => {
    await page.goto('/transfer-your-existing-juggling-licence');

    // Eligibility gateway chain — three yes/no questions.
    for (let i = 0; i < 3; i++) {
      await page.getByRole('button', { name: 'Yes' }).click();
      await page.waitForLoadState('networkidle');
    }

    await expect(page.getByRole('heading', { name: 'Before you continue' })).toBeVisible({ timeout: 15_000 });
    const checkboxes = page.locator('input[type="checkbox"]');
    const checkboxCount = await checkboxes.count();
    for (let i = 0; i < checkboxCount; i++) {
      await checkboxes.nth(i).check();
    }
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Your existing licence' })).toBeVisible({ timeout: 15_000 });
    await page.getByLabel('Name of the authority that issued your current licence').fill('International Juggling Federation');
    await page.getByLabel('Licence reference number').fill('IJF-2024-00123');
    await page.getByLabel('Competitive').check();
    await page.locator('#issue-date-day').fill('1');
    await page.locator('#issue-date-month').fill('6');
    await page.locator('#issue-date-year').fill('2020');
    await page.locator('#expiry-date-day').fill('1');
    await page.locator('#expiry-date-month').fill('6');
    await page.locator('#expiry-date-year').fill('2027');
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Upload your evidence' })).toBeVisible({ timeout: 15_000 });
    const fileInputs = page.locator('input[type="file"]');
    const fileCount = await fileInputs.count();
    for (let i = 0; i < fileCount; i++) {
      await fileInputs.nth(i).setInputFiles({
        name: 'evidence.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('test file content'),
      });
    }
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible({ timeout: 15_000 });

    // Change link back to the upload stage.
    const evidenceChange = page.locator('dt.govuk-summary-list__key:has-text("Current licence")')
      .locator('xpath=following-sibling::dd[contains(@class,"actions")]')
      .getByRole('button', { name: /^Change/ });
    await evidenceChange.first().click();

    await expect(page.getByRole('heading', { name: 'Upload your evidence' })).toBeVisible({ timeout: 15_000 });

    // Continue without reselecting any file.
    await page.getByRole('button', { name: 'Continue' }).click();

    // Must advance past the upload stage — not bounce back with "is required" errors.
    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.govuk-error-summary')).toHaveCount(0);
  });
});
