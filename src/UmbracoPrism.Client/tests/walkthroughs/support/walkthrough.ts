/**
 * Shared helpers for walkthrough executable specs.
 * See .claude/skills/walkthroughs-as-executable-specs/SKILL.md for the policy.
 */
import { expect, type Page, type APIRequestContext } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

// Resolve paths relative to the UmbracoPrism.Client directory (process.cwd() when Playwright runs).
const docsRoot = path.resolve(process.cwd(), '../../docs/images/walkthroughs');

export const businessAppOrigin = 'https://localhost:7245';

export const demoCredentials = { username: 'demo@prism.local', password: 'password' };
const defaultScreenshotMaxHeight = 3_600;
const defaultScreenshotPadding = 48;

export interface PageHealthCheck {
  url: RegExp;
  heading: string | RegExp;
  /** Override default error-marker regex. */
  bodyMustNotContain?: RegExp;
  /** Allow a GOV.UK error summary when the step intentionally captures validation feedback. */
  allowErrorSummary?: boolean;
  /** Skip the heading check (e.g. confirmation panels using govuk-panel--confirmation). */
  skipHeading?: boolean;
  /**
   * Capture the entire scrollable page without any height constraints.
   * The default capture strategy already expands beyond the viewport to show the
   * useful content being demonstrated, while still capping unusually tall pages.
   *
   * Use fullPage: true when:
   * - The step demonstrates content that still extends beyond the content-aware cap
   * - You need to guarantee all relevant page content appears in one screenshot
   * - The narrative requires the reader to see the full page context
   *
   * Hook contract for Isabelle (docs pipeline): when CAPTURE_SCREENSHOTS=1 a per-step
   * fullPage flag is the intended control point. If the docs service blueprint needs a different
   * default, add a SCREENSHOT_FULL_PAGE env var to the capture-screenshots.yml service blueprint
   * and read it here alongside the per-step override.
   */
  fullPage?: boolean;
  /** Capture up to this selector's bottom edge from the current scroll position. */
  screenshotSelector?: string;
  /** Override the content-aware screenshot height cap for unusually tall steps. */
  screenshotMaxHeight?: number;
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
  if (!expected.allowErrorSummary) {
    await expect(
      page.locator('.govuk-error-summary'),
      'Page should not show a GOV.UK error summary on a happy-path capture'
    ).toHaveCount(0);
  }
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
    await enterScreenshotMode(page);
    await hideScreenshotOnlyUi(page);
    await waitForMermaidReadiness(page, expected);
    const dir = path.join(docsRoot, walkthroughKey);
    await mkdir(dir, { recursive: true });
    const file = path.join(dir, filename);
    await captureScreenshot(page, file, expected);
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

export async function openDashboard(page: Page): Promise<void> {
  await page.goto('/');
  await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible({ timeout: 30_000 });
  await page.getByRole('link', { name: 'Go to Dashboard' }).click();
  await expect(page).toHaveURL(/\/dashboard\/?$/, { timeout: 30_000 });
  await expect(page.getByRole('heading', { name: 'Service Blueprint Demos' })).toBeVisible({ timeout: 30_000 });
}

export async function resetServiceBlueprints(request: APIRequestContext): Promise<void> {
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

async function hideScreenshotOnlyUi(page: Page): Promise<void> {
  await page.addStyleTag({
    content: `
      .prism-mobile-ua-demo {
        display: none !important;
        visibility: hidden !important;
      }
    `
  });
}

async function waitForMermaidReadiness(page: Page, expected: PageHealthCheck): Promise<void> {
  const hasMermaid = await page.evaluate(selector => {
    const root = selector ? document.querySelector(selector) ?? document : document;
    return !!root.querySelector('.mermaid');
  }, expected.screenshotSelector);

  if (!hasMermaid) {
    return;
  }

  await page.waitForFunction(
    selector => {
      const root = selector ? document.querySelector(selector) ?? document : document;
      const diagrams = Array.from(root.querySelectorAll('.mermaid'));
      if (diagrams.length === 0) {
        return true;
      }

      return diagrams.every(diagram => {
        if (!(diagram instanceof HTMLElement)) {
          return false;
        }

        const directTextNodes = Array.from(diagram.childNodes).filter(
          node => node.nodeType === Node.TEXT_NODE && (node.textContent ?? '').trim().length > 0
        );
        const card = diagram.closest('.def-card');
        const cardReady =
          !card ||
          !card.hasAttribute('data-mermaid-render-state') ||
          card.getAttribute('data-mermaid-render-state') === 'ready';

        return (
          cardReady &&
          diagram.getAttribute('data-processed') === 'true' &&
          diagram.querySelector('svg') instanceof SVGElement &&
          directTextNodes.length === 0
        );
      });
    },
    expected.screenshotSelector,
    { timeout: 30_000 }
  );

  await page.evaluate(async () => {
    await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
    await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
  });
}

async function captureScreenshot(page: Page, file: string, expected: PageHealthCheck): Promise<void> {
  const useFullPage = expected.fullPage ?? process.env.SCREENSHOT_FULL_PAGE === '1';
  if (useFullPage) {
    await page.screenshot({ path: file, fullPage: true });
    return;
  }

  const originalViewport = page.viewportSize();
  const targetHeight = await getContentAwareScreenshotHeight(page, expected);

  if (!originalViewport || targetHeight <= originalViewport.height) {
    await page.screenshot({ path: file });
    return;
  }

  await page.setViewportSize({ width: originalViewport.width, height: targetHeight });
  try {
    await page.screenshot({ path: file });
  } finally {
    await page.setViewportSize(originalViewport);
  }
}

async function getContentAwareScreenshotHeight(page: Page, expected: PageHealthCheck): Promise<number> {
  const viewport = page.viewportSize();
  if (!viewport) {
    return defaultScreenshotMaxHeight;
  }

  const maxHeight = Math.max(viewport.height, expected.screenshotMaxHeight ?? defaultScreenshotMaxHeight);

  const measuredHeight = await page.evaluate(
    ({ selector, padding, maxHeight: captureMaxHeight, minHeight }) => {
      const target =
        (selector ? document.querySelector(selector) : null) ??
        document.querySelector('main#main-content') ??
        document.querySelector('main') ??
        document.querySelector('[role="main"]') ??
        document.body;

      if (!(target instanceof HTMLElement)) {
        return minHeight;
      }

      const targetBottom = target.getBoundingClientRect().bottom + window.scrollY;
      const candidateHeight = Math.ceil(targetBottom + padding);
      return Math.min(captureMaxHeight, Math.max(minHeight, candidateHeight));
    },
    {
      selector: expected.screenshotSelector,
      padding: defaultScreenshotPadding,
      maxHeight,
      minHeight: viewport.height
    }
  );

  return Math.max(viewport.height, Math.ceil(measuredHeight));
}

/**
 * The service blueprint admin page loads Ace and Mermaid from public CDNs. We always stub
 * Ace because walkthroughs never need the full editor bundle. Mermaid is stubbed
 * only outside screenshot capture so normal Playwright tests stay deterministic
 * without third-party network dependencies, while screenshot runs still render
 * the real diagram markup before capture.
 */
export async function stubServiceBlueprintAdminVendorAssets(page: Page): Promise<void> {
  await page.context().route(/https:\/\/cdnjs\.cloudflare\.com\/ajax\/libs\/ace\/.*\/ace\.min\.js/, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/javascript',
      body: `
        window.ace = {
          edit() {
            return {
              setTheme() {},
              setOptions() {},
              setValue() {},
              getValue() { return '{}'; },
              session: { setMode() {} }
            };
          }
        };
      `
    })
  );

  if (process.env.CAPTURE_SCREENSHOTS !== '1') {
    await page.context().route(/https:\/\/cdn\.jsdelivr\.net\/npm\/mermaid@.*\/dist\/mermaid\.esm\.min\.mjs/, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/javascript',
        body: 'const mermaid = { initialize() {}, run() {} }; export default mermaid;'
      })
    );
  }
}

export async function waitForServiceBlueprintAdmin(page: Page): Promise<void> {
  await assertHealthyPage(page, {
    url: /https:\/\/localhost:7245\/admin\/service-blueprint\/?$/,
    heading: /service-blueprint admin/i
  });
  await expect(page.getByRole('heading', { name: 'ServiceBlueprint Instances' })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole('heading', { name: 'Service Blueprint Definitions' })).toBeVisible({ timeout: 30_000 });
}

export async function openServiceBlueprintAdminFromDashboard(page: Page): Promise<Page> {
  await stubServiceBlueprintAdminVendorAssets(page);

  const adminLink = page.getByRole('link', { name: 'Open Admin' });
  await expect(adminLink).toBeVisible({ timeout: 30_000 });
  await expect(adminLink).toHaveAttribute('href', `${businessAppOrigin}/admin/service-desk`);

  const [adminPage] = await Promise.all([
    page.context().waitForEvent('page'),
    adminLink.click()
  ]);

  await adminPage.waitForLoadState('domcontentloaded');
  await waitForServiceBlueprintAdmin(adminPage);

  return adminPage;
}
