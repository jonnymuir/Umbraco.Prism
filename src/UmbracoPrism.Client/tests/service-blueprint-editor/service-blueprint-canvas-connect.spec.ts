import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';

/**
 * Drag-to-connect and multi-select behaviours on the React Flow canvas:
 * connection drags create routes that respect the gateway-routing invariant,
 * shift-marquee selections support group copy/paste with key remapping, and
 * read-only canvases expose no connection affordances.
 */

const GRAPH_STORY = '/iframe.html?id=service-blueprint-editor-service-blueprint-graph--payment-demo-graph&viewMode=story';
const EDITOR_STORY = '/iframe.html?id=service-blueprint-editor-editor-host--planning-service-blueprint&viewMode=story';

async function gotoStory(page: Page, url: string) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(url);
  await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });
}

async function sourceHandleCentre(page: Page, stageKey: string) {
  return page.locator('prism-service-blueprint-graph').evaluate((graphElement, key) => {
    const root = (graphElement as HTMLElement).shadowRoot!;
    const handle = root.querySelector(`[data-prism-stage-card="${key}"] .react-flow__handle.source`);
    if (!handle) throw new Error(`no source handle for ${key}`);
    const rect = handle.getBoundingClientRect();
    return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
  }, stageKey);
}

test.describe('ServiceBlueprint canvas — drag-to-connect', () => {
  test('connecting a stage to a gateway adds a route and opens the inspector on it', async ({ page }) => {
    await gotoStory(page, GRAPH_STORY);
    // The await-payment-confirmation drop target sits below the default fold
    // once row spacing gives routes room to breathe; give the canvas room.
    await page.setViewportSize({ width: 1440, height: 1100 });

    // Chips are per authored route; route paths are per node pair (and this
    // connection reuses an existing pair), so assert on chips.
    const chipsBefore = await page.locator('prism-service-blueprint-graph [data-prism-transition]').count();
    await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      (window as unknown as { __inspector: unknown[] }).__inspector = [];
      graphElement.addEventListener('inspector-requested', event => {
        (window as unknown as { __inspector: unknown[] }).__inspector.push(
          (event as CustomEvent).detail
        );
      });
    });

    const handle = await sourceHandleCentre(page, 'confirm-payment-received');
    const target = await page.locator('prism-service-blueprint-graph [data-prism-gateway-node="await-payment-confirmation"]').boundingBox();
    await page.mouse.move(handle.x, handle.y);
    await page.mouse.down();
    await page.mouse.move(target!.x + target!.width / 2, target!.y + target!.height / 2, { steps: 10 });
    await page.mouse.up();

    await expect
      .poll(() => page.locator('prism-service-blueprint-graph [data-prism-transition]').count())
      .toBeGreaterThan(chipsBefore);
    const inspectorEvents = await page.evaluate(() =>
      (window as unknown as { __inspector: Array<{ kind: string }> }).__inspector);
    expect(inspectorEvents.some(detail => detail.kind === 'transition'),
      'the inspector must open on the newly created route').toBe(true);
  });

  test('connecting stage to stage routes through an auto-created Split gateway', async ({ page }) => {
    await gotoStory(page, GRAPH_STORY);
    // payment-complete sits below the story host's fixed-height canvas — no
    // viewport size brings it into view, only panning the canvas itself does.
    await page.locator('prism-service-blueprint-graph [data-prism-fit-screen]').click();
    // fitView animates over 200ms.
    await page.waitForTimeout(500);

    const handle = await sourceHandleCentre(page, 'confirm-payment-received');
    // payment-complete sits low in the canvas — drop on its visible top band.
    const target = await page.locator('prism-service-blueprint-graph [data-prism-stage-card="payment-complete"]').boundingBox();
    await page.mouse.move(handle.x, handle.y);
    await page.mouse.down();
    await page.mouse.move(target!.x + target!.width / 2, target!.y + 20, { steps: 10 });
    await page.mouse.up();

    // The gateway-routing invariant is preserved by construction: the new
    // stage→stage connection materialises as a Split gateway.
    await expect(
      page.locator('prism-service-blueprint-graph [data-prism-gateway="route-from-confirm-payment-received"]')
    ).toBeAttached({ timeout: 5_000 });
    await expect(
      page.locator('prism-service-blueprint-graph [data-prism-gateway="route-from-confirm-payment-received"]')
    ).toHaveAttribute('data-prism-gateway-kind', 'Split');
  });

  test('read-only canvases expose no connectable handles', async ({ page }) => {
    await gotoStory(page, '/iframe.html?id=service-blueprint-editor-service-blueprint-graph--graph-read-only&viewMode=story');

    const connectable = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const root = (graphElement as HTMLElement).shadowRoot!;
      return root.querySelectorAll('.react-flow__handle.connectable').length;
    });
    expect(connectable).toBe(0);
  });
});

test.describe('ServiceBlueprint canvas — marquee subgraph copy/paste', () => {
  test('shift-marquee selection copies and pastes a subgraph with remapped keys, undone in one step', async ({ page }) => {
    await gotoStory(page, EDITOR_STORY);

    const stagesBefore = await page.locator('prism-service-blueprint-graph [data-prism-stage]').count();
    const canvas = await page.locator('prism-service-blueprint-graph .graph-canvas').boundingBox();
    const first = await page.locator('prism-service-blueprint-graph [data-prism-stage-card]').first().boundingBox();
    const second = await page.locator('prism-service-blueprint-graph [data-prism-stage-card]').nth(1).boundingBox();

    const left = Math.max(canvas!.x + 8, Math.min(first!.x, second!.x) - 50);
    const top = Math.max(canvas!.y + 8, first!.y - 30);
    const right = Math.min(canvas!.x + canvas!.width - 8, Math.max(first!.x + first!.width, second!.x + second!.width) + 50);
    const bottom = Math.min(canvas!.y + canvas!.height - 8, second!.y + second!.height + 20);

    await page.keyboard.down('Shift');
    await page.mouse.move(left, top);
    await page.mouse.down();
    await page.mouse.move(right, bottom, { steps: 8 });
    await page.mouse.up();
    await page.keyboard.up('Shift');

    await expect
      .poll(() => page.locator('prism-service-blueprint-graph').evaluate(graphElement =>
        (graphElement as HTMLElement).shadowRoot!.querySelectorAll('.react-flow__node.selected').length))
      .toBeGreaterThanOrEqual(2);

    await page.keyboard.press('ControlOrMeta+c');
    await page.keyboard.press('ControlOrMeta+v');

    await expect
      .poll(() => page.locator('prism-service-blueprint-graph [data-prism-stage]').count())
      .toBeGreaterThan(stagesBefore);
    const copies = page.locator('prism-service-blueprint-graph [data-prism-stage*="-copy"]');
    await expect(copies.first()).toBeAttached();

    await page.keyboard.press('ControlOrMeta+z');
    await expect
      .poll(() => page.locator('prism-service-blueprint-graph [data-prism-stage]').count(),
        { message: 'one undo must remove the whole pasted subgraph' })
      .toBe(stagesBefore);
  });
});
