// Executable counterpart of docs/walkthroughs/information-request.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { assertHealthyPage, step, signIn, resetWorkflows } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Information request walkthrough', () => {
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

  test('happy path: user submits information request', async ({ page }) => {
    await signIn(page);
    await page.goto('/request-information');

    await step(page, '01-initial.png', {
      url: /\/request-information/,
      heading: 'Tell us about yourself'
    }, 'information-request');

    await page.getByLabel('First name').fill('Jane');
    await page.getByLabel('Last name').fill('Smith');
    await page.locator('#dateOfBirth-day').fill('12');
    await page.locator('#dateOfBirth-month').fill('3');
    await page.locator('#dateOfBirth-year').fill('1985');
    await page.getByLabel('Email address').fill('jane.smith@example.com');
    await page.locator('select#requestType').selectOption('Data subject access request');
    await page.getByLabel('Tell us more about your request').fill(
      'I would like to request a copy of all personal data you hold about me, in accordance with GDPR Article 15.'
    );
    await page.getByRole('radio', { name: 'Urgent (2 working days)' }).check();
    await step(page, '02-form-filled.png', {
      url: /\/request-information/,
      heading: 'Tell us about yourself'
    }, 'information-request');

    await page.getByRole('button', { name: 'Submit' }).click();
    await step(page, '03-under-review.png', {
      url: /\/request-information/,
      heading: 'Your request is being reviewed'
    }, 'information-request');

    // Explicit success state assertion: confirm body content, not just the heading.
    await expect(page.locator('body')).toContainText("We've received your submission");
    await expect(page.locator('body')).toContainText("no further action is needed right now");
  });

  test('validation: submitting without required fields shows error summary', async ({ page }) => {
    await signIn(page);
    await page.goto('/request-information');

    await assertHealthyPage(page, { url: /\/request-information/, heading: 'Tell us about yourself' });

    // Submit without filling any required fields
    await page.getByRole('button', { name: 'Submit' }).click();

    const errorSummary = page.locator('[role="alert"]').first();
    await expect(errorSummary).toBeVisible();
    await expect(errorSummary).toContainText('There is a problem');
    await expect(page.locator('.govuk-error-message').first()).toBeVisible();

    // URL should remain on the form page
    await expect(page).toHaveURL(/\/request-information/);
  });

  test('persistence: returning user sees under-review state after submission', async ({ page }) => {
    await signIn(page);
    await page.goto('/request-information');

    await assertHealthyPage(page, { url: /\/request-information/, heading: 'Tell us about yourself' });

    // Fill minimum required fields and submit
    await page.getByLabel('First name').fill('Jane');
    await page.getByLabel('Last name').fill('Smith');
    await page.locator('#dateOfBirth-day').fill('12');
    await page.locator('#dateOfBirth-month').fill('3');
    await page.locator('#dateOfBirth-year').fill('1985');
    await page.getByLabel('Email address').fill('jane.smith@example.com');
    await page.locator('select#requestType').selectOption('Complaint');
    await page.getByLabel('Tell us more about your request').fill('Testing state persistence after submission.');
    await page.getByRole('radio', { name: 'Standard (5-7 working days)' }).check();
    await page.getByRole('button', { name: 'Submit' }).click();

    // Verify submission success
    await expect(page.getByRole('heading', { name: 'Your request is being reviewed' })).toBeVisible({ timeout: 30_000 });

    // Navigate away
    await page.goto('/my-workflows');
    await expect(page.getByRole('heading', { name: 'My Workflows' })).toBeVisible();

    // Navigate back — instance policy means the under-review state persists
    await page.goto('/request-information');
    await expect(page.getByRole('heading', { name: 'Your request is being reviewed' })).toBeVisible({ timeout: 30_000 });
    await expect(page.locator('body')).not.toContainText('Tell us about yourself');
  });
});
