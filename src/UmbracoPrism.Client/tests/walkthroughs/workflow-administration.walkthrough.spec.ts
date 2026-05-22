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
    for (const workflowTitle of ['Get in Touch', 'Apply for Planning Permission', 'Payment Demo', 'Request Information']) {
      await expect(workflowDemoCard(page, workflowTitle)).toBeVisible();
      await expect(workflowDemoCard(page, workflowTitle).getByRole('link', { name: 'Start' })).toBeVisible();
    }

    const adminLink = page.getByRole('link', { name: 'Open Admin' });
    await expect(adminLink).toBeVisible();
    await expect(adminLink).toHaveAttribute('href', `${businessAppOrigin}/admin/workflow`);

    const editorCard = page.locator('.dash-card').filter({
      has: page.getByRole('heading', { name: 'Workflow Editor' })
    }).first();
    await expect(editorCard).toBeVisible();
    await expect(editorCard.getByRole('link', { name: 'Open Editor' })).toHaveAttribute(
      'href',
      `${businessAppOrigin}/workflow-editor`
    );
    await expect(editorCard.getByText('Direct Page')).toHaveCount(0);
  });

  test('workflow admin lists the four reference workflows and editor links', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);
    const adminPage = await openWorkflowAdminFromDashboard(page);

    for (const workflowKey of ['community-enquiry', 'information-request', 'payment-demo', 'planning']) {
      const definitionCard = adminPage.locator(`.def-card[data-workflow-key="${workflowKey}"]`).first();
      await expect(definitionCard).toBeVisible();
      await expect(definitionCard.getByRole('link', { name: 'Edit workflow' })).toHaveAttribute(
        'href',
        `/workflow-editor?workflow=${workflowKey}`
      );
    }

    await expect(adminPage.locator('.def-card[data-workflow-key]')).toHaveCount(4);
    await expect(adminPage.getByText('No editor definition yet')).toHaveCount(0);
  });

  test('workflow admin shows completed community enquiries without obsolete reviewer actions', async ({ page }) => {
    await signIn(page);
    await submitCommunityEnquiry(page);
    await openDashboard(page);
    const adminPage = await openWorkflowAdminFromDashboard(page);

    const row = workflowInstanceRow(adminPage, 'community-enquiry');
    await expect(row).toContainText('Thank you');
    await expect(row.getByRole('button', { name: 'Approve' })).toHaveCount(0);
    await expect(row.getByRole('button', { name: 'Request Changes' })).toHaveCount(0);

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
});

async function submitCommunityEnquiry(page: Page): Promise<void> {
  await page.goto('/get-in-touch');
  await assertHealthyPage(page, { url: /\/get-in-touch/, heading: 'Your details' });
  await page.getByRole('button', { name: 'Submit' }).click();

  await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({ timeout: 30_000 });
}

function workflowInstanceRow(page: Page, workflowKey: string) {
  return page.locator(`tbody tr[data-workflow-key="${workflowKey}"]`).first();
}

function workflowDemoCard(page: Page, title: string) {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}
