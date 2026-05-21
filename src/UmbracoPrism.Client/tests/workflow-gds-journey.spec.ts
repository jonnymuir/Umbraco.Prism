import { test, expect, type Page } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();
const demoCredentials = {
  username: 'demo@prism.local',
  password: 'password'
};

test.describe('Planning workflow GDS journey behavioural contracts', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    // Reset all workflow instances before each test so tests start with a clean slate.
    // The MockBusinessApp /api/test/reset endpoint is test-only infrastructure.
    await request.delete('https://localhost:7245/api/test/reset', {
      ignoreHTTPSErrors: true
    });
  });

  test('signed-in member can complete the full planning workflow journey', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    // Step 1: Project details
    await expect(page.getByRole('heading', { name: 'Describe your project' })).toBeVisible();
    await page.getByLabel('Project name').fill('Garden extension');
    await page.getByLabel('Describe the proposed works').fill('Building a conservatory at the rear of the property');
    await page.getByLabel('Property address').fill('123 Main Street\nSpringfield\nSP1 2AB');
    await page.getByRole('button', { name: 'Continue' }).click();

    // Step 2: Work type
    await expect(page.getByRole('heading', { name: 'Type of work' })).toBeVisible();
    await page.getByRole('radio', { name: 'Extension or alteration' }).check();
    await page.getByRole('radio', { name: 'Yes' }).first().check();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Step 3: Timeline and cost
    await expect(page.getByRole('heading', { name: 'Timeline and cost' })).toBeVisible();
    await page.locator('#proposedStartDate-day').fill('15');
    await page.locator('#proposedStartDate-month').fill('6');
    await page.locator('#proposedStartDate-year').fill('2025');
    await page.getByLabel('Estimated duration in weeks').fill('12');
    await page.getByLabel('Estimated cost of works').fill('25000');
    await page.getByRole('button', { name: 'Continue' }).click();

    // Step 4: Affected parties
    await expect(page.getByRole('heading', { name: 'Affected parties' })).toBeVisible();
    await page.getByRole('checkbox', { name: 'Neighbouring properties' }).check();
    await page.getByRole('radio', { name: 'Yes' }).last().check();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Step 5: Check answers
    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible();
    
    // Verify all entered data is displayed
    const summaryList = page.locator('.govuk-summary-list');
    await expect(summaryList.getByText('Garden extension')).toBeVisible();
    await expect(summaryList.getByText('Building a conservatory at the rear of the property')).toBeVisible();
    await expect(summaryList.getByText('Extension or alteration')).toBeVisible();
    await expect(summaryList.getByText('15/6/2025')).toBeVisible();
    await expect(summaryList.getByText('12', { exact: true })).toBeVisible();
    await expect(summaryList.getByText('£25000')).toBeVisible();
    await expect(summaryList.getByText('Neighbouring properties')).toBeVisible();

    await page.getByRole('button', { name: 'Submit' }).click();

    // Step 6: Confirmation
    await expect(page.locator('.govuk-panel--confirmation')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Application received' })).toBeVisible();
    await expect(page.getByText(/Your reference number/i)).toBeVisible();
  });

  test('required field validation shows error summary and field error', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    await expect(page.getByRole('heading', { name: 'Describe your project' })).toBeVisible();
    
    // Submit without filling required fields
    await page.getByRole('button', { name: 'Continue' }).click();

    // Verify error summary with role="alert"
    const errorSummary = page.locator('[role="alert"]').first();
    await expect(errorSummary).toBeVisible();
    await expect(errorSummary).toContainText('There is a problem');

    // Verify field-level error message is shown
    await expect(page.locator('.govuk-error-message').first()).toBeVisible();
  });

  test('conditional radio reveals sub-fields when specific option selected', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    // Navigate to work type step
    await page.getByLabel('Project name').fill('Test project');
    await page.getByLabel('Describe the proposed works').fill('Test description');
    await page.getByLabel('Property address').fill('Test address');
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Type of work' })).toBeVisible();

    // Select "Other" which should reveal a conditional field
    await page.getByRole('radio', { name: 'Other' }).check();

    // Verify the conditional field becomes visible
    const conditionalField = page.getByLabel('Describe the type of work');
    await expect(conditionalField).toBeVisible();
    await expect(conditionalField).not.toHaveAttribute('hidden');

    // Select a different option and verify conditional field is hidden
    await page.getByRole('radio', { name: 'Extension or alteration' }).check();
    await expect(conditionalField).toBeHidden();
  });

  test('date input shows validation error for invalid date', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    // Navigate to timeline step
    await page.getByLabel('Project name').fill('Test project');
    await page.getByLabel('Describe the proposed works').fill('Test description');
    await page.getByLabel('Property address').fill('Test address');
    await page.getByRole('button', { name: 'Continue' }).click();

    await page.getByRole('radio', { name: 'Extension or alteration' }).check();
    await page.getByRole('radio', { name: 'Yes' }).first().check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Timeline and cost' })).toBeVisible();

    // Enter invalid date
    await page.locator('#proposedStartDate-day').fill('99');
    await page.locator('#proposedStartDate-month').fill('13');
    await page.locator('#proposedStartDate-year').fill('2025');
    await page.getByLabel('Estimated duration in weeks').fill('12');
    await page.getByLabel('Estimated cost of works').fill('25000');
    await page.getByRole('button', { name: 'Continue' }).click();

    // Verify validation error is shown
    const errorSummary = page.locator('[role="alert"]').first();
    await expect(errorSummary).toBeVisible();
    
    // Should have field-level error for the date (server-rendered GDS error message)
    await expect(page.locator('.govuk-error-message')).toBeVisible();
  });

  test('workflow admin definitions are collapsed by default and can be expanded', async ({ page }) => {
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

  test('check-answers allows changing an answer via Change link', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    // Complete journey to check-answers
    await page.getByLabel('Project name').fill('Original project name');
    await page.getByLabel('Describe the proposed works').fill('Original description');
    await page.getByLabel('Property address').fill('Original address');
    await page.getByRole('button', { name: 'Continue' }).click();

    await page.getByRole('radio', { name: 'New building' }).check();
    await page.getByRole('radio', { name: 'Not sure' }).first().check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await page.locator('#proposedStartDate-day').fill('1');
    await page.locator('#proposedStartDate-month').fill('7');
    await page.locator('#proposedStartDate-year').fill('2025');
    await page.getByLabel('Estimated duration in weeks').fill('8');
    await page.getByLabel('Estimated cost of works').fill('50000');
    await page.getByRole('button', { name: 'Continue' }).click();

    await page.getByRole('checkbox', { name: 'None of the above' }).check();
    await page.getByRole('radio', { name: 'Not applicable' }).check();
    await page.getByRole('button', { name: 'Continue' }).click();

    // On check-answers page
    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible();
    await expect(page.getByText('Original project name')).toBeVisible();

    // Click "Change" button for project name
    const changeLinks = page.getByRole('button', { name: /Change/ });
    await changeLinks.first().click();

    // Should navigate back to first step
    await expect(page.getByRole('heading', { name: 'Describe your project' })).toBeVisible();
    await expect(page.getByLabel('Project name')).toHaveValue('Original project name');

    // Change the value
    await page.getByLabel('Project name').fill('Updated project name');
    await page.getByRole('button', { name: 'Continue' }).click();

    // Navigate back to check-answers (workflow should remember state)
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Verify the updated value is shown
    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible();
    await expect(page.getByText('Updated project name')).toBeVisible();
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
