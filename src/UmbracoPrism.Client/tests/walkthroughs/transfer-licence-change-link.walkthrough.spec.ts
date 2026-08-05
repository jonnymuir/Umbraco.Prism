// Regression coverage: Check Answers' "Change" links on the transfer-a-juggling-licence
// journey used to be pure no-ops. Root cause was in Wayfinder.Umbraco's own
// Views/Partials/_Stage-Review.cshtml: it hand-rolled its own <form>, wrapped only around the
// action-button group, and rendered the components loop (which includes the summary-list and
// therefore every "Change" button) entirely outside it — so every Change button, though
// type="submit", had no enclosing <form> at all and silently did nothing on click. Confirmed via
// btn.closest('form') === null and by dumping the raw DOM (the <form> opened after the whole
// summary-list had already rendered). Fixed by switching _Stage-Review.cshtml onto the same
// <wayfinder-stage-form> tag helper _Stage-Question.cshtml already used correctly.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';

const appHost = new LiveAppHost();

test.describe('Transfer licence Check Answers Change link', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(6 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('clicking Change on Check Answers navigates back to the target stage', async ({ page }) => {
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

    const firstChange = page.getByRole('button', { name: /^Change/ }).first();
    await expect(firstChange).toBeVisible();
    await firstChange.click();

    await expect(page.getByRole('heading', { name: 'Your existing licence' })).toBeVisible({ timeout: 10_000 });
  });
});
