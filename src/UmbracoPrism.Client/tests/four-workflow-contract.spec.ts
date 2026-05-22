import { test, expect } from '@playwright/test';

import { LiveAppHost } from './support/live-app-host';

const appHost = new LiveAppHost();

/**
 * Four-workflow reference contract: validates that exactly 4 demo workflows
 * are available through the MockBusinessApp admin surface and that all 4
 * have editor links (proving they're backed by authored sources).
 */
test.describe('Four-workflow reference contract', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  const expectedWorkflows = [
    'community-enquiry',
    'information-request',
    'payment-demo',
    'planning'
  ];

  test.beforeAll(async ({}, testInfo) => {
    testInfo.setTimeout(12 * 60_000);
    await appHost.start();
  });

  test.afterAll(async ({}, testInfo) => {
    testInfo.setTimeout(3 * 60_000);
    await appHost.stop();
  });

  test('admin screen lists exactly 4 workflows', async ({ page }) => {
    await page.goto('https://localhost:7245/admin/workflow');

    await expect(page.getByRole('heading', { name: /workflow admin/i })).toBeVisible();

    // Each workflow should appear as a card with data-workflow-key attribute
    for (const workflowKey of expectedWorkflows) {
      const workflowCard = page.locator(`[data-workflow-key="${workflowKey}"]`);
      await expect(workflowCard).toBeVisible({
        timeout: 5000
      });
    }

    // Count workflow cards to ensure no unexpected workflows
    const allWorkflowCards = page.locator('[data-workflow-key]');
    await expect(allWorkflowCards).toHaveCount(4, {
      timeout: 5000
    });
  });

  test('all 4 workflows have editor links', async ({ page }) => {
    await page.goto('https://localhost:7245/admin/workflow');

    await expect(page.getByRole('heading', { name: /workflow admin/i })).toBeVisible();

    // Each workflow should have an "Edit workflow" link
    for (const workflowKey of expectedWorkflows) {
      const workflowCard = page.locator(`[data-workflow-key="${workflowKey}"]`);
      await expect(workflowCard).toBeVisible();

      const editLink = workflowCard.locator(`a[href="/workflow-editor?workflow=${workflowKey}"]`);
      await expect(editLink).toBeVisible({
        timeout: 5000
      });
      await expect(editLink).toHaveText(/Edit workflow/i);
    }

    // No workflow should show "No editor definition yet"
    await expect(page.getByText('No editor definition yet')).not.toBeVisible();
  });

  test('authoring API lists exactly 4 workflows', async ({ request }) => {
    const response = await request.get('https://localhost:7245/api/workflow-authoring/workflows', {
      ignoreHTTPSErrors: true
    });

    expect(response.ok()).toBeTruthy();

    const workflows = await response.json();
    expect(Array.isArray(workflows)).toBeTruthy();
    expect(workflows).toHaveLength(4);

    const workflowKeys = workflows.map((w: any) => w.workflowKey).sort();
    expect(workflowKeys).toEqual(expectedWorkflows.sort());
  });

  test('all 4 workflows are loadable via authoring API', async ({ request }) => {
    for (const workflowKey of expectedWorkflows) {
      const response = await request.get(
        `https://localhost:7245/api/workflow-authoring/workflows/${workflowKey}`,
        {
          ignoreHTTPSErrors: true
        }
      );

      expect(response.ok()).toBeTruthy();

      const workflow = await response.json();
      expect(workflow).toBeTruthy();
      expect(workflow.definitionKey).toBeTruthy();
    }
  });
});
