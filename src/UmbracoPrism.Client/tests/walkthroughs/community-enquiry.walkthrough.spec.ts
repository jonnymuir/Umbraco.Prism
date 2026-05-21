// Executable counterpart of docs/walkthroughs/community-enquiry.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { assertHealthyPage, openDashboard, step, signIn, resetWorkflows } from './support/walkthrough';

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
    await resetWorkflows(request);
  });

  test('happy path: user opens the authored community enquiry start state and submits it', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');

    await step(page, '01-initial.png', {
      url: /\/get-in-touch/,
      heading: 'Your details'
    }, 'community-enquiry');

    await page.getByRole('button', { name: 'Submit' }).click();
    await step(page, '02-submitted.png', {
      url: /\/get-in-touch/,
      heading: 'Thank you'
    }, 'community-enquiry');
  });

  test('dashboard entry stays aligned with the four-workflow demo contract', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    for (const title of ['Get in Touch', 'Apply for Planning Permission', 'Payment Demo', 'Request Information']) {
      await expect(page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first()).toBeVisible();
    }

    await workflowDemoCard(page, 'Get in Touch').getByRole('link', { name: 'Start' }).click();
    await assertHealthyPage(page, { url: /\/get-in-touch/, heading: 'Your details' });
  });

  test('single-instance flow keeps the completed state when the member returns', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');
    await page.getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({ timeout: 30_000 });

    await page.goto('/my-workflows');
    await expect(page.getByRole('heading', { name: 'My Workflows' })).toBeVisible();
    await expect(page.locator('[data-workflow-key="community-enquiry"]')).toContainText('Thank you');

    await page.goto('/get-in-touch');
    await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({ timeout: 30_000 });
  });

  test('workflow hub lists the completed community enquiry for the signed-in member', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');
    await page.getByRole('button', { name: 'Submit' }).click();

    await page.goto('/my-workflows');
    await expect(page.getByRole('heading', { name: 'My Workflows' })).toBeVisible();

    const workflowCard = page.locator('[data-workflow-key="community-enquiry"]').first();
    await expect(workflowCard).toContainText('Get in Touch');
    await expect(workflowCard).toContainText('Thank you');
    await expect(workflowCard.getByRole('link', { name: 'View' })).toBeVisible();
  });
});

function workflowDemoCard(page: import('@playwright/test').Page, title: string) {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}
