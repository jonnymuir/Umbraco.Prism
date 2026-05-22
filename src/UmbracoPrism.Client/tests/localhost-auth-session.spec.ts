import { test, expect, type Locator, type Page, type Response } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();
const demoCredentials = {
  username: 'demo@prism.local',
  password: 'password'
};
const expectedWorkflowDemos = [
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

  test('signed-in member can open My Workflows', async ({ page }) => {
    await signIn(page);
    await page.goto('/my-workflows');

    await expect(page.getByRole('heading', { name: 'My Workflows' })).toBeVisible();
    await expectAnyVisible(
      page.getByText("You don't have any active workflows yet."),
      page.getByRole('heading', { name: 'In Progress' }),
      page.getByRole('heading', { name: 'Completed' })
    );
  });

  test('signed-in member can open the seeded workflow start page', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');

    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Submit' })).toBeVisible();
  });

  test('signed-in member can reach seeded workflow pages from the dashboard', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    await page.getByRole('link', { name: 'View Workflows' }).click();
    await expect(page).toHaveURL(/\/my-workflows$/);

    await openDashboard(page);
    for (const workflow of expectedWorkflowDemos) {
      await expect(workflowDemoCard(page, workflow.title)).toBeVisible();
      await expect(workflowDemoCard(page, workflow.title).getByRole('link', { name: 'Start' })).toHaveAttribute(
        'href',
        workflow.path
      );
    }

    await workflowDemoCard(page, 'Get in Touch').getByRole('link', { name: 'Start' }).click();
    await expect(page).toHaveURL(expectedWorkflowDemos[0].path);
    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible();
  });

  test('signed-in member can still call the mock business app API after the whole stack restarts', async ({ page }) => {
    await signIn(page);
    await appHost.restart();
    
    // Verify the user is still signed in after restart before attempting API call
    await expectSignedInHome(page);
    
    await callBusinessAppApi(page);
  });

  test('signed-in member can sign out cleanly', async ({ page }) => {
    await signIn(page);
    await signOut(page);
    await expectSignedOutHome(page);
  });

  test('signed-in member stays signed in across a full restart and can still sign out', async ({ page }) => {
    await signIn(page);
    await appHost.restart();

    await expectSignedInHome(page);
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

  await expect(page.getByRole('link', { name: 'View Workflows' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Call Mock Business App API' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Workflow Demos' })).toBeVisible();
}

function workflowDemoCard(page: Page, title: string): Locator {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}

async function expectAnyVisible(...locators: Locator[]): Promise<void> {
  for (const locator of locators) {
    if (await locator.isVisible().catch(() => false)) {
      return;
    }
  }

  throw new Error('Expected at least one workflow state indicator to be visible.');
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
