/**
 * Standalone screenshot-capture spec.
 *
 * Starts the full Aspire stack via LiveAppHost, signs in with the demo account,
 * navigates to each workflow landing page, and saves the initial-state screenshots
 * to docs/images/walkthroughs/ at the repo root.
 *
 * Run via the dedicated capture-screenshots workflow (see .github/workflows/capture-screenshots.yml)
 * or locally once the Aspire stack is already running (skip the beforeAll/afterAll by
 * commenting out the LiveAppHost calls).
 */
import { test } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();

// Resolve paths relative to the UmbracoPrism.Client directory (process.cwd() when Playwright runs).
const docsRoot = path.resolve(process.cwd(), '../../docs/images/walkthroughs');

const demoCredentials = { username: 'demo@prism.local', password: 'password' };

const workflows = [
  { key: 'community-enquiry', url: '/get-in-touch', heading: 'Tell us about your enquiry' },
  { key: 'planning-notification', url: '/apply-for-planning-permission', heading: 'Describe your project' },
  { key: 'payment-demo', url: '/payment-demo', heading: 'Enter Payment Details' },
  { key: 'information-request', url: '/request-information', heading: 'Tell us about yourself' }
] as const;

test.describe('Walkthrough screenshot capture', () => {
  test.describe.configure({ mode: 'serial' });
  // 20-minute budget: the Aspire stack (Keycloak + TestSite + MockBusinessApp) can take
  // up to ~10 minutes to reach full readiness on a cold GitHub Actions runner, plus time
  // for sign-in, navigation, and screenshot I/O for all four workflow pages.
  test.setTimeout(20 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('sign in and capture all walkthrough initial screenshots', async ({ page, request }) => {
    // Reset all workflow state so we always see the initial form.
    await request.delete('https://localhost:7245/api/test/reset', { ignoreHTTPSErrors: true });

    // Sign in once via Keycloak SSO.
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

    // Capture the initial screenshot for each workflow landing page.
    for (const workflow of workflows) {
      const outputDir = path.join(docsRoot, workflow.key);
      await mkdir(outputDir, { recursive: true });

      await page.goto(workflow.url);
      await page.getByRole('heading', { name: workflow.heading }).waitFor({ timeout: 30_000 });

      const screenshotPath = path.join(outputDir, '01-initial.png');
      await page.screenshot({ path: screenshotPath, fullPage: true });
      console.log(`Captured: ${screenshotPath}`);
    }
  });
});
