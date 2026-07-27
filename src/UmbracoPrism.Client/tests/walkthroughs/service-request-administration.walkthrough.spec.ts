// Executable counterpart of docs/walkthroughs/service-request-administration.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect, type Page } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import {
  assertHealthyPage,
  businessAppOrigin,
  openDashboard,
  openServiceBlueprintAdminFromDashboard,
  resetServiceBlueprints,
  signIn,
  step
} from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('ServiceBlueprint administration walkthrough', () => {
  test.fixme();
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await resetServiceBlueprints(request);
  });

  test('authenticated member sees the service blueprint admin entry point beside core dashboard actions', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    await step(page, '01-dashboard-admin-link.png', {
      url: /\/dashboard\/?$/,
      heading: /dashboard/i,
      skipHeading: true,
      screenshotSelector: '.dash-section:has(a[href*="/admin/service-desk"])'
    }, 'service-request-administration');

    await expect(page.getByRole('link', { name: 'View ServiceBlueprints' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'ServiceBlueprint Demos' })).toBeVisible();
    for (const serviceBlueprintTitle of ['Get in Touch', 'Apply for Planning Permission', 'Payment Demo', 'Request Information']) {
      await expect(serviceBlueprintDemoCard(page, serviceBlueprintTitle)).toBeVisible();
      await expect(serviceBlueprintDemoCard(page, serviceBlueprintTitle).getByRole('link', { name: 'Start' })).toBeVisible();
    }

    const adminLink = page.getByRole('link', { name: 'Open Admin' });
    await expect(adminLink).toBeVisible();
    await expect(adminLink).toHaveAttribute('href', `${businessAppOrigin}/admin/service-desk`);

    const editorCard = page.locator('.dash-card').filter({
      has: page.getByRole('heading', { name: 'Service Blueprint Editor' })
    }).first();
    await expect(editorCard).toBeVisible();
    await expect(editorCard.getByRole('link', { name: 'Open Editor' })).toHaveAttribute(
      'href',
      `${businessAppOrigin}/service-blueprint-editor`
    );
    await expect(editorCard.getByText('Direct Page')).toHaveCount(0);
  });

  test('service blueprint admin lists the four reference service blueprints and editor links', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);
    const adminPage = await openServiceBlueprintAdminFromDashboard(page);

    for (const serviceBlueprintKey of ['community-enquiry v1', 'information-request v1', 'payment-demo v1', 'planning v2']) {
      const definitionCard = adminPage.locator(`.def-card[data-service-blueprint-key="${serviceBlueprintKey}"]`).first();
      await expect(definitionCard).toBeVisible();
      await expect(definitionCard.getByRole('link', { name: 'Edit service blueprint' })).toHaveAttribute(
        'href',
        `/service-blueprint-editor?service blueprint=${serviceBlueprintKey}`
      );
    }

    await expect(adminPage.locator('.def-card[data-service-blueprint-key]')).toHaveCount(4);
    await expect(adminPage.getByText('No editor definition yet')).toHaveCount(0);
  });

  test('service blueprint admin shows completed community enquiries without obsolete reviewer actions', async ({ page }) => {
    await signIn(page);
    await submitCommunityEnquiry(page);
    await openDashboard(page);
    const adminPage = await openServiceBlueprintAdminFromDashboard(page);

    const row = serviceBlueprintInstanceRow(adminPage, 'community-enquiry');
    await expect(row).toContainText('Thank you');
    await expect(row.getByRole('button', { name: 'Approve' })).toHaveCount(0);
    await expect(row.getByRole('button', { name: 'Request Changes' })).toHaveCount(0);

    await step(adminPage, '02-admin-instance-list.png', {
      url: /https:\/\/localhost:7245\/admin\/service-blueprint\/?$/,
      heading: /service-blueprint admin/i,
      screenshotSelector: 'tbody tr[data-service-blueprint-key="community-enquiry"]'
    }, 'service-request-administration');
  });

  test('service blueprint admin definitions can be expanded and edited in-place', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);
    const adminPage = await openServiceBlueprintAdminFromDashboard(page);

    const definitionCard = adminPage.locator('.def-card').filter({ hasText: 'Get in Touch' }).first();
    await expect(definitionCard).toBeVisible();

    const header = definitionCard.locator('.def-header');
    await expect(header).toHaveAttribute('aria-expanded', 'false');
    await header.click();
    await expect(header).toHaveAttribute('aria-expanded', 'true');
    await expect(definitionCard.getByText('States (')).toBeVisible();
    await expect(definitionCard.getByText('Transitions (')).toBeVisible();

    await step(adminPage, '03-admin-definition-editor.png', {
      url: /https:\/\/localhost:7245\/admin\/service-blueprint\/?$/,
      heading: /service-blueprint admin/i,
      screenshotSelector: '.def-card.open'
    }, 'service-request-administration');

    await definitionCard.locator('button.btn-edit').click();
    await expect(adminPage.locator('#json-modal')).toBeVisible();
    await expect(adminPage.getByRole('button', { name: 'Apply Changes' })).toBeVisible();
    await adminPage.getByRole('button', { name: 'Cancel' }).click();
    await expect(adminPage.locator('#json-modal')).toBeHidden();
  });
});

async function submitCommunityEnquiry(page: Page): Promise<void> {
  await page.goto('/get-in-touch');
  await assertHealthyPage(page, { url: /\/get-in-touch/, heading: 'Your details' });
  await page.getByRole('button', { name: 'Submit' }).click();

  await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({ timeout: 30_000 });
}

function serviceBlueprintInstanceRow(page: Page, serviceBlueprintKey: string) {
  return page.locator(`tbody tr[data-service-blueprint-key="${serviceBlueprintKey}"]`).first();
}

function serviceBlueprintDemoCard(page: Page, title: string) {
  return page.locator('.dash-card').filter({ has: page.getByRole('heading', { name: title }) }).first();
}
