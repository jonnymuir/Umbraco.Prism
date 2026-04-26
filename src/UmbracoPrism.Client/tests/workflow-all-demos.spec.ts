import { test, expect, type Page } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();
const demoCredentials = {
  username: 'demo@prism.local',
  password: 'password'
};

test.describe('All workflow demos end-to-end coverage', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    // Reset all workflow instances before each test
    await request.delete('https://localhost:7245/api/test/reset', {
      ignoreHTTPSErrors: true
    });
  });

  test.describe('Community enquiry workflow', () => {
    test('happy path: user submits a general enquiry', async ({ page }) => {
      await signIn(page);
      await page.goto('/get-in-touch');

      // State: collecting-details
      await expect(page.getByRole('heading', { name: 'Tell us about your enquiry' })).toBeVisible();
      
      // About You fieldset
      await page.getByLabel('Full name').fill('Jane Doe');
      await page.getByLabel('Email address').fill('jane.doe@example.com');
      await page.getByLabel('Organisation (optional)').fill('Acme Corp');
      await page.locator('select#your-role').selectOption('Developer');

      // Your Enquiry fieldset
      await page.getByRole('radio', { name: 'General enquiry' }).check();

      // Your Message fieldset
      await page.getByLabel('Tell us more').fill('I would like to learn more about Prism integration options for our Umbraco site. We are currently evaluating multi-tenant solutions.');
      await page.getByRole('checkbox', { name: 'Umbraco CMS' }).check();
      await page.getByRole('checkbox', { name: '.NET Development' }).check();
      await page.getByRole('checkbox', { name: 'Keep me updated with Prism news and releases' }).check();

      await page.getByRole('button', { name: 'Submit' }).click();

      // State: under-review
      await expect(page.getByRole('heading', { name: 'Your enquiry is with us' })).toBeVisible();
      await expect(page.getByText(/it's currently being reviewed/i)).toBeVisible();

      // Verify v2 component partials are rendering (GOV.UK classes)
      const body = page.locator('body');
      await expect(body.locator('.govuk-fieldset')).toHaveCount(await body.locator('.govuk-fieldset').count());
    });

    test('conditional reveal: Other enquiry type shows sub-field', async ({ page }) => {
      await signIn(page);
      await page.goto('/get-in-touch');

      await page.getByLabel('Full name').fill('Test User');
      await page.getByLabel('Email address').fill('test@example.com');
      await page.locator('select#your-role').selectOption('Other');

      // Select "Other" enquiry type - should reveal conditional field
      await page.getByRole('radio', { name: 'Other' }).check();
      
      const conditionalField = page.getByLabel('Please specify your enquiry type');
      await expect(conditionalField).toBeVisible();
      await conditionalField.fill('Partnership enquiry');

      // Switch back and verify field is hidden
      await page.getByRole('radio', { name: 'General enquiry' }).check();
      await expect(conditionalField).toBeHidden();
    });
  });

  test.describe('Payment demo workflow', () => {
    test('happy path: user enters payment details and processes', async ({ page }) => {
      await signIn(page);
      await page.goto('/payment-demo');

      // State: enter-details
      await expect(page.getByRole('heading', { name: 'Enter Payment Details' })).toBeVisible();
      
      await page.getByLabel('Cardholder name').fill('Jane Doe');
      await page.getByLabel('Amount (£)').fill('42.50');

      await page.getByRole('button', { name: 'Submit' }).click();

      // State: processing-payment (Waiting component)
      await expect(page.getByRole('heading', { name: 'Processing Your Payment' })).toBeVisible();
      await expect(page.getByText(/processing your payment with our secure payment provider/i)).toBeVisible();
      
      // The Waiting component should be rendered via _Component-Waiting.cshtml
      await expect(page.locator('[data-component-type="waiting"]')).toBeVisible();
    });

    test('validation: minimum decimal value enforced', async ({ page }) => {
      await signIn(page);
      await page.goto('/payment-demo');

      await page.getByLabel('Cardholder name').fill('Test User');
      await page.getByLabel('Amount (£)').fill('0'); // Below min of 0.01

      await page.getByRole('button', { name: 'Submit' }).click();

      // Should show validation error
      const errorSummary = page.locator('[role="alert"]').first();
      await expect(errorSummary).toBeVisible();
      await expect(errorSummary).toContainText('There is a problem');
    });
  });

  test.describe('Planning notification workflow', () => {
    test('happy path: user completes multi-step planning application', async ({ page }) => {
      await signIn(page);
      await page.goto('/apply-for-planning-permission');

      // State: project-details
      await expect(page.getByRole('heading', { name: 'Describe your project' })).toBeVisible();
      await page.getByLabel('Project name').fill('Loft conversion');
      await page.getByLabel('Describe the proposed works').fill('Converting existing loft space into habitable bedroom with dormer window');
      await page.getByLabel('Property address').fill('456 Oak Avenue\nWoodlands\nWD3 4EF');
      await page.getByRole('button', { name: 'Continue' }).click();

      // State: work-type
      await expect(page.getByRole('heading', { name: 'Type of work' })).toBeVisible();
      await page.getByRole('radio', { name: 'Extension or alteration' }).check();
      await page.getByRole('radio', { name: 'Yes' }).first().check();
      await page.getByRole('button', { name: 'Continue' }).click();

      // State: timeline-cost
      await expect(page.getByRole('heading', { name: 'Timeline and cost' })).toBeVisible();
      await page.locator('#proposedStartDate-day').fill('1');
      await page.locator('#proposedStartDate-month').fill('9');
      await page.locator('#proposedStartDate-year').fill('2025');
      await page.getByLabel('Estimated duration in weeks').fill('16');
      await page.getByLabel('Estimated cost of works').fill('35000.75'); // Test decimal input
      await page.getByRole('button', { name: 'Continue' }).click();

      // State: affected-parties
      await expect(page.getByRole('heading', { name: 'Affected parties' })).toBeVisible();
      await page.getByRole('checkbox', { name: 'Neighbouring properties' }).check();
      await page.getByRole('checkbox', { name: 'Conservation area' }).check();
      await page.getByRole('radio', { name: 'Yes' }).last().check();
      await page.getByRole('button', { name: 'Continue' }).click();

      // State: check-answers (SummaryList components)
      await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible();
      
      // Verify summary list renders entered data
      const summaryList = page.locator('.govuk-summary-list');
      await expect(summaryList.getByText('Loft conversion')).toBeVisible();
      await expect(summaryList.getByText('£35000.75')).toBeVisible();
      await expect(summaryList.getByText('Neighbouring properties, Conservation area')).toBeVisible();

      await page.getByRole('button', { name: 'Submit' }).click();

      // State: complete (Panel component)
      await expect(page.locator('.govuk-panel--confirmation')).toBeVisible();
      await expect(page.getByRole('heading', { name: 'Application received' })).toBeVisible();
    });

    test('conditional reveal: Other work type shows textarea', async ({ page }) => {
      await signIn(page);
      await page.goto('/apply-for-planning-permission');

      // Navigate to work-type state
      await page.getByLabel('Project name').fill('Test');
      await page.getByLabel('Describe the proposed works').fill('Test');
      await page.getByLabel('Property address').fill('Test');
      await page.getByRole('button', { name: 'Continue' }).click();

      await expect(page.getByRole('heading', { name: 'Type of work' })).toBeVisible();

      // Select "Other" - should reveal conditional textarea from ConditionalChildren
      await page.getByRole('radio', { name: 'Other' }).check();
      
      const conditionalTextarea = page.getByLabel('Describe the type of work');
      await expect(conditionalTextarea).toBeVisible();
      await conditionalTextarea.fill('Listed building restoration with specialist masonry');

      // Switch to different option
      await page.getByRole('radio', { name: 'New building' }).check();
      await expect(conditionalTextarea).toBeHidden();
    });
  });

  test.describe('Information request workflow', () => {
    test('happy path: user submits information request', async ({ page }) => {
      await signIn(page);
      await page.goto('/request-information');

      // State: collecting-info
      await expect(page.getByRole('heading', { name: 'Tell us about yourself' })).toBeVisible();
      
      // Your details fieldset
      await page.getByLabel('First name').fill('Jane');
      await page.getByLabel('Last name').fill('Smith');
      await page.locator('#dateOfBirth-day').fill('12');
      await page.locator('#dateOfBirth-month').fill('3');
      await page.locator('#dateOfBirth-year').fill('1985');
      await page.getByLabel('Email address').fill('jane.smith@example.com');

      // Your request fieldset
      await page.locator('select#requestType').selectOption('General enquiry');
      await page.getByLabel('Tell us more about your request').fill('I would like to request information about my previous applications submitted through this portal.');
      await page.getByRole('radio', { name: 'Standard (5-7 working days)' }).check();

      await page.getByRole('button', { name: 'Submit' }).click();

      // State: under-review
      await expect(page.getByRole('heading', { name: 'Your request is being reviewed' })).toBeVisible();
      await expect(page.getByText(/we've received your submission and it's currently being reviewed/i)).toBeVisible();
    });
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
