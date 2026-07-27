import { expect, test } from '@playwright/test';

/**
 * Editor rendering tests for the three service blueprints migrated to the new
 * queues/gateways/routes format: planning, community-enquiry, information-request.
 *
 * These prove that hydrateServiceBlueprintDefinition correctly normalises the new
 * JSON shape (key/title/type on stages and gateways) and that the canvas
 * renders stages in their lane bands, shows gateway nodes, and keeps routes
 * flowing correctly.
 */

function graphStoryUrl(storyName: string): string {
  return `/iframe.html?id=service-blueprint-editor-service-blueprint-graph--${storyName}&viewMode=story`;
}

// ─── Planning Application (migrated) ─────────────────────────────────────────

test.describe('Planning service blueprint — migrated format', () => {
  const storyUrl = graphStoryUrl('planning-migrated');

  test('canvas loads without errors and renders stages', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const stages = graph.locator('[data-prism-stage]');
    await expect(stages.first()).toBeVisible({ timeout: 5_000 });
    expect(await stages.count()).toBe(4);
  });

  test('all stages are grouped in a queue lane band', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const lanes = graph.locator('[data-prism-role-queue]');
    await expect(lanes.first()).toBeVisible({ timeout: 5_000 });
    expect(await lanes.count()).toBeGreaterThanOrEqual(1);

    // All stages must carry data-prism-queue and resolve to the applicant queue.
    const stages = graph.locator('[data-prism-stage]');
    await expect(stages.first()).toBeVisible({ timeout: 5_000 });
    const laneAttrs = await stages.evaluateAll(els =>
      els.map(el => el.getAttribute('data-prism-queue'))
    );
    expect(laneAttrs.every(attr => attr === 'applicant')).toBe(true);
  });

  test('Split gateways are visible as gateway nodes', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const gateways = graph.locator('[data-prism-gateway-kind="Split"]');
    await expect(gateways.first()).toBeVisible({ timeout: 5_000 });
    expect(await gateways.count()).toBe(3);
  });

  test('stages have distinct Y positions (not all stacked at same row)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(graph.locator('[data-prism-stage]').first()).toBeVisible({ timeout: 5_000 });

    const tops = await graph.locator('[data-prism-stage]').evaluateAll(
      els => els.map(el => el.getBoundingClientRect().top)
    );
    const uniqueTops = new Set(tops.map(t => Math.round(t)));
    expect(uniqueTops.size).toBeGreaterThan(1);
  });

  test('routes flow from stages through gateways', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(graph.locator('[data-prism-stage]').first()).toBeVisible({ timeout: 5_000 });

    const edges = graph.locator('.edge-path');
    expect(await edges.count()).toBeGreaterThan(0);
  });
});

// ─── Community Enquiry (migrated) ─────────────────────────────────────────────

test.describe('Community Enquiry service blueprint — migrated format', () => {
  const storyUrl = graphStoryUrl('community-enquiry');

  test('canvas loads without errors and renders stages', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const stages = graph.locator('[data-prism-stage]');
    await expect(stages.first()).toBeVisible({ timeout: 5_000 });
    expect(await stages.count()).toBe(2);
  });

  test('stages are grouped in a queue lane band', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const lanes = graph.locator('[data-prism-role-queue]');
    await expect(lanes.first()).toBeVisible({ timeout: 5_000 });
    expect(await lanes.count()).toBeGreaterThanOrEqual(1);
  });

  test('Split gateway is visible and identified correctly', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const splitGateway = graph.locator('[data-prism-gateway="route-submitted"]');
    await expect(splitGateway).toBeVisible({ timeout: 5_000 });
    await expect(splitGateway).toHaveAttribute('data-prism-gateway-kind', 'Split');
  });

  test('gateway title is correctly hydrated from title field', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    // Single-route Split gateways render as pills (showing the trigger label as text).
    // The displayName ("Route to submitted") is surfaced via aria-label for accessibility.
    const gateway = graph.locator('[data-prism-gateway="route-submitted"]');
    await expect(gateway).toBeVisible({ timeout: 5_000 });
    await expect(gateway).toHaveAttribute('aria-label', /Route to submitted/);
  });
});

// ─── Information Request (migrated — with Join gateway) ───────────────────────

test.describe('Information Request service blueprint — migrated format', () => {
  const storyUrl = graphStoryUrl('information-request');

  test('canvas loads without errors and renders all three stages', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1080 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const stages = graph.locator('[data-prism-stage]');
    await expect(stages.first()).toBeVisible({ timeout: 5_000 });
    expect(await stages.count()).toBe(3);
  });

  test('two lane bands are visible for applicant and caseworker queues', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1080 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const lanes = graph.locator('[data-prism-role-queue]');
    await expect(lanes.first()).toBeVisible({ timeout: 5_000 });
    expect(await lanes.count()).toBeGreaterThanOrEqual(2);
  });

  test('Split and Join gateways are both visible', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1080 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    await expect(graph.locator('[data-prism-gateway-kind="Split"]').first()).toBeVisible({ timeout: 5_000 });
    await expect(graph.locator('[data-prism-gateway-kind="Join"]').first()).toBeVisible({ timeout: 5_000 });
  });

  test('Join gateway key is review-complete and is correctly typed', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1080 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    const joinGateway = graph.locator('[data-prism-gateway="review-complete"]');
    await expect(joinGateway).toBeVisible({ timeout: 5_000 });
    await expect(joinGateway).toHaveAttribute('data-prism-gateway-kind', 'Join');
  });

  test('caseworker stage is in the caseworker lane, not the applicant lane', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1080 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(graph.locator('[data-prism-stage]').first()).toBeVisible({ timeout: 5_000 });

    // data-prism-queue on stages was added by Blathers (prism-service-blueprint-graph.ts).
    // Verify caseworker-review stage resolves to the caseworker queue lane.
    const caseworkerStage = graph.locator('[data-prism-stage="caseworker-review"]');
    await expect(caseworkerStage).toBeVisible();
    await expect(caseworkerStage).toHaveAttribute('data-prism-queue', 'caseworker');

    // The caseworker-route gateway also carries data-prism-queue="caseworker".
    const caseworkerGateway = graph.locator('[data-prism-gateway="caseworker-route"]');
    await expect(caseworkerGateway).toBeVisible();
    await expect(caseworkerGateway).toHaveAttribute('data-prism-queue', 'caseworker');
  });

  test('stages have distinct Y positions — Join gateway DAG flows top-to-bottom', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1080 });
    await page.goto(storyUrl);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(graph.locator('[data-prism-stage]').first()).toBeVisible({ timeout: 5_000 });

    const tops = await graph.locator('[data-prism-stage]').evaluateAll(
      els => els.map(el => el.getBoundingClientRect().top)
    );
    const uniqueTops = new Set(tops.map(t => Math.round(t)));
    expect(uniqueTops.size).toBeGreaterThan(1);
  });
});
