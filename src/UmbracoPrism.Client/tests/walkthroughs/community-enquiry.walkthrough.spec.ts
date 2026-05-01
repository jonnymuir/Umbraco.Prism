// Executable counterpart of docs/walkthroughs/community-enquiry.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { assertHealthyPage, step, signIn, resetWorkflows } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Community enquiry walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await resetWorkflows(request);
  });

  test('happy path: user submits a general enquiry', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');

    await step(page, '01-initial.png', {
      url: /\/get-in-touch/,
      heading: 'Tell us about your enquiry'
    }, 'community-enquiry');

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
    await step(page, '02-conditional-reveal.png', {
      url: /\/get-in-touch/,
      heading: 'Tell us about your enquiry'
    }, 'community-enquiry');

    await page.getByRole('textbox', { name: /specify.*enquiry/i }).fill('Partnership enquiry');
    await page.getByRole('radio', { name: 'General enquiry' }).check();
    await page.getByLabel('Tell us more').fill(
      'I would like to learn more about Prism integration options for our Umbraco site.'
    );
    await page.getByRole('checkbox', { name: 'Umbraco CMS' }).check();
    await page.getByRole('checkbox', { name: '.NET Development' }).check();
    await step(page, '03-form-filled.png', {
      url: /\/get-in-touch/,
      heading: 'Tell us about your enquiry'
    }, 'community-enquiry');

    await page.getByRole('button', { name: 'Submit' }).click();
    await step(page, '04-under-review.png', {
      url: /\/get-in-touch/,
      heading: 'Your enquiry is with us'
    }, 'community-enquiry');
  });

  test('conditional reveal: Other enquiry type shows sub-field', async ({ page }) => {
    await signIn(page);
    await page.goto('/get-in-touch');

    await assertHealthyPage(page, { url: /\/get-in-touch/, heading: 'Tell us about your enquiry' });

    // Full name and Email address are readonly (pre-populated from claims)
    await expect(page.getByLabel('Full name')).toHaveValue('Demo User');
    await expect(page.getByLabel('Email address')).toHaveValue('demo@prism.local');
    await page.locator('select#your-role').selectOption('Other');

    // Select "Other" enquiry type — should reveal conditional field
    await page.getByRole('radio', { name: 'Other' }).check();

    const conditionalField = page.getByLabel('Please specify your enquiry type');
    await expect(conditionalField).toBeVisible();
    await conditionalField.fill('Partnership enquiry');

    // Switch back and verify field is hidden
    await page.getByRole('radio', { name: 'General enquiry' }).check();
    await expect(conditionalField).toBeHidden();
  });
});
