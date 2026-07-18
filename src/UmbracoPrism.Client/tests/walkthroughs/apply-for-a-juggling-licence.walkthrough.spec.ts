// Executable counterpart proving Prism CMS Workflow's core promise: the workflow editor is
// mounted natively in the Umbraco backoffice, definitions are authored/persisted entirely
// inside Umbraco (no separate business app), and one declarative definition serves both an
// anonymous visitor and a logged-in Prism Member — the member's Juggling Society membership
// (resolved via CmsWorkflowEngine's serviceInputsResolver extension point) defaults part of the
// form and applies a fee discount, purely from calc-scope wiring, with zero special-casing in
// the definition's JSON.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { step, signIn } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Apply for a juggling licence (CMS Workflow) walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test('anonymous visitor completes the application with the undiscounted fee', async ({ page }) => {
    // No signIn() — this is the whole point: a CMS Workflow journey is anonymous-first.
    await page.goto('/apply-for-a-juggling-licence');

    await step(page, '01-eligibility.png', {
      url: /\/apply-for-a-juggling-licence/,
      heading: 'Eligibility'
    }, 'apply-for-a-juggling-licence');

    await page.getByLabel('I confirm I am aged 16 or over').check();
    await page.getByLabel('I confirm I have a UK postal address').check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible({ timeout: 30_000 });
    await page.getByLabel('Full name').fill('Alex Juggler');
    await page.getByLabel('Email address').fill('alex.anonymous@example.test');
    await page.locator('#date-of-birth-day').fill('12');
    await page.locator('#date-of-birth-month').fill('3');
    await page.locator('#date-of-birth-year').fill('1990');
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Licence type' })).toBeVisible({ timeout: 30_000 });

    // No membership record for an anonymous visitor — the stat-group is showWhen-gated off.
    // The server renders it into the DOM behind a `hidden` attribute (so the live-form runtime
    // can reveal it without a round trip if inputs change), so it must still be asserted
    // not-visible rather than absent.
    await expect(page.getByText('Your Juggling Society membership')).not.toBeVisible();

    await page.getByLabel('Recreational').check();
    await page.getByLabel('I confirm the information I have given is accurate').check();

    await step(page, '02-licence-type.png', {
      url: /\/apply-for-a-juggling-licence/,
      heading: 'Licence type'
    }, 'apply-for-a-juggling-licence');

    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('Alex Juggler')).toBeVisible();
    // Standard fee, no membership discount.
    await expect(page.getByText('£25')).toBeVisible();

    await step(page, '03-check-answers.png', {
      url: /\/apply-for-a-juggling-licence/,
      heading: 'Check your answers'
    }, 'apply-for-a-juggling-licence');

    await page.getByRole('button', { name: 'Submit' }).click();

    await expect(page.getByRole('heading', { name: 'Application submitted' })).toBeVisible({ timeout: 30_000 });

    await step(page, '04-confirmation.png', {
      url: /\/apply-for-a-juggling-licence/,
      heading: 'Application submitted'
    }, 'apply-for-a-juggling-licence');
  });

  test('logged-in member sees their membership tier and the discounted fee', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-a-juggling-licence');

    await expect(page.getByRole('heading', { name: 'Eligibility' })).toBeVisible({ timeout: 30_000 });
    await page.getByLabel('I confirm I am aged 16 or over').check();
    await page.getByLabel('I confirm I have a UK postal address').check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Your details' })).toBeVisible({ timeout: 30_000 });
    await page.getByLabel('Full name').fill('Demo Member');
    await page.getByLabel('Email address').fill('member@example.test');
    await page.locator('#date-of-birth-day').fill('5');
    await page.locator('#date-of-birth-month').fill('6');
    await page.locator('#date-of-birth-year').fill('1985');
    await page.getByRole('button', { name: 'Continue' }).click();

    // demo@prism.local is seeded as a Competitive-tier Juggling Society member — the same
    // definition that showed nothing for the anonymous visitor now shows this, driven entirely
    // by CmsWorkflowEngine's serviceInputsResolver resolving real membership data for a
    // logged-in member.
    await expect(page.getByRole('heading', { name: 'Licence type' })).toBeVisible({ timeout: 30_000 });
    const membershipStat = page.getByText('Your Juggling Society membership').locator('..');
    await expect(membershipStat).toBeVisible();
    await expect(membershipStat).toContainText('Competitive');

    await step(page, '10-member-licence-type.png', {
      url: /\/apply-for-a-juggling-licence/,
      heading: 'Licence type'
    }, 'apply-for-a-juggling-licence');

    await page.getByLabel('Competitive').check();
    await page.getByLabel('I confirm the information I have given is accurate').check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible({ timeout: 30_000 });
    // Competitive-tier discount applied.
    await expect(page.getByText('£20')).toBeVisible();

    await step(page, '11-member-check-answers.png', {
      url: /\/apply-for-a-juggling-licence/,
      heading: 'Check your answers'
    }, 'apply-for-a-juggling-licence');

    await page.getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByRole('heading', { name: 'Application submitted' })).toBeVisible({ timeout: 30_000 });
  });

  test('backoffice CMS Workflow authoring API requires admin auth', async ({ request }) => {
    const response = await request.get(
      'https://localhost:44345/umbraco/management/api/v1/prism/cms-workflows/queues',
      { ignoreHTTPSErrors: true } as never
    );
    expect(response.status()).toBe(401);
  });
});
