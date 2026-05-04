/**
 * Shared helpers for walkthrough executable specs.
 * See .squad/skills/walkthroughs-as-executable-specs/SKILL.md for the policy.
 */
import { expect, type Page, type APIRequestContext } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

// Resolve paths relative to the UmbracoPrism.Client directory (process.cwd() when Playwright runs).
const docsRoot = path.resolve(process.cwd(), '../../docs/images/walkthroughs');

export const businessAppOrigin = 'https://localhost:7245';

export const demoCredentials = { username: 'demo@prism.local', password: 'password' };

export interface PageHealthCheck {
  url: RegExp;
  heading: string | RegExp;
  /** Override default error-marker regex. */
  bodyMustNotContain?: RegExp;
  /** Skip the heading check (e.g. confirmation panels using govuk-panel--confirmation). */
  skipHeading?: boolean;
  /**
   * Capture full-page screenshot (scroll height) instead of the default viewport crop.
   * Use sparingly — viewport captures are preferred for documentation because they show
   * what the user actually sees. Only set true for summary/check-answers pages where the
   * entire list must be visible in the screenshot.
   *
   * Hook contract for Isabelle (docs pipeline): when CAPTURE_SCREENSHOTS=1 a per-step
   * fullPage flag is the intended control point. If the docs workflow needs a different
   * default, add a SCREENSHOT_FULL_PAGE env var to the capture-screenshots.yml workflow
   * and read it here alongside the per-step override.
   */
  fullPage?: boolean;
}

/**
 * Verify the page is the page we intended before asserting or capturing.
 *
 * Checks URL, heading (unless skipHeading), body error markers, and the
 * absence of a GOV.UK error summary. Prevents screenshots of 404/error pages.
 * See SKILL.md R3.
 */
export async function assertHealthyPage(page: Page, expected: PageHealthCheck): Promise<void> {
  await expect(page, `URL should match ${expected.url}`).toHaveURL(expected.url, { timeout: 30_000 });
  if (!expected.skipHeading) {
    await expect(
      page.getByRole('heading', { name: expected.heading }).first(),
      `Expected heading "${expected.heading}" to be visible`
    ).toBeVisible({ timeout: 30_000 });
  }
  const errorMarker =
    expected.bodyMustNotContain ??
    /\b(404|Not Found|Page not found|An error occurred|Server Error|status code does not indicate success)\b/i;
  await expect(
    page.locator('body'),
    'Page body should not contain error markers'
  ).not.toContainText(errorMarker, { timeout: 5_000 });
  await expect(
    page.locator('.govuk-error-summary'),
    'Page should not show a GOV.UK error summary on a happy-path capture'
  ).toHaveCount(0);
}

/**
 * Assert the page is healthy then, if CAPTURE_SCREENSHOTS=1, write a PNG.
 * Specs use step() exclusively — never page.screenshot() directly. See SKILL.md R3 and R4.
 */
export async function step(
  page: Page,
  filename: string,
  expected: PageHealthCheck,
  walkthroughKey: string
): Promise<void> {
  await assertHealthyPage(page, expected);
  if (process.env.CAPTURE_SCREENSHOTS === '1') {
    const dir = path.join(docsRoot, walkthroughKey);
    await mkdir(dir, { recursive: true });
    const file = path.join(dir, filename);
    await page.screenshot({ path: file, fullPage: expected.fullPage ?? false });
    console.log(`Captured: ${file}`);
  }
}

export async function signIn(page: Page): Promise<void> {
  if (process.env.CAPTURE_SCREENSHOTS === '1') {
    await enterScreenshotMode(page);
  }
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

export async function resetWorkflows(request: APIRequestContext): Promise<void> {
  await request.delete(`${businessAppOrigin}/api/test/reset`, { ignoreHTTPSErrors: true });
}

/**
 * Set the `prism-screenshot-mode` cookie so the server suppresses the mobile
 * helper toggle widget for every subsequent page load in this browser context.
 * The UA bootstrap script is still emitted — mobile-UA behaviour in tests that
 * need it is unaffected.
 *
 * Call once per browser context before any navigation.  `signIn()` calls this
 * automatically when `CAPTURE_SCREENSHOTS=1`; call it directly in any spec that
 * needs screenshot-clean pages outside of the signIn flow.
 *
 * Contract: the server reads cookie `prism-screenshot-mode=1` (see
 * `PrismScreenshotMode.CookieName` in UmbracoPrism.Core).
 */
export async function enterScreenshotMode(page: Page): Promise<void> {
  await page.context().addCookies([
    {
      name: 'prism-screenshot-mode',
      value: '1',
      domain: 'localhost',
      path: '/',
      sameSite: 'Lax',
      secure: false,
    },
  ]);
}
