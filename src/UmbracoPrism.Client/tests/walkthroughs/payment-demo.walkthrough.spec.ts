// Executable counterpart of docs/walkthroughs/payment-demo.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { step, signIn, resetWorkflows } from './support/walkthrough';

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
});
