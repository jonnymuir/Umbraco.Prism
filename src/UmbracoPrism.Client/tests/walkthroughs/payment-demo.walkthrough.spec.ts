// Executable counterpart of docs/walkthroughs/payment-demo.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect, type Page } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import {
  assertHealthyPage,
  openDashboard,
  openWorkflowAdminFromDashboard,
  resetWorkflows,
  signIn,
  step
} from './support/walkthrough';

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

  test('happy path: member submits payment, reviewer completes it, and the waiting page advances automatically', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    const paymentCard = workflowDemoCard(page, 'Payment Demo');
    await expect(paymentCard.getByRole('link', { name: 'Start' })).toBeVisible();
    await paymentCard.scrollIntoViewIfNeeded();
    await step(page, '01-dashboard-payment-demo-start.png', {
      url: /\/dashboard\/?$/,
      heading: /dashboard/i,
      skipHeading: true,
      screenshotSelector: '.dash-card--cta:has(a[href$="/payment-demo"])'
    }, 'payment-demo');

    await paymentCard.getByRole('link', { name: 'Start' }).click();
    await step(page, '02-initial.png', {
      url: /\/payment-demo\/?$/,
      heading: 'Enter Payment Details'
    }, 'payment-demo');

    await page.getByLabel('Cardholder name').fill('Jane Doe');
    await page.getByLabel('Amount (£)').fill('42.50');
    await step(page, '03-form-filled.png', {
      url: /\/payment-demo\/?$/,
      heading: 'Enter Payment Details'
    }, 'payment-demo');

    await page.getByRole('button', { name: 'Submit' }).click();
    await step(page, '04-processing.png', {
      url: /\/payment-demo\/?$/,
      heading: 'Processing Your Payment'
    }, 'payment-demo');
    await expect(page.locator('body')).toContainText('You can leave this page', { timeout: 10_000 });

    const workflowHubPage = await page.context().newPage();
    await workflowHubPage.goto('/my-workflows');
    await step(workflowHubPage, '05-workflow-hub-processing.png', {
      url: /\/my-workflows\/?$/,
      heading: 'My Workflows'
    }, 'payment-demo');

    const hubCard = workflowHubCard(workflowHubPage, 'Payment Demo');
    await expect(hubCard).toContainText('Processing Your Payment');
    await expect(hubCard.getByRole('link', { name: 'Continue' })).toBeVisible();

    const reviewerJourneyPage = await page.context().newPage();
    await openDashboard(reviewerJourneyPage);

    const adminCard = dashboardCard(reviewerJourneyPage, 'Workflow Admin');
    await expect(adminCard.getByRole('link', { name: 'Open Admin' })).toBeVisible();
    await adminCard.scrollIntoViewIfNeeded();
    await step(reviewerJourneyPage, '06-dashboard-admin-link.png', {
      url: /\/dashboard\/?$/,
      heading: /dashboard/i,
      skipHeading: true,
      screenshotSelector: '.dash-section:has(a[href*="/admin/workflow"])'
    }, 'payment-demo');

    const adminPage = await openWorkflowAdminFromDashboard(reviewerJourneyPage);
    const instanceRow = workflowInstanceRow(adminPage, 'payment-demo');
    await expect(instanceRow).toContainText('Processing Your Payment');
    await expect(instanceRow).toContainText('processing-payment');
    await expect(instanceRow.getByRole('button', { name: 'Complete' })).toBeVisible();
    await step(adminPage, '07-admin-processing-instance.png', {
      url: /https:\/\/localhost:7245\/admin\/workflow\/?$/,
      heading: /workflow admin/i,
      screenshotSelector: 'tbody tr[data-workflow-key="payment-demo"]'
    }, 'payment-demo');

    const definitionCard = adminPage.locator('[data-definition-key="payment-demo"]').first();
    await expect(definitionCard).toBeVisible();
    await definitionCard.locator('.def-header').click();
    await expect(definitionCard.locator('.def-header')).toHaveAttribute('aria-expanded', 'true');
    await expect(definitionCard).toContainText('processing-payment');
    await expect(definitionCard).toContainText('payment-complete');
    await expect(definitionCard).toContainText('complete');
    await definitionCard.scrollIntoViewIfNeeded();
    await step(adminPage, '08-admin-payment-definition.png', {
      url: /https:\/\/localhost:7245\/admin\/workflow\/?$/,
      heading: /workflow admin/i,
      screenshotSelector: '[data-definition-key="payment-demo"]'
    }, 'payment-demo');

    await instanceRow.getByRole('button', { name: 'Complete' }).click();
    await expect(workflowInstanceRow(adminPage, 'payment-demo')).toContainText('Payment Complete', { timeout: 30_000 });
    await expect(workflowInstanceRow(adminPage, 'payment-demo')).toContainText('payment-complete');

    await page.bringToFront();
    await expect(page.getByText('Payment received')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('body')).toContainText('A receipt has been sent to your email address.', { timeout: 20_000 });
    await step(page, '09-payment-complete.png', {
      url: /\/payment-demo\/?$/,
      heading: /payment/i,
      skipHeading: true
    }, 'payment-demo');

    await page.getByRole('link', { name: 'My Workflows' }).click();
    await assertHealthyPage(page, { url: /\/my-workflows\/?$/, heading: 'My Workflows' });
    const completedCard = workflowHubCard(page, 'Payment Demo');
    await expect(page.getByRole('heading', { name: 'Completed' })).toBeVisible();
    await expect(completedCard).toContainText('Payment Complete');
    await expect(completedCard.getByRole('link', { name: 'View' })).toBeVisible();
  });

  test('validation: minimum decimal value enforced', async ({ page }) => {
    await signIn(page);
    await page.goto('/payment-demo');

    await page.getByLabel('Cardholder name').fill('Test User');
    await page.getByLabel('Amount (£)').fill('0');
    await page.getByRole('button', { name: 'Submit' }).click();

    const errorSummary = page.locator('[role="alert"]').first();
    await expect(errorSummary).toBeVisible();
    await expect(errorSummary).toContainText('There is a problem');
  });

  test('processing state: defer option visible and returning user sees processing', async ({ page }) => {
    await signIn(page);
    await page.goto('/payment-demo');

    await assertHealthyPage(page, { url: /\/payment-demo\/?$/, heading: 'Enter Payment Details' });

    await page.getByLabel('Cardholder name').fill('Jane Doe');
    await page.getByLabel('Amount (£)').fill('25.00');
    await page.getByRole('button', { name: 'Submit' }).click();

    await expect(page.getByRole('heading', { name: 'Processing Your Payment' })).toBeVisible({ timeout: 30_000 });
    await expect(page.locator('body')).toContainText('You can leave this page', { timeout: 10_000 });

    await page.goto('/my-workflows');
    await expect(page.getByRole('heading', { name: 'My Workflows' })).toBeVisible();

    await page.goto('/payment-demo');
    await expect(page.getByRole('heading', { name: 'Processing Your Payment' })).toBeVisible({ timeout: 30_000 });
    await expect(page.locator('body')).not.toContainText('Enter Payment Details');
  });
});

function workflowDemoCard(page: Page, title: string) {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}

function dashboardCard(page: Page, title: string) {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}

function workflowHubCard(page: Page, title: string) {
  return page.locator(`.prism-instance-card[data-workflow-key="payment-demo"]`).filter({
    has: page.getByRole('heading', { name: title })
  }).first();
}

function workflowInstanceRow(page: Page, workflowKey: string) {
  return page.locator(`tbody tr[data-workflow-key="${workflowKey}"]`).first();
}
