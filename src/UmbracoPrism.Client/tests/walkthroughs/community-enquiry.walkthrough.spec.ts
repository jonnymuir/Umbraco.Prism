// Executable counterpart of docs/walkthroughs/community-enquiry.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { assertHealthyPage, openDashboard, step, signIn, resetServiceBlueprints } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Community enquiry walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await resetServiceBlueprints(request);
  });

  test('happy path: user opens the authored community enquiry start state and submits it', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');

    await step(page, '01-initial.png', {
      url: /\/get-in-touch/,
      heading: 'Your details'
    }, 'community-enquiry');

    // 1. Target the dropdown by its accessible name (ignoring the "(required)" suffix wrapper if needed)
    await page.getByRole('combobox', { name: 'Your role' }).selectOption('Developer');

    // 2. Check the specific radio button by its own literal label text
    await page.getByLabel('General enquiry').check();

    // 3. Fill the textarea by matching its exact label or accessible role name
    await page.getByRole('textbox', { name: 'Tell us more' }).fill('I have a question about the service.');

    await page.getByRole('button', { name: 'Submit' }).click();
    await step(page, '02-submitted.png', {
      url: /\/get-in-touch/,
      heading: 'Thank you'
    }, 'community-enquiry');
  });

  test('dashboard entry stays aligned with the four-service-blueprint demo contract', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    for (const title of ['Get in Touch', 'Apply for Planning Permission', 'Payment Demo', 'Request Information']) {
      await expect(page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first()).toBeVisible();
    }

    await serviceBlueprintDemoCard(page, 'Get in Touch').getByRole('link', { name: 'Start' }).click();
    await assertHealthyPage(page, { url: /\/get-in-touch/, heading: 'Your details' });
  });

  test('single-instance flow keeps the completed state when the member returns', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');
    // Fill out required form fields so submission actually succeeds
    await page.getByRole('combobox', { name: 'Your role' }).selectOption('Developer');
    await page.getByLabel('General enquiry').check();
    await page.getByRole('textbox', { name: 'Tell us more' }).fill('Testing single instance flow persistence.');

    await page.getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({ timeout: 30_000 });

    await page.goto('/my-service-requests');
    await expect(page.getByRole('heading', { name: 'My Service Requests' })).toBeVisible();
    await expect(page.locator('[data-blueprint-key="community-enquiry"]')).toContainText('Thank you');

    await page.goto('/get-in-touch');
    await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({ timeout: 30_000 });
  });

  test('service blueprint hub lists the completed community enquiry for the signed-in member', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');
    await page.getByRole('combobox', { name: 'Your role' }).selectOption('Developer');
    await page.getByLabel('General enquiry').check();
    await page.getByRole('textbox', { name: 'Tell us more' }).fill('Testing single instance flow persistence.');
    
    await page.getByRole('button', { name: 'Submit' }).click();

    await page.goto('/my-service-requests');
    await expect(page.getByRole('heading', { name: 'My Service Requests' })).toBeVisible();

    const serviceBlueprintCard = page.locator('[data-blueprint-key="community-enquiry"]').first();
    await expect(serviceBlueprintCard).toContainText('Get in Touch');
    await expect(serviceBlueprintCard).toContainText('Thank you');
    await expect(serviceBlueprintCard.getByRole('link', { name: 'View' })).toBeVisible();
  });
});

function serviceBlueprintDemoCard(page: import('@playwright/test').Page, title: string) {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}
