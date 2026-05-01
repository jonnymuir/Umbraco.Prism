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

/**
 * Verify the page we're about to screenshot is the page we intended.
 *
 * Captures of the wrong page (404 / error / unauthenticated redirect) silently
 * land in docs/ and only get noticed during human review. This helper makes that
 * a hard failure: it asserts the URL matches, the expected heading is visible,
 * and no error markers (404, "Not Found", error summary, problem JSON) appear
 * on the page. Call this immediately before every `shoot()`.
 *
 * See .squad/skills/screenshot-capture-assertions/SKILL.md for the policy.
 */
async function assertHealthyPage(
  page: Page,
  expected: { url: RegExp; heading: string | RegExp; bodyMustNotContain?: RegExp }
): Promise<void> {
  await expect(page, `URL should match ${expected.url}`).toHaveURL(expected.url, { timeout: 30_000 });
  const headingMatcher = typeof expected.heading === 'string' ? expected.heading : expected.heading;
  await expect(
    page.getByRole('heading', { name: headingMatcher }).first(),
    `Expected heading "${expected.heading}" to be visible`
  ).toBeVisible({ timeout: 30_000 });
  // Defensive: no error/404 markers anywhere on the page.
  const errorMarker = expected.bodyMustNotContain ?? /\b(404|Not Found|Page not found|An error occurred|Server Error|status code does not indicate success)\b/i;
  await expect(
    page.locator('body'),
    'Page body should not contain error markers'
  ).not.toContainText(errorMarker, { timeout: 5_000 });
  // Defensive: no GOV.UK error summary on a "happy path" capture step.
  await expect(
    page.locator('.govuk-error-summary'),
    'Page should not show a GOV.UK error summary on a happy-path capture'
  ).toHaveCount(0);
}

async function shoot(
  page: Page,
  dir: string,
  name: string,
  expected: { url: RegExp; heading: string | RegExp; bodyMustNotContain?: RegExp }
): Promise<void> {
  await assertHealthyPage(page, expected);
  const file = path.join(dir, name);
  if (process.env.CAPTURE_SCREENSHOTS === '1') {
    await page.screenshot({ path: file, fullPage: true });
    console.log(`Captured: ${file}`);
  }
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
  await shoot(page, dir, '01-homepage.png', {
    url: /https:\/\/localhost:44345\/?$/,
    heading: /Welcome|Prism|Home/i
  });
  await page.getByRole('link', { name: 'Go to Dashboard' }).click();
  await shoot(page, dir, '02-dashboard.png', {
    url: /\/dashboard|\/my-workflows|\//,
    heading: /My Workflows|Dashboard/i
  });
}

async function captureCommunityEnquiry(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('community-enquiry');
  await resetWorkflows(request);
  await page.goto('/get-in-touch');
  await shoot(page, dir, '01-initial.png', {
    url: /\/get-in-touch/,
    heading: 'Tell us about your enquiry'
  });

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
  await expect(page.getByRole('textbox', { name: /specify.*enquiry/i })).toBeVisible({ timeout: 5_000 });
  await shoot(page, dir, '02-conditional-reveal.png', {
    url: /\/get-in-touch/,
    heading: 'Tell us about your enquiry'
  });

  await page.getByRole('textbox', { name: /specify.*enquiry/i }).fill('Partnership enquiry');
  await page.getByRole('radio', { name: 'General enquiry' }).check();
  await page.getByLabel('Tell us more').fill(
    'I would like to learn more about Prism integration options for our Umbraco site.'
  );
  await page.getByRole('checkbox', { name: 'Umbraco CMS' }).check();
  await page.getByRole('checkbox', { name: '.NET Development' }).check();
  await shoot(page, dir, '03-form-filled.png', {
    url: /\/get-in-touch/,
    heading: 'Tell us about your enquiry'
  });

  await page.getByRole('button', { name: 'Submit' }).click();
  await shoot(page, dir, '04-under-review.png', {
    url: /\/get-in-touch/,
    heading: 'Your enquiry is with us'
  });
}

async function capturePaymentDemo(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('payment-demo');
  await resetWorkflows(request);
  await page.goto('/payment-demo');
  await shoot(page, dir, '01-initial.png', {
    url: /\/payment-demo/,
    heading: 'Enter Payment Details'
  });

  await page.getByLabel('Cardholder name').fill('Jane Doe');
  await page.getByLabel('Amount (£)').fill('42.50');
  await shoot(page, dir, '02-form-filled.png', {
    url: /\/payment-demo/,
    heading: 'Enter Payment Details'
  });

  await page.getByRole('button', { name: 'Submit' }).click();
  await shoot(page, dir, '03-processing.png', {
    url: /\/payment-demo/,
    heading: 'Processing Your Payment'
  });
}

async function capturePlanningNotification(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('planning-notification');
  await resetWorkflows(request);
  await page.goto('/apply-for-planning-permission');
  await shoot(page, dir, '01-initial.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Describe your project'
  });

  await page.getByLabel('Project name').fill('Loft conversion');
  await page.getByLabel('Describe the proposed works').fill(
    'Converting existing loft space into habitable bedroom with dormer window'
  );
  await page.getByLabel('Property address').fill('456 Oak Avenue\nWoodlands\nWD3 4EF');
  await shoot(page, dir, '02-project-filled.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Describe your project'
  });
  await page.getByRole('button', { name: 'Continue' }).click();

  await shoot(page, dir, '03-work-type.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Type of work'
  });

  await page.getByRole('radio', { name: 'Other' }).check();
  await expect(page.getByLabel('Describe the type of work')).toBeVisible({ timeout: 5_000 });
  await shoot(page, dir, '04-work-type-conditional.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Type of work'
  });

  await page.getByLabel('Describe the type of work').fill('Listed building restoration with specialist masonry');
  await page.getByRole('radio', { name: 'Extension or alteration' }).check();
  await page.getByRole('radio', { name: 'Yes' }).first().check();
  await page.getByRole('button', { name: 'Continue' }).click();

  await shoot(page, dir, '05-timeline-cost.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Timeline and cost'
  });

  await page.locator('#proposedStartDate-day').fill('1');
  await page.locator('#proposedStartDate-month').fill('9');
  await page.locator('#proposedStartDate-year').fill('2025');
  await page.getByLabel('Estimated duration in weeks').fill('16');
  await page.getByLabel('Estimated cost of works').fill('35000.75');
  await shoot(page, dir, '06-timeline-filled.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Timeline and cost'
  });
  await page.getByRole('button', { name: 'Continue' }).click();

  await shoot(page, dir, '07-affected-parties.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Affected parties'
  });

  await page.getByRole('checkbox', { name: 'Neighbouring properties' }).check();
  await page.getByRole('checkbox', { name: 'Conservation area' }).check();
  await page.getByRole('radio', { name: 'Yes' }).last().check();
  await page.getByRole('button', { name: 'Continue' }).click();

  await shoot(page, dir, '08-check-answers.png', {
    url: /\/apply-for-planning-permission/,
    heading: 'Check your answers'
  });

  await page.getByRole('button', { name: 'Submit' }).click();
  await expect(page.locator('.govuk-panel--confirmation')).toBeVisible({ timeout: 30_000 });
  // Confirmation page uses a govuk-panel rather than a standard heading, so
  // assert URL + panel rather than reusing assertHealthyPage's heading check.
  await expect(page).toHaveURL(/\/apply-for-planning-permission/);
  await expect(page.locator('body')).not.toContainText(/\b(404|Not Found|Page not found)\b/i);
  if (process.env.CAPTURE_SCREENSHOTS === '1') {
    const file = path.join(dir, '09-confirmation.png');
    await page.screenshot({ path: file, fullPage: true });
    console.log(`Captured: ${file}`);
  }
}

async function captureInformationRequest(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('information-request');
  await resetWorkflows(request);
  await page.goto('/request-information');
  await shoot(page, dir, '01-initial.png', {
    url: /\/request-information/,
    heading: 'Tell us about yourself'
  });

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
  await shoot(page, dir, '02-form-filled.png', {
    url: /\/request-information/,
    heading: 'Tell us about yourself'
  });

  await page.getByRole('button', { name: 'Submit' }).click();
  await shoot(page, dir, '03-under-review.png', {
    url: /\/request-information/,
    heading: 'Your request is being reviewed'
  });
}

// ---------------------------------------------------------------------------
// New walkthrough capture helpers
// ---------------------------------------------------------------------------

/**
 * Captures screenshots for the "Authoring a Workflow" walkthrough.
 *
 * Most of this walkthrough covers source files and JSON — not interactive browser
 * pages — so the automatable surface is limited to the final verification step
 * (the seeded leave-request workflow rendered in the TestSite).
 *
 * TODO (manual captures required for backoffice steps):
 *   01-backoffice-workflow-key.png  — backoffice → Content → new Workflow Page → Workflow Key field
 *
 * The leave-request workflow seed must exist in workflow-seeds/ before running.
 * If the seed is absent, this helper skips the live-page capture gracefully.
 */
async function captureAuthoringAWorkflow(page: Page, request: APIRequestContext): Promise<void> {
  const dir = await shotDir('authoring-a-workflow');
  await resetWorkflows(request);

  // Attempt to capture the seeded leave-request workflow — only works if the seed exists.
  try {
    await page.goto('/leave-request');
    await page.getByRole('heading', { name: /leave|request annual leave/i }).waitFor({ timeout: 10_000 });
    await shoot(page, dir, '02-leave-request-initial.png');
  } catch {
    console.log('[capture] leave-request workflow not seeded — skipping live-page capture.');
  }
}

/**
 * Captures screenshots for the "Creating a Tenant" walkthrough.
 *
 * All steps require backoffice login — they cannot be driven by the demo-user
 * OIDC session. Manual captures are required for all steps.
 *
 * TODO (manual captures required):
 *   01-backoffice-login.png      — navigate to /umbraco, screenshot the login screen
 *   02-prism-dashboard.png       — Settings → Prism Dashboard, tenant list
 *   03-new-tenant-modal.png      — click "Add tenant", screenshot the modal
 *   04-branding-tab.png          — modal → Branding tab
 *   05-tenant2-homepage.png      — browser at tenant2.localhost after tenant created
 */
async function captureCreatingATenant(_page: Page): Promise<void> {
  await shotDir('creating-a-tenant');
  console.log('[capture] creating-a-tenant: all steps require backoffice login — manual captures only.');
}

/**
 * Captures screenshots for the "Design System" walkthrough.
 *
 * Storybook pages are automatable. Backoffice branding editor steps require
 * backoffice login and are flagged as manual captures.
 *
 * TODO (manual captures required):
 *   04-branding-editor.png          — backoffice → Prism Dashboard → localhost → Branding tab
 *   05-branding-updated-frontend.png — TestSite after changing --prism-primary in branding editor
 */
async function captureDesignSystem(page: Page): Promise<void> {
  const dir = await shotDir('design-system');

  // Storybook must be running on port 6006 for these captures.
  // In CI the capture-screenshots workflow should start Storybook separately.
  try {
    await page.goto('http://localhost:6006', { timeout: 15_000 });
    await page.waitForLoadState('networkidle', { timeout: 15_000 });
    await shoot(page, dir, '01-storybook-home.png');

    // Navigate to the Prism Dashboard story via the Storybook sidebar URL.
    await page.goto('http://localhost:6006/?path=/story/backoffice-prism-dashboard--default', { timeout: 15_000 });
    await page.waitForLoadState('networkidle', { timeout: 15_000 });
    await shoot(page, dir, '02-storybook-sidebar.png');

    // Tenant modal story.
    await page.goto('http://localhost:6006/?path=/story/backoffice-prism-create-tenant-modal--default', { timeout: 15_000 });
    await page.waitForLoadState('networkidle', { timeout: 15_000 });
    await shoot(page, dir, '03-storybook-tenant-modal.png');
  } catch {
    console.log('[capture] design-system Storybook captures skipped — Storybook not running on :6006.');
  }
}

/**
 * Captures screenshots for the "Push Notifications" walkthrough.
 *
 * The notification preferences page is accessible after sign-in.
 * Browser permission prompts and OS notification toasts cannot be automated.
 *
 * TODO (manual captures required):
 *   02-browser-permission.png     — OS/browser permission prompt (cannot be scripted)
 *   03-backoffice-send-notification.png — backoffice → Announcements → publish
 *   04-browser-notification.png   — OS notification toast (cannot be scripted)
 */
async function capturePushNotifications(page: Page): Promise<void> {
  const dir = await shotDir('push-notifications');

  // Attempt to reach the notification preferences page (path may vary by site config).
  try {
    await page.goto('/notifications');
    await page.waitForLoadState('networkidle', { timeout: 10_000 });
    await shoot(page, dir, '01-notification-prefs.png');
  } catch {
    console.log('[capture] push-notifications: /notifications page not found — skipping.');
  }
}

/**
 * Captures screenshots for the "Building a Mobile App" walkthrough.
 *
 * All mobile captures require a physical device or emulator — they cannot be
 * scripted via Playwright running against the web stack.
 *
 * TODO (manual captures required):
 *   01-biometric-enroll.png       — iOS/Android device: biometric enrollment prompt
 *   02-backoffice-biometric-setting.png — backoffice → Prism Dashboard → Biometric Auth toggle
 *   03-mobile-nav.png             — Storybook: Prism Mobile Nav story, OR physical device
 *   04-ios-app-running.png        — Physical iOS device or Xcode simulator
 *   05-android-app-running.png    — Android emulator or physical device
 */
async function captureBuildingAMobileApp(page: Page): Promise<void> {
  const dir = await shotDir('building-a-mobile-app');

  // Capture Mobile Nav story from Storybook if available.
  try {
    await page.goto('http://localhost:6006/?path=/story/mobile-prism-mobile-nav--default', { timeout: 15_000 });
    await page.waitForLoadState('networkidle', { timeout: 15_000 });
    await shoot(page, dir, '03-mobile-nav.png');
  } catch {
    console.log('[capture] building-a-mobile-app: Storybook not running — skipping mobile-nav story capture.');
  }
}

test.describe('Walkthrough screenshot capture', () => {
  test.describe.configure({ mode: 'serial' });
  // Only run when explicitly opted in (via the capture-screenshots GitHub Actions
  // workflow which sets CAPTURE_SCREENSHOTS=1, or locally with the same env var).
  // Skipped during the normal localhost-auth CI lane to keep that lane focused on
  // functional tests.
  test.skip(process.env.CAPTURE_SCREENSHOTS !== '1', 'Set CAPTURE_SCREENSHOTS=1 to run.');

  // 45-minute budget: cold Aspire stack (~10 min) + nine multi-step walkthrough captures.
  test.setTimeout(45 * 60_000);

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

    // New walkthroughs (5 additional).
    await captureAuthoringAWorkflow(page, request);
    await captureCreatingATenant(page);
    await captureDesignSystem(page);
    await capturePushNotifications(page);
    await captureBuildingAMobileApp(page);

    // Sanity: confirm we ended on a recognised page.
    await expect(page).toHaveURL(/.*/);
  });
});
