import { test, expect, type Locator, type Page, type Response } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();
const demoCredentials = {
  username: 'demo@prism.local',
  password: 'password'
};
const expectedServiceBlueprintDemos = [
  { title: 'Get in Touch', path: /\/get-in-touch$/ },
  { title: 'Apply for Planning Permission', path: /\/apply-for-planning-permission$/ },
  { title: 'Payment Demo', path: /\/payment-demo$/ },
  { title: 'Request Information', path: /\/request-information$/ }
] as const;

test.describe('Localhost auth/session behavioural contracts', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('logged-out member can complete the localhost Keycloak sign-in flow', async ({ page }) => {
    await expectSignedOutHome(page);
    await signIn(page);
    await expectSignedInHome(page);
  });

  test('signed-in member can call the mock business app API', async ({ page }) => {
    await signIn(page);
    await callBusinessAppApi(page);
  });

  test('signed-in member can open My ServiceBlueprints', async ({ page }) => {
    await signIn(page);
    await page.goto('/my-service-blueprints');

    await expect(page.getByRole('heading', { name: 'My ServiceBlueprints' })).toBeVisible();
    await expectAnyVisible(
      page.getByText("You don't have any active service blueprints yet."),
      page.getByRole('heading', { name: 'In Progress' }),
      page.getByRole('heading', { name: 'Completed' })
    );
  });

  test('anonymous CMS ServiceBlueprint instance is claimed and resumable after signing in', async ({ page }) => {
    // Start "Apply for a juggling licence" anonymously — no sign-in yet.
    await page.goto('/apply-for-a-juggling-licence');
    await page.getByLabel('I confirm I am aged 16 or over').check();
    await page.getByLabel('I confirm I have a UK postal address').check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible();

    const anonymousCookie = (await page.context().cookies()).find(c => c.name === 'PrismCmsServiceBlueprintVisitor');
    expect(anonymousCookie, 'starting a CMS ServiceBlueprint anonymously must set the visitor correlation cookie').toBeTruthy();

    // Sign in — same browser context, so the anonymous cookie rides along with the sign-in
    // request and the server-side claim hook can see both identities together.
    await signIn(page);

    // The claim succeeded: the anonymous cookie is gone (nothing left to correlate against —
    // the instance now belongs to the signed-in member) and it shows up as resumable.
    const cookiesAfterSignIn = await page.context().cookies();
    expect(cookiesAfterSignIn.some(c => c.name === 'PrismCmsServiceBlueprintVisitor')).toBe(false);

    await page.goto('/my-service-blueprints');
    await expect(page.getByRole('heading', { name: 'In Progress' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Apply for a juggling licence', level: 3 })).toBeVisible();
    await expect(page.getByText('Your details')).toBeVisible();

    // Resuming lands back on the exact step left off, not a fresh restart.
    await page.getByRole('link', { name: 'Continue' }).click();
    await expect(page).toHaveURL(/\/apply-for-a-juggling-licence\/?\?instanceId=/);
    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible();
  });

  test('signed-in member can open the seeded service blueprint start page', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');

    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Submit' })).toBeVisible();
  });

  test('signed-in member can reach seeded service blueprint pages from the dashboard', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    await page.getByRole('link', { name: 'View ServiceBlueprints' }).click();
    await expect(page).toHaveURL(/\/my-service-blueprints$/);

    await openDashboard(page);
    for (const serviceBlueprint of expectedServiceBlueprintDemos) {
      await expect(serviceBlueprintDemoCard(page, serviceBlueprint.title)).toBeVisible();
      await expect(serviceBlueprintDemoCard(page, serviceBlueprint.title).getByRole('link', { name: 'Start' })).toHaveAttribute(
        'href',
        serviceBlueprint.path
      );
    }

    await serviceBlueprintDemoCard(page, 'Get in Touch').getByRole('link', { name: 'Start' }).click();
    await expect(page).toHaveURL(expectedServiceBlueprintDemos[0].path);
    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible();
  });

  test('signed-in member stays signed in across a full restart', async ({ page }) => {
    await signIn(page);
    await appHost.restart();
    
    // After restart, verify member is still authenticated
    await expectSignedInHome(page);
    
    // Verify persistent session allows protected API calls
    await callBusinessAppApi(page);
    
    // Verify clean sign-out still works after restart
    await signOut(page);
    await expectSignedOutHome(page);
  });

  test('signed-in member can sign out cleanly', async ({ page }) => {
    await signIn(page);
    await signOut(page);
    await expectSignedOutHome(page);
  });
});

async function expectSignedOutHome(page: Page): Promise<void> {
  await page.goto('/');
  await expect(page.getByRole('link', { name: 'Sign In' })).toBeVisible();
  await expect(page.getByRole('heading', { name: /Your account, your way/i })).toBeVisible();
}

async function expectSignedInHome(page: Page): Promise<void> {
  await page.goto('/');
  await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sign Out' }).first()).toBeVisible();
  await expect(page.getByText('Welcome back, Demo User')).toBeVisible();
}

async function signIn(page: Page): Promise<void> {
  await expectSignedOutHome(page);

  await page.getByRole('link', { name: 'Sign In' }).click();

  await expect(page.locator('#username')).toBeVisible({ timeout: 120_000 });
  await page.locator('#username').fill(demoCredentials.username);
  await page.locator('#password').fill(demoCredentials.password);

  const capture = captureResponses(page, /localhost:44345\/(signin-oidc|dashboard\/?$|$)/);
  try {
    await Promise.all([
      page.waitForURL(
        url => url.origin === 'https://localhost:44345' && url.pathname !== '/signin-oidc',
        { timeout: 120_000 }
      ),
      page.locator('#kc-login').click()
    ]);
  } catch (error) {
    throw new Error(await formatNavigationFailure(page, 'Sign-in callback did not return to a stable app page.', capture, error));
  } finally {
    capture.dispose();
  }

  await expectSignedInHome(page);
}

async function signOut(page: Page): Promise<void> {
  await page.goto('/');

  await expect(page.getByRole('button', { name: 'Sign Out' }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Sign Out' }).first().click();

  const logoutConfirm = page.locator('#kc-logout');
  if (await logoutConfirm.isVisible({ timeout: 5_000 }).catch(() => false)) {
    await logoutConfirm.click();
  }

  await page.waitForURL(url => url.origin === 'https://localhost:44345' && url.pathname === '/', {
    timeout: 120_000
  });
}

async function callBusinessAppApi(page: Page): Promise<void> {
  await openDashboard(page);
  await page.getByRole('button', { name: 'Call Mock Business App API' }).click();

  const statusBadge = page.locator('#api-status-badge');
  const apiSummary = page.locator('#api-summary');
  const apiBody = page.locator('#api-body');
  const apiUrl = page.locator('#api-url-label');

  try {
    await expect(statusBadge).toHaveText(/200 OK/, { timeout: 120_000 });
  } catch (error) {
    const actualStatus = await statusBadge.textContent().catch(() => '(not found)');
    const actualSummary = await apiSummary.textContent().catch(() => '(not found)');
    const actualBody = await apiBody.textContent().catch(() => '(not found)');

    throw new Error(
      `Expected API call to succeed with 200 OK, but got:\n` +
        `Status: ${actualStatus}\n` +
        `Summary: ${actualSummary}\n` +
        `Body: ${actualBody}\n\n` +
        `Original error: ${error instanceof Error ? error.message : String(error)}`
    );
  }

  await expect(apiSummary).toContainText('responded successfully');
  await expect(apiBody).toContainText('"tenant": "Prism Demo (Keycloak)"');
  await expect(apiBody).toContainText('"assignedRole": "Admin"');
  await expect(apiBody).toContainText('"userEmail": "demo@prism.local"');

  // Contract: Browser-facing API responses must not expose internal backchannel URLs
  const displayedUrl = await apiUrl.textContent();
  expect(displayedUrl).not.toContain(':5163', 
    'displayed URL must not expose the internal backchannel port 5163');
  expect(displayedUrl).toContain('https://localhost:7245',
    'displayed URL must show the public-facing HTTPS endpoint');
}

async function openDashboard(page: Page): Promise<void> {
  await page.goto('/');

  const dashboardLink = page.getByRole('link', { name: 'Go to Dashboard' });
  await expect(dashboardLink).toHaveAttribute('href', /\/dashboard\/?$/);

  const capture = captureResponses(page, /localhost:44345\/(dashboard\/?$|$)/);
  try {
    await Promise.all([page.waitForURL(/\/dashboard\/?$/, { timeout: 120_000 }), dashboardLink.click()]);
  } catch (error) {
    throw new Error(await formatNavigationFailure(page, 'Dashboard navigation did not settle on /dashboard.', capture, error));
  } finally {
    capture.dispose();
  }

  await expect(page.getByRole('link', { name: 'View ServiceBlueprints' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Call Mock Business App API' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'ServiceBlueprint Demos' })).toBeVisible();
}

function serviceBlueprintDemoCard(page: Page, title: string): Locator {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}

async function expectAnyVisible(...locators: Locator[]): Promise<void> {
  for (const locator of locators) {
    if (await locator.isVisible().catch(() => false)) {
      return;
    }
  }

  throw new Error('Expected at least one service blueprint state indicator to be visible.');
}

type ResponseCapture = {
  entries: Array<{ status: number; url: string; location: string | null }>;
  dispose: () => void;
};

function captureResponses(page: Page, urlPattern: RegExp): ResponseCapture {
  const entries: Array<{ status: number; url: string; location: string | null }> = [];
  const handler = (response: Response) => {
    const url = response.url();
    if (!urlPattern.test(url)) {
      return;
    }

    entries.push({
      status: response.status(),
      url,
      location: response.headers()['location'] ?? null
    });

    if (entries.length > 20) {
      entries.splice(0, entries.length - 20);
    }
  };

  page.on('response', handler);
  return {
    entries,
    dispose: () => page.off('response', handler)
  };
}

async function formatNavigationFailure(
  page: Page,
  summary: string,
  capture: ResponseCapture,
  error: unknown
): Promise<string> {
  const bodyPreview = (await page.locator('body').innerText().catch(() => '')).slice(0, 200).replace(/\s+/g, ' ').trim();
  const recentResponses =
    capture.entries.length > 0
      ? capture.entries
          .map(entry => `${entry.status} ${entry.url}${entry.location ? ` -> ${entry.location}` : ''}`)
          .join('\n')
      : '(no matching responses captured)';

  return [
    summary,
    `Current URL: ${page.url()}`,
    `Recent responses:\n${recentResponses}`,
    `Body preview: ${bodyPreview || '(empty)'}`,
    `Playwright error: ${error instanceof Error ? error.message : String(error)}`
  ].join('\n\n');
}
