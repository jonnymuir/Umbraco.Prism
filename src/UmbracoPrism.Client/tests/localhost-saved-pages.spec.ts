import { test, expect, type Page } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

// Saved pages (Design/mobile-offline-pages.md), as a generated app built with them sees the site:
// its user agent says so, and the site registers prism-offline-sw.js for it.
const appUserAgent =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 PrismMobile PrismNativeTabs PrismSystemSignIn PrismSavedPages';

const appHost = new LiveAppHost();

// Chromium registers a service worker only on a site whose certificate it trusts, and
// ignoreHTTPSErrors does not reach worker registration; CI does not trust the local dev certificate.
test.use({
  userAgent: appUserAgent,
  serviceWorkers: 'allow',
  launchOptions: { args: ['--ignore-certificate-errors'] }
});

test.describe('Saved pages in an app built with them', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('a page a visitor has seen opens with no connection, and says the app is offline', async ({ page, context }) => {
    await openWithWorker(page, '/');
    await expect.poll(() => savedPaths(page)).toContain('/');

    await context.setOffline(true);
    await page.goto('/');

    await expect(page.getByRole('heading', { name: /Your account, your way/i })).toBeVisible();
    await expect(page.getByRole('status')).toHaveText('You’re offline. Some information may be out of date.');

    await context.setOffline(false);
    await expect(page.getByRole('status')).toHaveCount(0);
  });

  test('a page the site says must not be stored is never saved', async ({ page }) => {
    await openWithWorker(page, '/');
    await page.goto('/apply-for-a-juggling-licence');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await savedPaths(page)).not.toContain('/apply-for-a-juggling-licence');
  });

  test('signing in deletes every saved page, and signed-in pages are never saved', async ({ page }) => {
    await openWithWorker(page, '/');
    await expect.poll(() => savedPaths(page)).toContain('/');

    await page.getByRole('link', { name: 'Sign In' }).click();
    await expect(page.locator('#username')).toBeVisible({ timeout: 120_000 });
    await page.locator('#username').fill('demo@prism.local');
    await page.locator('#password').fill('password');
    await Promise.all([
      page.waitForURL(url => url.origin === 'https://localhost:44345' && url.pathname !== '/signin-oidc', { timeout: 120_000 }),
      page.locator('#kc-login').click()
    ]);

    await page.goto('/');
    await expect(page.getByText('Welcome back, Demo User')).toBeVisible();
    await expect.poll(() => savedPaths(page)).toEqual([]);
  });
});

/** Opens a page once the worker controls it, so the load goes through the worker. */
async function openWithWorker(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await expect
    .poll(() => page.evaluate(async () => Boolean((await navigator.serviceWorker.getRegistration())?.active)), { timeout: 30_000 })
    .toBe(true);
  await page.reload();
  await expect.poll(() => page.evaluate(() => navigator.serviceWorker.controller !== null)).toBe(true);
}

async function savedPaths(page: Page): Promise<string[]> {
  return page.evaluate(async () => {
    const cache = await caches.open('prism-saved-pages-v1');
    return (await cache.keys()).map(request => new URL(request.url).pathname);
  });
}
