// Executable counterpart of docs/walkthroughs/information-request.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { step, signIn, resetWorkflows } from './support/walkthrough';

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
  });
});
