import { test, expect } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();

/**
 * Six-service-blueprint reference contract: validates that exactly the six demo service
 * blueprints in service-blueprints/ are available through the MockBusinessApp admin surface
 * and that all six have editor links (proving they're backed by authored sources).
 */
test.describe('Six-service-blueprint reference contract', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  const expectedServiceBlueprints = [
    'community-enquiry',
    'information-request',
    'money-modeller',
    'payment-demo',
    'planning',
    'planning-notification'
  ];

  test.beforeAll(async ({}, testInfo) => {
    testInfo.setTimeout(12 * 60_000);
    await appHost.start();
  });

  test.afterAll(async ({}, testInfo) => {
    testInfo.setTimeout(3 * 60_000);
    await appHost.stop();
  });

  test('admin screen lists exactly 6 service blueprints', async ({ page }) => {
    await page.goto('https://localhost:7245/admin/service-desk');

    await expect(page.getByRole('heading', { name: /service desk/i })).toBeVisible();

    // Each service blueprint should appear as a card with data-definition-key attribute
    for (const serviceBlueprintKey of expectedServiceBlueprints) {
      const serviceBlueprintCard = page.locator(`[data-definition-key="${serviceBlueprintKey}"]`);
      await expect(serviceBlueprintCard).toBeVisible({
        timeout: 5000
      });
    }

    // Count service blueprint cards to ensure no unexpected service blueprints
    const allServiceBlueprintCards = page.locator('[data-definition-key]');
    await expect(allServiceBlueprintCards).toHaveCount(6, {
      timeout: 5000
    });
  });

  test('all 6 service blueprints have editor links', async ({ page }) => {
    await page.goto('https://localhost:7245/admin/service-desk');

    await expect(page.getByRole('heading', { name: /service desk/i })).toBeVisible();

    // Each service blueprint should have an "Edit service blueprint" link
    for (const serviceBlueprintKey of expectedServiceBlueprints) {
      const serviceBlueprintCard = page.locator(`[data-definition-key="${serviceBlueprintKey}"]`);
      await expect(serviceBlueprintCard).toBeVisible();

      const editLink = serviceBlueprintCard.locator(`a[href="/service-blueprint-editor?serviceBlueprint=${serviceBlueprintKey}"]`);
      await expect(editLink).toBeVisible({
        timeout: 5000
      });
      await expect(editLink).toHaveText(/Edit service blueprint/i);
    }

    // No service blueprint should show "No editor definition yet"
    await expect(page.getByText('No editor definition yet')).not.toBeVisible();
  });

  test('service blueprint source API lists exactly 6 service blueprints', async ({ request }) => {
    const response = await request.get('https://localhost:7245/mockapp/service-blueprints', {
      ignoreHTTPSErrors: true
    });

    expect(response.ok()).toBeTruthy();

    const serviceBlueprints = await response.json();
    expect(Array.isArray(serviceBlueprints)).toBeTruthy();
    expect(serviceBlueprints).toHaveLength(6);

    const serviceBlueprintKeys = serviceBlueprints.map((w: any) => w.definitionKey).sort();
    expect(serviceBlueprintKeys).toEqual(expectedServiceBlueprints.sort());
  });

  test('all 6 service blueprints are loadable via service blueprint source API', async ({ request }) => {
    for (const serviceBlueprintKey of expectedServiceBlueprints) {
      const response = await request.get(
        `https://localhost:7245/mockapp/service-blueprints/${serviceBlueprintKey}`,
        {
          ignoreHTTPSErrors: true
        }
      );

      expect(response.ok()).toBeTruthy();

      const serviceBlueprint = await response.json();
      expect(serviceBlueprint).toBeTruthy();
      expect(serviceBlueprint.definitionKey).toBeTruthy();
    }
  });
});
