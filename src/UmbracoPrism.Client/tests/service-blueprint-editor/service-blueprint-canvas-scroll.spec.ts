import { expect, test } from '@playwright/test';
import {
  CANONICAL_SCENARIOS,
  gotoCanonicalScenario,
  graphLocator,
  VISUAL_VIEWPORT,
} from './support/canvas-helpers';

/**
 * Concern 3 from `docs/testing/service-blueprint-editor-visual-tests.md`, restated for
 * the React Flow canvas: content larger than the canvas is reached by
 * panning and fitView instead of native scrollbars. Lane headers live in the
 * viewport (ViewportPortal), so they pan with the content — the non-sticky
 * contract from BUG-VR-1 carries over.
 */

test.use({ viewport: { ...VISUAL_VIEWPORT } });

async function nodeScreenTops(page: import('@playwright/test').Page): Promise<number[]> {
  return graphLocator(page).evaluate((el) => {
    const root = (el as HTMLElement).shadowRoot!;
    return Array.from(root.querySelectorAll<HTMLElement>('[data-prism-stage-card]'))
      .map(shell => shell.getBoundingClientRect().top);
  });
}

async function canvasRect(page: import('@playwright/test').Page) {
  return graphLocator(page).evaluate((el) => {
    const root = (el as HTMLElement).shadowRoot!;
    const rect = root.querySelector<HTMLElement>('.graph-canvas')!.getBoundingClientRect();
    return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom };
  });
}

async function panePan(page: import('@playwright/test').Page, dx: number, dy: number) {
  const rect = await canvasRect(page);
  const startX = (rect.left + rect.right) / 2;
  const startY = (rect.top + rect.bottom) / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + dx, startY + dy, { steps: 6 });
  await page.mouse.up();
}

test.describe('ServiceBlueprint canvas — pan and fit behaviour', () => {
  test('LARGE_SERVICE_BLUEPRINT: content extends beyond the visible canvas at default zoom', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'LARGE_SERVICE_BLUEPRINT')!;
    await gotoCanonicalScenario(page, scenario);

    const rect = await canvasRect(page);
    const boxes = await graphLocator(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      return Array.from(root.querySelectorAll<HTMLElement>('[data-prism-stage-card]'))
        .map(shell => {
          const box = shell.getBoundingClientRect();
          return { top: box.top, bottom: box.bottom, left: box.left, right: box.right };
        });
    });
    expect(boxes.length).toBeGreaterThan(0);
    expect(
      boxes.some(box => box.bottom > rect.bottom || box.right > rect.right
        || box.top < rect.top || box.left < rect.left),
      'a large service blueprint must have stages outside the visible canvas at default zoom',
    ).toBe(true);
  });

  test('LARGE_SERVICE_BLUEPRINT: dragging the pane pans the content', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'LARGE_SERVICE_BLUEPRINT')!;
    await gotoCanonicalScenario(page, scenario);

    const before = await nodeScreenTops(page);
    await panePan(page, 0, -250);
    const after = await nodeScreenTops(page);

    const moved = before[0] - after[0];
    expect(
      moved,
      `stages should move up by roughly the pan distance; actual=${moved.toFixed(0)}px`,
    ).toBeGreaterThan(150);
  });

  test('LARGE_SERVICE_BLUEPRINT: lane header pans with the canvas (not sticky)', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'LARGE_SERVICE_BLUEPRINT')!;
    await gotoCanonicalScenario(page, scenario);

    const headerTop = () => graphLocator(page).evaluate((el) => {
      const header = (el as HTMLElement).shadowRoot!.querySelector<HTMLElement>('[data-prism-queue-header]');
      if (!header) return null;
      return { top: header.getBoundingClientRect().top, position: getComputedStyle(header).position };
    });

    const before = await headerTop();
    expect(before, 'at least one lane header must render').not.toBeNull();
    await panePan(page, 0, -250);
    const after = await headerTop();
    if (!before || !after) return;

    expect(after.position, 'lane-header must not have position:sticky').not.toBe('sticky');
    const moved = before.top - after.top;
    expect(
      moved,
      `Lane header should have panned up by ≥150px after a 250px pane drag; actual=${moved.toFixed(0)}px`,
    ).toBeGreaterThan(150);
  });

  test('LARGE_SERVICE_BLUEPRINT: fit-to-screen brings the whole service blueprint into view', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'LARGE_SERVICE_BLUEPRINT')!;
    await gotoCanonicalScenario(page, scenario);

    await graphLocator(page).locator('[data-prism-fit-screen]').click();
    // fitView animates over 200ms.
    await page.waitForTimeout(500);

    const rect = await canvasRect(page);
    const tops = await nodeScreenTops(page);
    expect(tops.length).toBeGreaterThan(0);
    expect(
      tops.every(top => top >= rect.top - 1 && top <= rect.bottom + 1),
      'after fit-to-screen every stage top must be inside the canvas',
    ).toBe(true);
  });

  // Carried over as fixme from the scroll-era suite: the canonical scenario
  // renders wider than the 1440px viewport, so "fits" has never held here.
  test.fixme('SINGLE_LANE_LINEAR: a fitting service blueprint renders fully inside the canvas at default zoom', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'SINGLE_LANE_LINEAR')!;
    await gotoCanonicalScenario(page, scenario);

    const rect = await canvasRect(page);
    const lanesRight = await graphLocator(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      return Array.from(root.querySelectorAll<HTMLElement>('[data-prism-queue-container]'))
        .map(lane => lane.getBoundingClientRect().right);
    });
    expect(lanesRight.length).toBeGreaterThan(0);
    expect(
      lanesRight.every(right => right <= rect.right + 16),
      'a fitting service blueprint must not extend horizontally past the canvas',
    ).toBe(true);
  });
});
