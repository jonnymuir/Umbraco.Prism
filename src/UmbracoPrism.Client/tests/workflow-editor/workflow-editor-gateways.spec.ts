import { expect, test } from '@playwright/test';

function graphStoryUrl(): string {
  return '/iframe.html?id=workflow-editor-workflow-graph--gateway-representation&viewMode=story';
}

function editorStoryUrl(): string {
  return '/iframe.html?id=workflow-editor-editor-host--gateway-representation&viewMode=story';
}

test.describe('Workflow editor gateway representation', () => {
  test('renders split and join gateways as lane-owned graph nodes', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const storyEl = page.locator('prism-workflow-graph');
    await expect(storyEl).toBeVisible({ timeout: 10_000 });

    const splitGateway = storyEl.locator('[data-prism-gateway-kind="Split"][data-prism-gateway="review-split"]');
    const joinGateway = storyEl.locator('[data-prism-gateway-kind="Join"][data-prism-gateway="decision-join"]');

    await expect(splitGateway).toBeVisible();
    await expect(joinGateway).toBeVisible();
    await expect(splitGateway).toHaveAttribute('data-prism-lane', 'applicant');
    await expect(joinGateway).toHaveAttribute('data-prism-lane', 'applicant');
    await expect(splitGateway).toContainText('Review split');
    await expect(joinGateway).toContainText('Decision join');
  });

  test('styles branch and merge routes distinctly while preserving executable transitions', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const storyEl = page.locator('prism-workflow-graph');
    await expect(storyEl).toBeVisible({ timeout: 10_000 });

    // Slice C: routes belong to gateways. The Review split fans out into
    // three branches; the Decision join is fed by three per-stage feeder
    // splits, each carrying one merge route. Feeder-split → join edges
    // satisfy both the branch (source = Split) and merge (target = Join)
    // styling rules, so the branch-path count includes them too.
    await expect(storyEl.locator('.edge-path[data-prism-transition-from="review-split"]')).toHaveCount(3);
    await expect(storyEl.locator('.edge-path[data-prism-transition-to="decision-join"]')).toHaveCount(3);
    await expect(storyEl.locator('.edge-path.branch-path')).toHaveCount(6);
    await expect(storyEl.locator('.edge-path.merge-path')).toHaveCount(3);
    await expect(storyEl.locator('[data-prism-stage="start-request"]')).toBeVisible();
    await expect(storyEl.locator('[data-prism-stage="decision-confirmed"]')).toBeVisible();
  });

  test('supports keyboard selection for gateway nodes', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const splitGateway = page.locator('[data-prism-gateway="review-split"]');
    await splitGateway.focus();
    await expect(splitGateway).toBeFocused();
    await splitGateway.press('Enter');
    await expect(splitGateway).toHaveAttribute('aria-pressed', 'true');
  });

  test('shows gateway details in the inspector without turning preview into gateway runtime', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(editorStoryUrl());

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    const splitGateway = page.locator('[data-prism-gateway="review-split"]');
    await splitGateway.click();
    await splitGateway.press('e');

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible();
    await expect(inspector.locator('[data-prism-inspector-kind="gateway"]')).toBeVisible();
    await expect(inspector.locator('[data-prism-inspector-heading]')).toHaveText('Review split');
    await expect(inspector.locator('[data-prism-field="kind"]')).toContainText('Split gateway');
    await expect(page.getByRole('tab', { name: 'Canvas' })).toHaveAttribute('aria-selected', 'true');
    await expect(page.locator('[data-prism-preview-stage-name]')).toHaveCount(0);
  });

  test('surfaces gateways as gateway nodes in the canvas matrix', async ({ page }) => {
    // Slice 4 retired the linear "List view" mode. Gateway visibility is now proved
    // by the canvas slot-matrix rendering each authored gateway as a node with the
    // Split/Join kind attached. Slice C: with gateways owning their routes, the
    // Decision join is fed by per-stage feeder splits, so the demo fixture now
    // exposes five gateways (review-split + 3 feeder splits + decision-join).
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const storyEl = page.locator('prism-workflow-graph');
    await expect(storyEl).toBeVisible({ timeout: 10_000 });

    await expect(storyEl.locator('[data-prism-gateway]')).toHaveCount(5);
    await expect(storyEl.locator('[data-prism-gateway-kind="Split"]')).toHaveCount(4);
    await expect(storyEl.locator('[data-prism-gateway-kind="Join"]')).toHaveCount(1);
  });

  // ─── #84: Join gateways carry waiting information ─────────────────────────

  test('join gateway inspector shows gateway kind as Join — not a stage type', async ({ page }) => {
    // Join gateways are routing nodes, not action-bearing stages. The inspector must
    // communicate this clearly so authors understand the join holds waiting information,
    // not user-facing form content.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(editorStoryUrl());

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    const joinGateway = page.locator('[data-prism-gateway="decision-join"]');
    await joinGateway.click();
    await joinGateway.press('e');

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible();
    await expect(inspector.locator('[data-prism-inspector-kind="gateway"]')).toBeVisible();
    await expect(inspector.locator('[data-prism-field="kind"]')).toContainText('Join gateway',
      { timeout: 5_000 });
  });

  test('split gateway inspector does not show a waiting copy field', async ({ page }) => {
    // Waiting information belongs to join gateways only. A split gateway routes — it does
    // not wait. The inspector must not show a waiting copy field for split gateways.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(editorStoryUrl());

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    const splitGateway = page.locator('[data-prism-gateway="review-split"]');
    await splitGateway.click();
    await splitGateway.press('e');

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible();
    await expect(inspector.locator('[data-prism-inspector-kind="gateway"]')).toBeVisible();

    // A split gateway routes — it must not expose waiting copy fields to authors
    await expect(inspector.locator('[data-prism-field="waitingCopy"]')).toHaveCount(0,
      { timeout: 3_000 });
    await expect(inspector.locator('[data-prism-field="waitingInstructions"]')).toHaveCount(0,
      { timeout: 3_000 });
  });

  // ─── #84 pending: join gateway waiting copy field (needs Blathers implementation) ──

  test.skip('join gateway inspector shows a waiting copy field for authors to fill in', async ({ page }) => {
    // When #84 lands: the inspector for a join gateway must show a "Waiting copy" field
    // so authors can write the message users see while their lane waits for other lanes.
    // This keeps the waiting story on the gateway, not on a fake placeholder stage.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(editorStoryUrl());

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    const joinGateway = page.locator('[data-prism-gateway="decision-join"]');
    await joinGateway.click();
    await joinGateway.press('e');

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector.locator('[data-prism-field="waitingCopy"]')).toBeVisible();
  });
});
