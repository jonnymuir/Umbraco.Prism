import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';

/**
 * Manual canvas arrangement: dragging nodes persists positions in the
 * definition's layout block, dropping into another lane reassigns the queue,
 * Tidy layout restores the automatic arrangement, and every gesture is a
 * single undoable history entry.
 */

const GRAPH_STORY = '/iframe.html?id=service-blueprint-editor-service-blueprint-graph--payment-demo-graph&viewMode=story';
const EDITOR_STORY = '/iframe.html?id=service-blueprint-editor-editor-host--planning-service-blueprint&viewMode=story';

type LayoutUpdate = {
  layoutNodeIds: string[];
  queuesByStage: Record<string, string>;
};

declare global {
  interface Window {
    __layoutUpdates?: LayoutUpdate[];
  }
}

async function gotoGraphStory(page: Page, url: string) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(url);
  await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });
}

async function recordServiceBlueprintUpdates(page: Page) {
  await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
    window.__layoutUpdates = [];
    graphElement.addEventListener('service-blueprint-updated', event => {
      const serviceBlueprint = (event as CustomEvent<{ serviceBlueprint: {
        layout?: { nodes?: Record<string, unknown> };
        states: Array<{ stateKey: string; queueKey?: string }>;
      } }>).detail.serviceBlueprint;
      window.__layoutUpdates!.push({
        layoutNodeIds: Object.keys(serviceBlueprint.layout?.nodes ?? {}),
        queuesByStage: Object.fromEntries(
          serviceBlueprint.states.map(state => [state.stateKey, state.queueKey ?? ''])
        ),
      });
    });
  });
}

async function dragBy(page: Page, selector: string, dx: number, dy: number) {
  const box = await page.locator(`prism-service-blueprint-graph ${selector}`).boundingBox();
  if (!box) throw new Error(`no bounding box for ${selector}`);
  const startX = box.x + box.width / 2;
  const startY = box.y + box.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + dx, startY + dy, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(200);
}

test.describe('ServiceBlueprint canvas — manual arrangement', () => {
  test('dragging a stage moves it and persists the position in the layout block', async ({ page }) => {
    await gotoGraphStory(page, GRAPH_STORY);
    await recordServiceBlueprintUpdates(page);

    const stage = page.locator('prism-service-blueprint-graph [data-prism-stage="enter-details"]');
    const before = await stage.boundingBox();
    await dragBy(page, '[data-prism-stage="enter-details"]', 0, 120);
    const after = await stage.boundingBox();

    expect(after!.y - before!.y, 'the stage must follow the drag').toBeGreaterThan(80);

    const updates = await page.evaluate(() => window.__layoutUpdates!);
    expect(updates, 'one drag gesture commits exactly one service blueprint update').toHaveLength(1);
    expect(updates[0].layoutNodeIds).toContain('stage:enter-details');
  });

  test('dropping a stage into another lane reassigns its queue in the same commit', async ({ page }) => {
    await gotoGraphStory(page, GRAPH_STORY);
    await recordServiceBlueprintUpdates(page);

    const lanes = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const root = (graphElement as HTMLElement).shadowRoot!;
      return Array.from(root.querySelectorAll<HTMLElement>('[data-prism-role-queue]')).map(lane => {
        const rect = lane.getBoundingClientRect();
        return { key: lane.getAttribute('data-prism-role-queue') ?? '', centerX: rect.left + rect.width / 2 };
      });
    });
    expect(lanes.length).toBeGreaterThanOrEqual(2);

    const stage = page.locator('prism-service-blueprint-graph [data-prism-stage="enter-details"]');
    await expect(stage).toHaveAttribute('data-prism-queue', lanes[0].key);

    const box = await stage.boundingBox();
    await dragBy(page, '[data-prism-stage="enter-details"]', lanes[1].centerX - (box!.x + box!.width / 2), 40);

    await expect(stage).toHaveAttribute('data-prism-queue', lanes[1].key);

    const updates = await page.evaluate(() => window.__layoutUpdates!);
    expect(updates, 'position + queue reassignment land as a single undoable commit').toHaveLength(1);
    expect(updates[0].queuesByStage['enter-details']).toBe(lanes[1].key);
  });

  test('Tidy layout writes explicit positions for every node in one commit', async ({ page }) => {
    await gotoGraphStory(page, GRAPH_STORY);

    const nodeCount = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const root = (graphElement as HTMLElement).shadowRoot!;
      return root.querySelectorAll('.react-flow__node').length;
    });

    await dragBy(page, '[data-prism-stage="enter-details"]', 0, 140);
    await recordServiceBlueprintUpdates(page);

    await page.locator('prism-service-blueprint-graph [data-prism-auto-arrange]').click();
    await page.waitForTimeout(500);

    const updates = await page.evaluate(() => window.__layoutUpdates!);
    expect(updates, 'Tidy layout is one commit').toHaveLength(1);
    expect(updates[0].layoutNodeIds.length, 'every node gets an explicit position').toBe(nodeCount);
  });

  test('undo restores a dragged stage to its previous position', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(EDITOR_STORY);
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const stageKey = await page.locator('prism-service-blueprint-graph [data-prism-stage]').first()
      .getAttribute('data-prism-stage');
    const stage = page.locator(`prism-service-blueprint-graph [data-prism-stage="${stageKey}"]`);

    const before = await stage.boundingBox();
    await dragBy(page, `[data-prism-stage="${stageKey}"]`, 0, 140);
    const moved = await stage.boundingBox();
    expect(moved!.y - before!.y).toBeGreaterThan(100);

    await page.keyboard.press('ControlOrMeta+z');
    await expect
      .poll(async () => Math.abs((await stage.boundingBox())!.y - before!.y), {
        message: 'undo must snap the stage back to its pre-drag position',
      })
      .toBeLessThan(2);
  });

  test('read-only viewer does not allow dragging nodes (the gesture pans instead)', async ({ page }) => {
    await gotoGraphStory(page, '/iframe.html?id=service-blueprint-editor-service-blueprint-graph--graph-read-only&viewMode=story');
    await recordServiceBlueprintUpdates(page);

    // Dragging a node in a read-only canvas falls through to a viewport pan,
    // so screen coordinates move — but the node must not move relative to its
    // lane, and the service blueprint document must not change.
    const stage = page.locator('prism-service-blueprint-graph [data-prism-stage]').first();
    const lane = page.locator('prism-service-blueprint-graph [data-prism-role-queue]').first();
    const stageBefore = await stage.boundingBox();
    const laneBefore = await lane.boundingBox();

    const selector = `[data-prism-stage="${await stage.getAttribute('data-prism-stage')}"]`;
    await dragBy(page, selector, 0, 120);

    const stageAfter = await stage.boundingBox();
    const laneAfter = await lane.boundingBox();
    const relativeBefore = stageBefore!.y - laneBefore!.y;
    const relativeAfter = stageAfter!.y - laneAfter!.y;

    expect(Math.abs(relativeAfter - relativeBefore), 'read-only canvases must not move nodes within their lane').toBeLessThan(2);
    expect(await page.evaluate(() => window.__layoutUpdates!.length), 'no service blueprint mutation in read-only mode').toBe(0);
  });
});
