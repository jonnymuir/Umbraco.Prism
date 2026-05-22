import { test, expect, type Page } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();
const demoCredentials = {
  username: 'demo@prism.local',
  password: 'password'
};

test.describe('Planning workflow behavioural contracts', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await request.delete('https://localhost:7245/api/test/reset', {
      ignoreHTTPSErrors: true
    });
  });

  test('signed-in member can complete the current planning workflow journey', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    await expect(page.getByRole('heading', { name: 'Declaration' })).toBeVisible();
    await page.getByLabel('Applicant name').fill('Garden extension');
    await page.getByLabel('Site address').fill('123 Main Street\nSpringfield\nSP1 2AB');
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Application Form' })).toBeVisible();
    await page.getByLabel('Description of proposed works').fill(
      'Single-storey rear extension with updated kitchen and dining space'
    );
    await page.getByLabel('Type of development').selectOption('Extension');
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible();

    const summaryList = page.locator('.govuk-summary-list');
    await expect(summaryList).toContainText('Garden extension');
    await expect(summaryList).toContainText('123 Main Street');
    await expect(summaryList).toContainText('Single-storey rear extension with updated kitchen and dining space');
    await expect(summaryList).toContainText('Extension');

    await page.getByRole('button', { name: 'Submit' }).click();

    await expect(page.getByRole('heading', { name: 'Application submitted' })).toBeVisible();
  });

  test('required declaration fields show validation errors', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    await expect(page.getByRole('heading', { name: 'Declaration' })).toBeVisible();
    await page.getByRole('button', { name: 'Continue' }).click();

    const errorSummary = page.locator('[role="alert"]').first();
    await expect(errorSummary).toBeVisible();
    await expect(errorSummary).toContainText('There is a problem');
    await expect(page.locator('.govuk-error-message').first()).toBeVisible();
  });

  test('workflow admin definitions deep-link to the live planning editor', async ({ page }) => {
    await page.goto('https://localhost:7245/admin/workflow');
    await expect(page).toHaveURL(/\/admin\/workflow$/);
    await expect(page.getByRole('heading', { name: /workflow admin/i })).toBeVisible();

    const cards = page.locator('.def-card');
    const planningCard = page.locator('.def-card').filter({ hasText: 'Planning Application' });
    const planningHeader = planningCard.locator('.def-header');
    const planningBody = planningCard.locator('.def-body');
    const communityEnquiryCard = page.locator('.def-card').filter({ hasText: 'Get in Touch' });

    await expect(cards).not.toHaveCount(0);
    await expect(planningCard).toBeVisible();
    await expect(planningHeader).toHaveAttribute('aria-expanded', 'false');
    await expect(planningBody).toBeHidden();
    await expect(planningCard.getByRole('link', { name: 'Edit workflow' })).toHaveAttribute(
      'href',
      '/workflow-editor?workflow=planning'
    );
    await expect(communityEnquiryCard.getByRole('link', { name: 'Edit workflow' })).toHaveAttribute(
      'href',
      '/workflow-editor?workflow=community-enquiry'
    );
    await expect(communityEnquiryCard.getByText('No editor definition yet')).toHaveCount(0);

    await planningHeader.click();
    await expect(planningHeader).toHaveAttribute('aria-expanded', 'true');
    await expect(planningBody).toBeVisible();
    await expect(planningCard).toHaveAttribute('data-mermaid-render-state', 'ready');

    await planningHeader.click();
    await expect(planningHeader).toHaveAttribute('aria-expanded', 'false');
    await expect(planningBody).toBeHidden();

    const totalCards = await cards.count();

    await page.getByRole('button', { name: 'Expand All' }).click();
    await expect(page.locator('.def-card.open')).toHaveCount(totalCards);

    await page.getByRole('button', { name: 'Collapse All' }).click();
    await expect(page.locator('.def-card.open')).toHaveCount(0);

    await planningCard.getByRole('link', { name: 'Edit workflow' }).click();
    await expect(page).toHaveURL(/\/workflow-editor\.html\?workflow=planning$/);
    await expect(page.locator('[data-prism-component="workflow-editor-shell"]')).toHaveAttribute(
      'data-prism-active-workflow',
      'planning'
    );
    await expect(page.getByText(/does not list it/i)).toHaveCount(0);
    await expect(page.locator('prism-workflow-editor')).toHaveAttribute(
      'data-prism-workflow-loaded',
      'planning'
    );
  });
});

async function signIn(page: Page): Promise<void> {
  await page.goto('/');
  await page.getByRole('link', { name: 'Sign In' }).click();

  await expect(page.locator('#username')).toBeVisible({ timeout: 120_000 });
  await page.locator('#username').fill(demoCredentials.username);
  await page.locator('#password').fill(demoCredentials.password);

  await Promise.all([
    page.waitForURL(
      url => url.origin === 'https://localhost:44345' && url.pathname !== '/signin-oidc',
      { timeout: 120_000 }
    ),
    page.locator('#kc-login').click()
  ]);

  await page.goto('/');
  await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible();
}
