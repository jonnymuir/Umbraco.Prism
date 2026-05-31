// Planning-notification walkthrough retained for historical screenshots only.
// The live localhost-auth lane now runs against the authored "planning" workflow
// (Declaration → Application Form → Check your answers → Application submitted).
// See planning-workflow-complete.walkthrough.spec.ts for the current contract.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { step, signIn, resetWorkflows } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe.skip('Planning notification walkthrough', () => {
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

  test('happy path: user completes multi-step planning application', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    await step(page, '01-initial.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Describe your project'
    }, 'planning-notification');

    await page.getByLabel('Project name').fill('Loft conversion');
    await page.getByLabel('Describe the proposed works').fill(
      'Converting existing loft space into habitable bedroom with dormer window'
    );
    await page.getByLabel('Property address').fill('456 Oak Avenue\nWoodlands\nWD3 4EF');
    await step(page, '02-project-filled.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Describe your project'
    }, 'planning-notification');
    await page.getByRole('button', { name: 'Continue' }).click();

    await step(page, '03-work-type.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Type of work'
    }, 'planning-notification');

    await page.getByRole('radio', { name: 'Other' }).check();
    await expect(page.getByLabel('Describe the type of work')).toBeVisible({ timeout: 5_000 });
    await step(page, '04-work-type-conditional.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Type of work'
    }, 'planning-notification');

    await page.getByLabel('Describe the type of work').fill('Listed building restoration with specialist masonry');
    await page.getByRole('radio', { name: 'Extension or alteration' }).check();
    await page.getByRole('radio', { name: 'Yes' }).first().check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await step(page, '05-timeline-cost.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Timeline and cost'
    }, 'planning-notification');

    await page.locator('#proposedStartDate-day').fill('1');
    await page.locator('#proposedStartDate-month').fill('9');
    await page.locator('#proposedStartDate-year').fill('2025');
    await page.getByLabel('Estimated duration in weeks').fill('16');
    await page.getByLabel('Estimated cost of works').fill('35000.75');
    await step(page, '06-timeline-filled.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Timeline and cost'
    }, 'planning-notification');
    await page.getByRole('button', { name: 'Continue' }).click();

    await step(page, '07-affected-parties.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Affected parties'
    }, 'planning-notification');

    await page.getByRole('checkbox', { name: 'Neighbouring properties' }).check();
    await page.getByRole('checkbox', { name: 'Conservation area' }).check();
    await page.getByRole('radio', { name: 'Yes' }).last().check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await step(page, '08-check-answers.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Check your answers',
      fullPage: true
    }, 'planning-notification');

    await page.getByRole('button', { name: 'Submit' }).click();
    await expect(page.locator('.govuk-panel--confirmation')).toBeVisible({ timeout: 30_000 });
    // Step 9: confirmation panel uses govuk-panel--confirmation, not a standard heading role.
    await step(page, '09-confirmation.png', {
      url: /\/apply-for-planning-permission/,
      heading: 'Application received',
      skipHeading: true
    }, 'planning-notification');
  });

  test('conditional reveal: Other work type shows textarea', async ({ page }) => {
    await signIn(page);
    await page.goto('/apply-for-planning-permission');

    // Navigate to work-type state
    await page.getByLabel('Project name').fill('Test');
    await page.getByLabel('Describe the proposed works').fill('Test');
    await page.getByLabel('Property address').fill('Test');
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Type of work' })).toBeVisible();

    // Select "Other" — should reveal conditional textarea from ConditionalChildren
    await page.getByRole('radio', { name: 'Other' }).check();

    const conditionalTextarea = page.getByLabel('Describe the type of work');
    await expect(conditionalTextarea).toBeVisible();
    await conditionalTextarea.fill('Listed building restoration with specialist masonry');

    // Switch to different option
    await page.getByRole('radio', { name: 'New building' }).check();
    await expect(conditionalTextarea).toBeHidden();
  });
});
