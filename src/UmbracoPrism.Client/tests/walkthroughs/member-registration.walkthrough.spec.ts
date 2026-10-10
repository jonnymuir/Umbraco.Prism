// Executable counterpart of docs/walkthroughs/member-registration.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
// A new person creates an account at the identity provider (Keycloak), verifies their email, and the journey
// then creates their membership in the business app with their own token. The business app takes who they are
// and which tenant they join from that verified token, never from the form.
import { test, expect, type Page } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { step, businessAppOrigin, mailpitOrigin, demoCredentials, signIn } from './support/walkthrough';

const appHost = new LiveAppHost();
const key = 'member-registration';

// Nothing about a brand-new person exists anywhere until they register, so each run makes its own.
const unique = Date.now().toString(36);
const newcomer = { email: `robin.${unique}@prism.local`, password: 'Passw0rd!reg', first: 'Robin', last: 'Reed' };

async function verificationLinkFor(email: string): Promise<string> {
  // Keycloak sends the email as it finishes the registration form; poll the catcher for it.
  for (let attempt = 0; attempt < 30; attempt++) {
    const found = await fetch(`${mailpitOrigin}/api/v1/search?query=${encodeURIComponent(`to:${email}`)}`);
    const { messages } = (await found.json()) as { messages: { ID: string }[] };
    if (messages.length > 0) {
      const message = (await (await fetch(`${mailpitOrigin}/api/v1/message/${messages[0].ID}`)).json()) as { Text: string };
      const link = message.Text.match(/https?:\/\/\S+action-token\S+/);
      if (link) return link[0];
    }
    await new Promise((resolve) => setTimeout(resolve, 1_000));
  }
  throw new Error(`No verification email arrived for ${email}`);
}

async function fillRegistrationForm(page: Page): Promise<void> {
  await page.getByLabel('First name').fill(newcomer.first);
  await page.getByLabel('Last name').fill(newcomer.last);
  await page.getByLabel('Email').fill(newcomer.email);
  await page.locator('#password').fill(newcomer.password);
  await page.locator('#password-confirm').fill(newcomer.password);
  await page.getByRole('button', { name: /register/i }).click();
}

test.describe('Registering a member walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
    await fetch(`${mailpitOrigin}/api/v1/messages`, { method: 'DELETE' });
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('a new person creates an account, verifies their email, and becomes a member', async ({ page }) => {
    // The identity provider owns registration: this link asks Keycloak for its registration page (OIDC prompt=create).
    await page.goto('/auth/register?returnUrl=/register-as-a-member');
    await expect(page.getByRole('heading', { name: /register/i }).first()).toBeVisible({ timeout: 120_000 });
    await fillRegistrationForm(page);

    // They cannot continue until they have proved they control the address.
    await expect(page.getByText(/verify your email address/i).first()).toBeVisible({ timeout: 60_000 });
    // Following the link in the same browser finishes the sign-in, so Keycloak sends them straight back.
    await page.goto(await verificationLinkFor(newcomer.email));

    // Back in the journey, signed in as the new person.
    await expect(page).toHaveURL(/\/register-as-a-member\/?/, { timeout: 120_000 });
    await step(page, '01-become-a-member.png', { url: /\/register-as-a-member\/?/, heading: 'Become a member' }, key);
    await page.getByRole('button', { name: /continue/i }).click();

    // The business app was asked, as this person, whether they already have a membership. They do not.
    await expect(page.getByLabel('Your name')).toBeVisible({ timeout: 60_000 });
    await step(page, '02-your-details.png', { url: /\/register-as-a-member\/?/, heading: 'Your details' }, key);
    await page.getByLabel('Your name').fill('Robin Reed');
    await page.getByLabel('Telephone number').fill('01632 960 123');
    await page.getByLabel('phone', { exact: true }).check();
    await page.getByRole('button', { name: /continue/i }).click();
    await page.getByRole('button', { name: /submit/i }).click();

    // The membership id is the business app's, issued for the tenant their token belongs to.
    await expect(page.getByText(/MBR-[0-9A-F]{8}/).first()).toBeVisible({ timeout: 60_000 });
    await step(page, '03-member.png', { url: /\/register-as-a-member\/?/, heading: 'You are now a member' }, key);

    // The new membership works for the rest of the service: Update my details finds them.
    await page.goto('/update-my-details?action=start-new');
    await page.getByRole('button', { name: /continue/i }).click();
    await expect(page.getByText(newcomer.email).first()).toBeVisible({ timeout: 60_000 });
    await expect(page.getByText('Robin Reed').first()).toBeVisible();
    await expect(page.getByLabel('Telephone number')).toHaveValue('01632 960 123');
    await step(page, '04-their-record.png', { url: /\/update-my-details\/?/, heading: 'Your contact details' }, key);
  });

  test('someone who is already a member is told so, not given a second membership', async ({ page }) => {
    await signIn(page, demoCredentials);
    await page.goto('/register-as-a-member?action=start-new');
    await page.getByRole('button', { name: /continue/i }).click();

    await expect(page.getByText(/you are already a member/i).first()).toBeVisible({ timeout: 60_000 });
    await step(page, '05-already-a-member.png', { url: /\/register-as-a-member\/?/, heading: 'You are already a member' }, key);
  });

  test('the business app will not register anyone without a valid token', async ({ request }) => {
    const response = await request.post(`${businessAppOrigin}/api/backoffice/members`, {
      data: { name: 'Robin Reed', contactPreference: 'email' },
      ignoreHTTPSErrors: true
    });
    expect(response.status()).toBe(401);
  });
});
