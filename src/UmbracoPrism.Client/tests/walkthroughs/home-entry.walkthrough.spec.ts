// Executable counterpart of docs/walkthroughs/home-entry.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { assertHealthyPage, step, signIn } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Home entry walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('unauthenticated user sees homepage hero with sign-in call-to-action', async ({ page }) => {
    await page.goto('/');

    await step(page, '01-signed-out-hero.png', {
      url: /localhost:44345\/?$/,
      heading: /Your account, your way/i
    }, 'home-entry');

    await expect(page.getByRole('link', { name: 'Sign In' })).toBeVisible();
    // Dashboard and workflow links should not be present for unauthenticated users
    await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toHaveCount(0);
  });

  test('authenticated user sees personalised hero with dashboard link', async ({ page }) => {
    await signIn(page);

    await step(page, '02-signed-in-hero.png', {
      url: /localhost:44345\/?$/,
      heading: /Your account, your way/i,
      skipHeading: true // heading may vary; assert personalised content directly below
    }, 'home-entry');

    await expect(page.getByText('Welcome back, Demo User')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign Out' }).first()).toBeVisible();
    // Sign In link should no longer be present
    await expect(page.getByRole('link', { name: 'Sign In' })).toHaveCount(0);
  });

  test('authenticated user navigates from homepage to dashboard and workflow hub', async ({ page }) => {
    await signIn(page);

    // Start from home
    await assertHealthyPage(page, {
      url: /localhost:44345\/?$/,
      heading: /Your account, your way/i,
      skipHeading: true
    });
    await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible();

    // Navigate to dashboard
    await page.getByRole('link', { name: 'Go to Dashboard' }).click();
    await page.waitForURL(/\/dashboard\/?$/, { timeout: 30_000 });

    await step(page, '03-dashboard.png', {
      url: /\/dashboard\/?$/,
      heading: /dashboard/i,
      skipHeading: true // dashboard heading varies; assert affordances directly
    }, 'home-entry');

    await expect(page.getByRole('link', { name: 'View Workflows' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Start Workflow' })).toBeVisible();

    // Navigate to workflow hub via View Workflows
    await page.getByRole('link', { name: 'View Workflows' }).click();
    await page.waitForURL(/\/my-workflows\/?$/, { timeout: 30_000 });

    await step(page, '04-workflow-hub.png', {
      url: /\/my-workflows\/?$/,
      heading: 'My Workflows'
    }, 'home-entry');
  });
});
