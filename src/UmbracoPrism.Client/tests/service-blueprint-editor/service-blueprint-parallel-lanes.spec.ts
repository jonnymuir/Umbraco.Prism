import { expect, test } from '@playwright/test';
// TODO Slice E: re-cert after gateway-pill rendering + simulation reshape. See .squad/decisions/inbox/copilot-slice-d-close-out.md.

// Tests for the merged #83 + #84 + #85 slice.
// Proving that parallel lanes exist as independent, non-overwriting entities in the
// service blueprint editor. These tests target the gateway-representation story which contains
// both applicant and caseworker lanes with split and join gateways.

function graphStoryUrl(): string {
  return '/iframe.html?id=service-blueprint-editor-service-blueprint-graph--gateway-representation&viewMode=story';
}

function editorStoryUrl(): string {
  return '/iframe.html?id=service-blueprint-editor-editor-host--gateway-representation&viewMode=story';
}

test.describe('ServiceBlueprint editor parallel lanes', () => {
  // ─── #83: Both lanes are visible simultaneously ───────────────────────────

  test('multiple lanes are visible simultaneously in the graph canvas', async ({ page }) => {
    // The graph must show all lanes at the same time — authors need to see the whole
    // flow across lanes without switching between views.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const lanes = graph.locator('[data-prism-role-queue]');
    const laneCount = await lanes.count();
    expect(laneCount).toBeGreaterThanOrEqual(2,
      { message: 'A multi-lane service blueprint must show at least two lanes simultaneously' });
  });

  test('each lane column contains its own stages — no stage appears in two lanes simultaneously', async ({ page }) => {
    // Every stage lives in one lane column. Lane columns are the structural semantic unit.
    // A stage appearing under two different lane columns would mean lane ownership has collapsed.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    // Each lane column must contain at least one stage
    const laneCols = graph.locator('[data-prism-role-queue]');
    const laneCount = await laneCols.count();
    expect(laneCount).toBeGreaterThanOrEqual(2);

    const stageKeysPerLane: string[][] = [];
    for (let i = 0; i < laneCount; i++) {
      const laneStages = laneCols.nth(i).locator('[data-prism-stage]');
      const count = await laneStages.count();
      const keys: string[] = [];
      for (let j = 0; j < count; j++) {
        const key = await laneStages.nth(j).getAttribute('data-prism-stage');
        if (key) keys.push(key);
      }
      stageKeysPerLane.push(keys);
    }

    // No stage key should appear in more than one lane column
    const allKeys = stageKeysPerLane.flat();
    const uniqueKeys = new Set(allKeys);
    expect(uniqueKeys.size).toBe(allKeys.length,
      { message: 'Each stage must belong to exactly one lane — no stage key appears in two lane columns' });
  });

  test.fixme('split gateway belongs to one lane and does not span all lanes', async ({ page }) => {
    // A split gateway is owned by one lane (the one that starts the split).
    // It must not appear as owned by multiple lanes simultaneously.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const splitGateway = graph.locator('[data-prism-gateway-kind="Split"]');
    await expect(splitGateway).toBeVisible();

    // The split gateway has exactly one lane — the one that owns the branching decision
    const laneAttr = await splitGateway.first().getAttribute('data-prism-queue');
    expect(laneAttr).toBeTruthy();
    expect(laneAttr).not.toContain(',');
  });

  test('join gateway belongs to one lane and shows waiting attribution for that lane', async ({ page }) => {
    // A join gateway is owned by one lane — the one that holds the waiting story.
    // Its lane attribution must match the lane that waits, not all incoming lanes.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const joinGateway = graph.locator('[data-prism-gateway-kind="Join"]');
    await expect(joinGateway).toBeVisible();

    const laneAttr = await joinGateway.first().getAttribute('data-prism-queue');
    expect(laneAttr).toBeTruthy();
    expect(laneAttr).not.toContain(',');
  });

  // ─── #83: Selecting a stage does not affect other lanes ───────────────────

  test('selecting a stage in one lane does not collapse or clear another lane column', async ({ page }) => {
    // Lane columns must remain stable after selection interactions. Clicking a stage in one
    // lane must not cause another lane's content to disappear or its heading to be removed.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    // Record lane column headings before selection
    const laneCols = graph.locator('[data-prism-role-queue]');
    const laneCount = await laneCols.count();
    expect(laneCount).toBeGreaterThanOrEqual(2);

    // Select any stage in the graph — the first lane column may only hold a gateway node
    const anyStage = graph.locator('[data-prism-stage]').first();
    await expect(anyStage).toBeVisible();
    await anyStage.click();

    // All lane columns must still be present after selection.
    // Gateway nodes carry a data-prism-queue attribute but are rendered as graph siblings —
    // they are not DOM children of the lane column containers.
    await expect(laneCols).toHaveCount(laneCount,
      { timeout: 3_000 });
  });

  // ─── #84: Gateway nodes are distinct from stage nodes in the graph ────────

  test('gateway nodes and stage nodes are visually distinguishable in the graph', async ({ page }) => {
    // Authors must be able to tell stages (action-bearing) from gateways (routing) at a glance.
    // Stages use [data-prism-stage]; gateways use [data-prism-gateway].
    // These selectors must not overlap — an element cannot be both a stage and a gateway.
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(graphStoryUrl());

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const stages = graph.locator('[data-prism-stage]');
    const gateways = graph.locator('[data-prism-gateway]');

    await expect(stages.first()).toBeVisible({ timeout: 5_000 });
    await expect(gateways.first()).toBeVisible({ timeout: 5_000 });

    expect(await stages.count()).toBeGreaterThan(0);
    expect(await gateways.count()).toBeGreaterThan(0);

    // No element should carry BOTH data-prism-stage and data-prism-gateway — they are distinct kinds
    const ambiguousNodes = graph.locator('[data-prism-stage][data-prism-gateway]');
    await expect(ambiguousNodes).toHaveCount(0);
  });

  // ─── #85 pending: parallel lane cursor independence (needs engine implementation) ──

  test.skip('the runtime shows both lanes as active simultaneously when a split gateway fires', async ({ page }) => {
    // When #85 lands: after a split gateway, the simulation panel must show two active
    // cursor positions — one per lane — rather than one global "current stage."
    // This proves independent cursors are tracked without one overwriting the other.
    await page.goto(editorStoryUrl());
    // TODO: use simulation tab, fire the split, assert two [data-prism-simulation-cursor] elements
  });

  test.skip('the runtime join gateway only releases after both required lanes have arrived', async ({ page }) => {
    // When #85 lands: the simulation must show the join as "waiting" with a visible
    // explanation of which lanes are still outstanding. The join must not release
    // after just one lane arrives if two are required.
    await page.goto(editorStoryUrl());
    // TODO: use simulation tab, advance one lane, assert join shows waiting state,
    //       advance second lane, assert join releases to next stage
  });

  test.skip('completing lanes in reverse order produces the same service blueprint outcome', async ({ page }) => {
    // When #85 lands: lane B completing before lane A must produce the same converged
    // outcome as lane A completing first. Deterministic convergence regardless of order.
    await page.goto(editorStoryUrl());
    // TODO: run two separate simulations, one A-first and one B-first, compare outcomes
  });
});
