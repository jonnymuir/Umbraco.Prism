// Executable counterpart of docs/walkthroughs/payment-demo.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { assertHealthyPage, step, signIn, resetWorkflows } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Payment demo walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await resetWorkflows(request);
  });

  test('happy path: user enters payment details and processes', async ({ page }) => {
    await signIn(page);
    await page.goto('/payment-demo');

    await step(page, '01-initial.png', {
      url: /\/payment-demo/,
      heading: 'Enter Payment Details'
    }, 'payment-demo');

    await page.getByLabel('Cardholder name').fill('Jane Doe');
    await page.getByLabel('Amount (£)').fill('42.50');
    await step(page, '02-form-filled.png', {
      url: /\/payment-demo/,
      heading: 'Enter Payment Details'
    }, 'payment-demo');

    await page.getByRole('button', { name: 'Submit' }).click();
    await step(page, '03-processing.png', {
      url: /\/payment-demo/,
      heading: 'Processing Your Payment'
    }, 'payment-demo');
  });

  test('validation: minimum decimal value enforced', async ({ page }) => {
    await signIn(page);
    await page.goto('/payment-demo');

    await page.getByLabel('Cardholder name').fill('Test User');
    await page.getByLabel('Amount (£)').fill('0'); // Below min of 0.01

    await page.getByRole('button', { name: 'Submit' }).click();

    // Should show validation error
    const errorSummary = page.locator('[role="alert"]').first();
    await expect(errorSummary).toBeVisible();
    await expect(errorSummary).toContainText('There is a problem');
  });

  test('processing state: defer option visible and returning user sees processing', async ({ page }) => {
    await signIn(page);
    await page.goto('/payment-demo');

    await assertHealthyPage(page, { url: /\/payment-demo/, heading: 'Enter Payment Details' });

    await page.getByLabel('Cardholder name').fill('Jane Doe');
    await page.getByLabel('Amount (£)').fill('25.00');
    await page.getByRole('button', { name: 'Submit' }).click();

    // Processing state heading
    await expect(page.getByRole('heading', { name: 'Processing Your Payment' })).toBeVisible({ timeout: 30_000 });

    // Defer message should be visible — processing state has allowDefer:true with a deferMessage
    await expect(page.locator('body')).toContainText('You can leave this page', { timeout: 10_000 });

    // Navigate away then return — instance policy 'single' means processing state persists
    await page.goto('/my-workflows');
    await expect(page.getByRole('heading', { name: 'My Workflows' })).toBeVisible();

    await page.goto('/payment-demo');
    await expect(page.getByRole('heading', { name: 'Processing Your Payment' })).toBeVisible({ timeout: 30_000 });
    await expect(page.locator('body')).not.toContainText('Enter Payment Details');
  });
});
