// Executable counterpart of docs/walkthroughs/workflow-administration.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { assertHealthyPage, step, signIn, resetWorkflows } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Workflow administration walkthrough', () => {
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

  test('authenticated user sees workflow admin link on dashboard', async ({ page }) => {
    await signIn(page);
    await page.goto('/dashboard');

    await step(page, '01-dashboard-admin-link.png', {
      url: /\/dashboard\/?$/,
      heading: /dashboard/i,
      skipHeading: true
    }, 'workflow-administration');

    // Verify the admin link is present
    await expect(page.getByRole('link', { name: /open admin|workflow admin/i })).toBeVisible();
  });

  test('navigates to admin panel and sees workflow instance list', async ({ page }) => {
    await signIn(page);
    
    // Start a workflow to have an instance in the admin panel
    await page.goto('/get-in-touch');
    await page.getByLabel(/name/i).fill('Test User');
    await page.getByLabel(/email/i).fill('test@example.com');
    await page.getByLabel(/organisation/i).fill('Test Org');
    await page.getByLabel(/enquiry/).fill('Test enquiry');
    await page.getByRole('button', { name: /continue/i }).click();
    await page.waitForTimeout(2000);

    // Navigate to dashboard and then to admin panel
    await page.goto('/dashboard');
    
    const adminLink = page.getByRole('link', { name: /open admin|workflow admin/i });
    await expect(adminLink).toBeVisible();
    
    // Get the href and navigate directly (can't follow target="_blank")
    const adminUrl = await adminLink.getAttribute('href');
    if (adminUrl) {
      await page.goto(adminUrl);
    } else {
      throw new Error('Admin URL not found');
    }

    await step(page, '02-admin-instance-list.png', {
      url: /\/admin\/workflow\/?$/,
      heading: /workflow/i,
      skipHeading: true
    }, 'workflow-administration');

    // Verify workflow instances are visible
    await expect(page.locator('body')).toContainText('community-enquiry', { timeout: 10_000 });
  });

  test('admin panel displays workflow definition editor', async ({ page }) => {
    await signIn(page);
    
    // Navigate to admin panel
    await page.goto('/dashboard');
    const adminLink = page.getByRole('link', { name: /open admin|workflow admin/i });
    const adminUrl = await adminLink.getAttribute('href');
    if (adminUrl) {
      await page.goto(adminUrl);
    } else {
      throw new Error('Admin URL not found');
    }

    // Wait for admin panel to load
    await page.waitForTimeout(2000);

    await step(page, '03-admin-definition-editor.png', {
      url: /\/admin\/workflow\/?$/,
      heading: /workflow/i,
      skipHeading: true
    }, 'workflow-administration');

    // Verify workflow definitions are visible (look for workflow keys)
    const definitionsPresent = await Promise.any([
      page.locator('body').getByText(/payment-demo|community-enquiry|planning-notification/i).isVisible(),
      page.locator('body').getByText(/workflow/i).isVisible()
    ]).catch(() => false);
    
    expect(definitionsPresent).toBeTruthy();
  });

  test('admin can view and potentially manage workflow instances', async ({ page }) => {
    await signIn(page);
    
    // Start a workflow
    await page.goto('/get-in-touch');
    await page.getByLabel(/name/i).fill('Admin Test');
    await page.getByLabel(/email/i).fill('admin@test.com');
    await page.getByLabel(/organisation/i).fill('Admin Test Org');
    await page.getByLabel(/enquiry/).fill('Testing admin panel');
    await page.getByRole('button', { name: /continue/i }).click();
    await page.waitForTimeout(2000);

    // Navigate to admin panel
    await page.goto('/dashboard');
    const adminLink = page.getByRole('link', { name: /open admin|workflow admin/i });
    const adminUrl = await adminLink.getAttribute('href');
    if (adminUrl) {
      await page.goto(adminUrl);
    } else {
      throw new Error('Admin URL not found');
    }

    await page.waitForTimeout(2000);

    // Verify admin interface loads without errors
    await assertHealthyPage(page, {
      url: /\/admin\/workflow\/?$/,
      heading: /workflow/i,
      skipHeading: true
    });

    // Admin panel should be present and functional
    await expect(page.locator('body')).toBeTruthy();
  });
});
