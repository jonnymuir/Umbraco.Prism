import { test, expect } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();

// GOV.UK Frontend v5 ships its JS as an ES module (the bundle ends in an `export{...}`
// statement) — loading it as a classic <script> throws a SyntaxError at parse time, which
// silently kills every GDS JS-enhanced component on the page (accordion, button
// double-submit guarding, checkbox/radio conditional reveal, etc.) without failing any
// server-side request or C# test. Only a real browser catches this class of bug.
test.describe('GOV.UK Frontend JS bootstrap', () => {
  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('govuk-frontend.min.js loads without a script error and initAll runs', async ({ page }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', error => pageErrors.push(error.message));

    await page.goto('/');

    expect(
      pageErrors,
      `page threw uncaught script error(s), most likely from govuk-frontend.min.js being loaded as a ` +
        `classic script when it's actually an ES module:\n${pageErrors.join('\n')}`
    ).toEqual([]);

    const initAllType = await page.evaluate(() => typeof (window as unknown as {
      GOVUKFrontend?: { initAll?: unknown };
    }).GOVUKFrontend?.initAll);

    expect(initAllType, 'window.GOVUKFrontend.initAll must be a function after the bootstrap script runs').toBe('function');
  });
});
