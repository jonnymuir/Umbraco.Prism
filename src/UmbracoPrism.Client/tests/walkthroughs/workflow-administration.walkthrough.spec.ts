// Executable counterpart of docs/walkthroughs/workflow-administration.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect, type Page } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import {
  assertHealthyPage,
  businessAppOrigin,
  openDashboard,
  openWorkflowAdminFromDashboard,
  resetWorkflows,
  signIn,
  step
} from './support/walkthrough';

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

  test('authenticated member sees the workflow admin entry point beside core dashboard actions', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    await step(page, '01-dashboard-admin-link.png', {
      url: /\/dashboard\/?$/,
      heading: /dashboard/i,
      skipHeading: true,
      screenshotSelector: '.dash-section:has(a[href*="/admin/workflow"])'
    }, 'workflow-administration');

    await expect(page.getByRole('link', { name: 'View Workflows' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Workflow Demos' })).toBeVisible();
    await expect(workflowDemoCard(page, 'Get in Touch').getByRole('link', { name: 'Start' })).toBeVisible();

    const adminLink = page.getByRole('link', { name: 'Open Admin' });
    await expect(adminLink).toBeVisible();
    await expect(adminLink).toHaveAttribute('href', `${businessAppOrigin}/admin/workflow`);
  });

  test('workflow admin shows under-review instances and reviewer actions', async ({ page }) => {
    await signIn(page);
    await submitCommunityEnquiry(page, 'Operator-adjacent workflow coverage for the walkthrough suite.');
    await openDashboard(page);
    const adminPage = await openWorkflowAdminFromDashboard(page);

    const row = workflowInstanceRow(adminPage, 'community-enquiry');
    await expect(row).toContainText('Your enquiry is with us');
    await expect(row.getByRole('button', { name: 'Approve' })).toBeVisible();
    await expect(row.getByRole('button', { name: 'Request Changes' })).toBeVisible();

    await step(adminPage, '02-admin-instance-list.png', {
      url: /https:\/\/localhost:7245\/admin\/workflow\/?$/,
      heading: /workflow admin/i,
      screenshotSelector: 'tbody tr[data-workflow-key="community-enquiry"]'
    }, 'workflow-administration');
  });

  test('workflow admin definitions can be expanded and edited in-place', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);
    const adminPage = await openWorkflowAdminFromDashboard(page);

    const definitionCard = adminPage.locator('.def-card').filter({ hasText: 'Get in Touch' }).first();
    await expect(definitionCard).toBeVisible();

    const header = definitionCard.locator('.def-header');
    await expect(header).toHaveAttribute('aria-expanded', 'false');
    await header.click();
    await expect(header).toHaveAttribute('aria-expanded', 'true');
    await expect(definitionCard.getByText('States (')).toBeVisible();
    await expect(definitionCard.getByText('Transitions (')).toBeVisible();

    await step(adminPage, '03-admin-definition-editor.png', {
      url: /https:\/\/localhost:7245\/admin\/workflow\/?$/,
      heading: /workflow admin/i,
      screenshotSelector: '.def-card.open'
    }, 'workflow-administration');

    await definitionCard.locator('button.btn-edit').click();
    await expect(adminPage.locator('#json-modal')).toBeVisible();
    await expect(adminPage.getByRole('button', { name: 'Apply Changes' })).toBeVisible();
    await adminPage.getByRole('button', { name: 'Cancel' }).click();
    await expect(adminPage.locator('#json-modal')).toBeHidden();
  });

  test('reviewer actions can request changes and then approve a resubmitted enquiry', async ({ page }) => {
    const originalMessage = 'Please review this submission, then send it back for changes.';
    const updatedMessage = 'This enquiry has now been updated after reviewer feedback.';

    await signIn(page);
    await submitCommunityEnquiry(page, originalMessage);
    await openDashboard(page);
    const adminPage = await openWorkflowAdminFromDashboard(page);

    await workflowInstanceRow(adminPage, 'community-enquiry').getByRole('button', { name: 'Request Changes' }).click();
    await adminPage.waitForURL(/https:\/\/localhost:7245\/admin\/workflow\/?$/, { timeout: 30_000 });
    await expect(workflowInstanceRow(adminPage, 'community-enquiry')).toContainText('Tell us about your enquiry');

    await page.goto('/get-in-touch');
    await assertHealthyPage(page, { url: /\/get-in-touch/, heading: 'Tell us about your enquiry' });
    await expect(page.getByLabel('Tell us more')).toHaveValue(originalMessage);
    await page.getByLabel('Tell us more').fill(updatedMessage);
    await page.getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByRole('heading', { name: 'Your enquiry is with us' })).toBeVisible();

    await openDashboard(page);
    const approvalAdminPage = await openWorkflowAdminFromDashboard(page);
    await workflowInstanceRow(approvalAdminPage, 'community-enquiry').getByRole('button', { name: 'Approve' }).click();
    await approvalAdminPage.waitForURL(/https:\/\/localhost:7245\/admin\/workflow\/?$/, { timeout: 30_000 });
    await expect(workflowInstanceRow(approvalAdminPage, 'community-enquiry')).toContainText("We're in touch!");

    await page.goto('/get-in-touch');
    await expect(page.getByText('Enquiry received')).toBeVisible();
    await expect(page.getByText("We've sent you a confirmation email.")).toBeVisible();
  });
});

async function submitCommunityEnquiry(page: Page, message: string): Promise<void> {
  await page.goto('/get-in-touch');
  await assertHealthyPage(page, { url: /\/get-in-touch/, heading: 'Tell us about your enquiry' });

  await expect(page.getByLabel('Full name')).toHaveValue('Demo User');
  await expect(page.getByLabel('Email address')).toHaveValue('demo@prism.local');

  await page.locator('select#your-role').selectOption('Developer');
  await page.getByRole('radio', { name: 'General enquiry' }).check();
  await page.getByLabel('Tell us more').fill(message);
  await page.getByRole('button', { name: 'Submit' }).click();

  await expect(page.getByRole('heading', { name: 'Your enquiry is with us' })).toBeVisible({ timeout: 30_000 });
}

function workflowInstanceRow(page: Page, workflowKey: string) {
  return page.locator(`tbody tr[data-workflow-key="${workflowKey}"]`).first();
}

function workflowDemoCard(page: Page, title: string) {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}
