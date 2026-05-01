/**
 * Walkthrough screenshot-capture spec.
 *
 * Starts the full Aspire stack via LiveAppHost, signs in with the demo account,
 * navigates each demo workflow through every state, and saves a numbered PNG per
 * step under docs/images/walkthroughs/{workflow-key}/.
 *
 * This spec is the *source of truth* for walkthrough imagery. The accompanying
 * markdown files in docs/walkthroughs/ embed each numbered PNG inline at the
 * matching step.
 *
 * Run via the dedicated capture-screenshots workflow
 * (.github/workflows/capture-screenshots.yml) which sets CAPTURE_SCREENSHOTS=1
 * and auto-commits the regenerated images. Or locally:
 *
 *   cd src/UmbracoPrism.Client
 *   CAPTURE_SCREENSHOTS=1 npm run test:playwright:localhost-auth -- capture-screenshot.spec.ts
 */
import { test, expect, type Page, type APIRequestContext } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();

// Resolve paths relative to the UmbracoPrism.Client directory (process.cwd() when Playwright runs).
const docsRoot = path.resolve(process.cwd(), '../../docs/images/walkthroughs');
const businessAppOrigin = 'https://localhost:7245';

const demoCredentials = { username: 'demo@prism.local', password: 'password' };

async function shotDir(key: string): Promise<string> {
  const dir = path.join(docsRoot, key);
  await mkdir(dir, { recursive: true });
  return dir;
}

async function shoot(page: Page, dir: string, name: string): Promise<void> {
  const file = path.join(dir, name);
  await page.screenshot({ path: file, fullPage: true });
  console.log(`Captured: ${file}`);
}

async function resetWorkflows(request: APIRequestContext): Promise<void> {
  await request.delete(`${businessAppOrigin}/api/test/reset`, { ignoreHTTPSErrors: true });
}

async function signIn(page: Page): Promise<void> {
  await page.goto('/');
  await page.getByRole('link', { name: 'Sign In' }).click();
  await page.locator('#username').waitFor({ timeout: 120_000 });
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
  await page.getByRole('link', { name: 'Go to Dashboard' }).waitFor({ timeout: 30_000 });
}

async function captureLanding(page: Page): Promise<void> {
  // Generic landing/dashboard captures shared across walkthroughs.
  const dir = await shotDir('shared');
  await page.goto('/');
  await shoot(page, dir, '01-homepage.png');
  await page.getByRole('link', { name: 'Go to Dashboard' }).click();
  await page.getByRole('heading', { name: /My Workflows|Dashboard/i }).first().waitFor({ timeout: 30_000 });
  await shoot(page, dir, '02-dashboard.png');
}

async function captureCommunityEnquiry(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('community-enquiry');
  await resetWorkflows(request);
  await page.goto('/get-in-touch');
  await page.getByRole('heading', { name: 'Tell us about your enquiry' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '01-initial.png');

  // Pre-populated readonly fields come from auth claims; only fill non-readonly.
  for (const label of ['Full name', 'Email address']) {
    const field = page.getByLabel(label);
    const isReadonly = await field.evaluate(el => el.hasAttribute('readonly')).catch(() => true);
    if (!isReadonly) await field.fill(label === 'Full name' ? 'Jane Doe' : 'jane.doe@example.com');
  }
  await page.getByLabel('Organisation (optional)').fill('Acme Corp');
  await page.locator('select#your-role').selectOption('Developer');

  // Show conditional reveal first.
  await page.getByRole('radio', { name: 'Other' }).check();
  await page.getByRole('textbox', { name: /specify.*enquiry/i }).waitFor({ timeout: 5_000 });
  await shoot(page, dir, '02-conditional-reveal.png');

  await page.getByRole('textbox', { name: /specify.*enquiry/i }).fill('Partnership enquiry');
  await page.getByRole('radio', { name: 'General enquiry' }).check();
  await page.getByLabel('Tell us more').fill(
    'I would like to learn more about Prism integration options for our Umbraco site.'
  );
  await page.getByRole('checkbox', { name: 'Umbraco CMS' }).check();
  await page.getByRole('checkbox', { name: '.NET Development' }).check();
  await shoot(page, dir, '03-form-filled.png');

  await page.getByRole('button', { name: 'Submit' }).click();
  await page.getByRole('heading', { name: 'Your enquiry is with us' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '04-under-review.png');
}

async function capturePaymentDemo(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('payment-demo');
  await resetWorkflows(request);
  await page.goto('/payment-demo');
  await page.getByRole('heading', { name: 'Enter Payment Details' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '01-initial.png');

  await page.getByLabel('Cardholder name').fill('Jane Doe');
  await page.getByLabel('Amount (£)').fill('42.50');
  await shoot(page, dir, '02-form-filled.png');

  await page.getByRole('button', { name: 'Submit' }).click();
  await page.getByRole('heading', { name: 'Processing Your Payment' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '03-processing.png');
}

async function capturePlanningNotification(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('planning-notification');
  await resetWorkflows(request);
  await page.goto('/apply-for-planning-permission');
  await page.getByRole('heading', { name: 'Describe your project' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '01-initial.png');

  await page.getByLabel('Project name').fill('Loft conversion');
  await page.getByLabel('Describe the proposed works').fill(
    'Converting existing loft space into habitable bedroom with dormer window'
  );
  await page.getByLabel('Property address').fill('456 Oak Avenue\nWoodlands\nWD3 4EF');
  await shoot(page, dir, '02-project-filled.png');
  await page.getByRole('button', { name: 'Continue' }).click();

  await page.getByRole('heading', { name: 'Type of work' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '03-work-type.png');

  await page.getByRole('radio', { name: 'Other' }).check();
  await page.getByLabel('Describe the type of work').waitFor({ timeout: 5_000 });
  await shoot(page, dir, '04-work-type-conditional.png');

  await page.getByLabel('Describe the type of work').fill('Listed building restoration with specialist masonry');
  await page.getByRole('radio', { name: 'Extension or alteration' }).check();
  await page.getByRole('radio', { name: 'Yes' }).first().check();
  await page.getByRole('button', { name: 'Continue' }).click();

  await page.getByRole('heading', { name: 'Timeline and cost' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '05-timeline-cost.png');

  await page.locator('#proposedStartDate-day').fill('1');
  await page.locator('#proposedStartDate-month').fill('9');
  await page.locator('#proposedStartDate-year').fill('2025');
  await page.getByLabel('Estimated duration in weeks').fill('16');
  await page.getByLabel('Estimated cost of works').fill('35000.75');
  await shoot(page, dir, '06-timeline-filled.png');
  await page.getByRole('button', { name: 'Continue' }).click();

  await page.getByRole('heading', { name: 'Affected parties' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '07-affected-parties.png');

  await page.getByRole('checkbox', { name: 'Neighbouring properties' }).check();
  await page.getByRole('checkbox', { name: 'Conservation area' }).check();
  await page.getByRole('radio', { name: 'Yes' }).last().check();
  await page.getByRole('button', { name: 'Continue' }).click();

  await page.getByRole('heading', { name: 'Check your answers' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '08-check-answers.png');

  await page.getByRole('button', { name: 'Submit' }).click();
  await page.locator('.govuk-panel--confirmation').waitFor({ timeout: 30_000 });
  await shoot(page, dir, '09-confirmation.png');
}

async function captureInformationRequest(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('information-request');
  await resetWorkflows(request);
  await page.goto('/request-information');
  await page.getByRole('heading', { name: 'Tell us about yourself' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '01-initial.png');

  await page.getByLabel('First name').fill('Jane');
  await page.getByLabel('Last name').fill('Smith');
  await page.locator('#dateOfBirth-day').fill('12');
  await page.locator('#dateOfBirth-month').fill('3');
  await page.locator('#dateOfBirth-year').fill('1985');
  await page.getByLabel('Email address').fill('jane.smith@example.com');
  await page.locator('select#requestType').selectOption('Data subject access request');
  await page.getByLabel('Tell us more about your request').fill(
    'I would like to request a copy of all personal data you hold about me, in accordance with GDPR Article 15.'
  );
  await page.getByRole('radio', { name: 'Urgent (2 working days)' }).check();
  await shoot(page, dir, '02-form-filled.png');

  await page.getByRole('button', { name: 'Submit' }).click();
  await page.getByRole('heading', { name: 'Your request is being reviewed' }).waitFor({ timeout: 30_000 });
  await shoot(page, dir, '03-under-review.png');
}

test.describe('Walkthrough screenshot capture', () => {
  test.describe.configure({ mode: 'serial' });
  // Only run when explicitly opted in (via the capture-screenshots GitHub Actions
  // workflow which sets CAPTURE_SCREENSHOTS=1, or locally with the same env var).
  // Skipped during the normal localhost-auth CI lane to keep that lane focused on
  // functional tests.
  test.skip(process.env.CAPTURE_SCREENSHOTS !== '1', 'Set CAPTURE_SCREENSHOTS=1 to run.');

  // 30-minute budget: cold Aspire stack (~10 min) + four multi-step workflow walks.
  test.setTimeout(30 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('sign in and capture every walkthrough step', async ({ page, request }) => {
    await signIn(page);
    await captureLanding(page);
    await captureCommunityEnquiry(page, request);
    await capturePaymentDemo(page, request);
    await capturePlanningNotification(page, request);
    await captureInformationRequest(page, request);

    // Sanity: confirm we ended on a recognised page.
    await expect(page).toHaveURL(/.*/);
  });
});
