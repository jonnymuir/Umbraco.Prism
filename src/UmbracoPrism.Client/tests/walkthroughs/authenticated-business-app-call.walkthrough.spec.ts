// Executable counterpart of docs/walkthroughs/authenticated-business-app-call.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
// A front-stage journey that calls the business app AS the signed-in member: their own bearer token loads
// and saves their record, scoped to their tenant, and the business app refuses every caller without one.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { step, signIn, businessAppOrigin, njfCaseworkerCredentials } from './support/walkthrough';

const appHost = new LiveAppHost();
const key = 'authenticated-business-app-call';

// A finished case persists for the member (requestPolicy single), so always start a fresh one.
const journeyUrl = '/update-my-details?action=start-new';

test.describe('Calling a business app as the signed-in member walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('a member sees their own record, is refused a bad change, and has a good one accepted', async ({ page }) => {
    await signIn(page);
    await page.goto(journeyUrl);

    await step(page, '01-start.png', { url: /\/update-my-details\/?/, heading: 'Update my details' }, key);
    await page.getByRole('button', { name: /continue/i }).click();

    // The record came from the business app, looked up with this member's own token for their tenant.
    await expect(page.getByText('Prism Demo (Keycloak)').first()).toBeVisible({ timeout: 60_000 });
    await expect(page.getByText('demo@prism.local').first()).toBeVisible();
    await step(page, '02-your-record.png', { url: /\/update-my-details\/?/, heading: 'Your contact details' }, key);

    // The business app validates the change itself and can refuse it. A refusal is a route, not an error page.
    await page.getByLabel('Telephone number').fill('not a number');
    await page.getByRole('button', { name: /continue/i }).click();
    await page.getByRole('button', { name: /submit/i }).click();
    await expect(page.getByText(/correct format/i).first()).toBeVisible({ timeout: 60_000 });
    await step(page, '03-refused.png', { url: /\/update-my-details\/?/, heading: 'We could not update your details' }, key);

    await page.getByRole('button', { name: /continue/i }).click();
    await page.getByLabel('Telephone number').fill('01632 960 001');
    await page.getByRole('button', { name: /continue/i }).click();
    await page.getByRole('button', { name: /submit/i }).click();

    // The reference is the business app's, not Prism's.
    await expect(page.getByText(/UPD-[0-9A-F]{8}/).first()).toBeVisible({ timeout: 60_000 });
    await step(page, '04-updated.png', { url: /\/update-my-details\/?/, heading: 'Details updated' }, key);
  });

  test('someone who can sign in to the tenant but is not a member is told so', async ({ page }) => {
    await signIn(page, njfCaseworkerCredentials);
    await page.goto(journeyUrl);
    await page.getByRole('button', { name: /continue/i }).click();

    await expect(page.getByText(/could not find your membership/i).first()).toBeVisible({ timeout: 60_000 });
    await step(page, '05-not-registered.png', { url: /\/update-my-details\/?/, heading: 'We could not find your membership' }, key);
  });

  test('the business app refuses every caller without a valid token, including the routes that used to be open', async ({ request }) => {
    const refused = [
      ['GET', '/api/backoffice/me'],
      ['GET', '/api/backoffice/profile'],
      ['PUT', '/api/backoffice/profile'],
      ['POST', '/contributions/submissions'],
      ['GET', '/contributions/submissions/anything/file'],
      // The anonymous support-system routes this app used to expose (one let an anonymous caller make the
      // server POST to any address). They no longer exist, and an unmatched path is refused, not revealed.
      ['GET', '/queue'],
      ['POST', '/submissions'],
      ['POST', '/queue/anything/decide'],
    ];

    for (const [method, route] of refused) {
      const response = await request.fetch(`${businessAppOrigin}${route}`, { method, ignoreHTTPSErrors: true });
      expect(response.status(), `${method} ${route} must not answer without a valid token`).toBe(401);
    }

    // A forged token (alg=none, the right issuer and audience) is refused by validation, not by a lookup.
    const forged = `${Buffer.from('{"alg":"none","typ":"JWT"}').toString('base64url')}.${Buffer.from(
      JSON.stringify({ iss: 'https://localhost:8443/realms/prism-dev', aud: 'prism-business-app', preferred_username: 'demo@prism.local', exp: 4102444800 })
    ).toString('base64url')}.`;
    const response = await request.get(`${businessAppOrigin}/api/backoffice/profile`, {
      headers: { Authorization: `Bearer ${forged}` },
      ignoreHTTPSErrors: true
    });
    expect(response.status()).toBe(401);
  });
});
