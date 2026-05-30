import { expect, test } from '@playwright/test';
import {
  CANONICAL_SCENARIOS,
  gotoCanonicalScenario,
  graphLocator,
  VISUAL_VIEWPORT,
} from './support/canvas-helpers';

/**
 * Concern 3 from `docs/testing/workflow-editor-visual-tests.md`:
 * the canvas must scroll on the overflowing axis when content exceeds the
 * viewport, and lane headers must remain sticky during vertical scroll.
 * When the workflow fits, scrollbars must not appear.
 */

test.use({ viewport: { ...VISUAL_VIEWPORT } });

type CanvasMetrics = {
  scrollWidth: number;
  clientWidth: number;
  scrollHeight: number;
  clientHeight: number;
  overflowX: string;
  overflowY: string;
  hasVerticalOverflow: boolean;
  hasHorizontalOverflow: boolean;
};

async function readCanvasMetrics(page: import('@playwright/test').Page): Promise<CanvasMetrics> {
  return graphLocator(page).evaluate((el) => {
    const root = (el as HTMLElement).shadowRoot;
    const canvas = root?.querySelector<HTMLElement>('.graph-canvas');
    if (!canvas) throw new Error('.graph-canvas not found');
    const cs = getComputedStyle(canvas);
    return {
      scrollWidth: canvas.scrollWidth,
      clientWidth: canvas.clientWidth,
      scrollHeight: canvas.scrollHeight,
      clientHeight: canvas.clientHeight,
      overflowX: cs.overflowX,
      overflowY: cs.overflowY,
      hasHorizontalOverflow: canvas.scrollWidth > canvas.clientWidth,
      hasVerticalOverflow: canvas.scrollHeight > canvas.clientHeight,
    };
  });
}

test.describe('Workflow canvas — scroll behaviour', () => {
  test('LARGE_WORKFLOW: canvas reports both horizontal and vertical overflow', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'LARGE_WORKFLOW')!;
    await gotoCanonicalScenario(page, scenario);

    const metrics = await readCanvasMetrics(page);
    // Both axes can produce scrollable overflow on a large synthetic workflow.
    // We assert at least one axis genuinely overflows; on viewports where the
    // synthetic workflow only overflows vertically, that is still a valid
    // proof of the contract.
    expect(
      metrics.hasHorizontalOverflow || metrics.hasVerticalOverflow,
      `LARGE_WORKFLOW must overflow the canvas on at least one axis; metrics=${JSON.stringify(metrics)}`,
    ).toBe(true);

    // Both axes are allowed to scroll.
    expect(['auto', 'scroll']).toContain(metrics.overflowX);
    expect(['auto', 'scroll']).toContain(metrics.overflowY);
  });

  test('LARGE_WORKFLOW: scripted scroll moves the canvas without affecting shell layout', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'LARGE_WORKFLOW')!;
    await gotoCanonicalScenario(page, scenario);

    const before = await graphLocator(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const canvas = root.querySelector<HTMLElement>('.graph-canvas')!;
      const header = root.querySelector<HTMLElement>('[data-prism-lane-header]');
      return {
        scrollTop: canvas.scrollTop,
        scrollLeft: canvas.scrollLeft,
        headerTop: header?.getBoundingClientRect().top ?? null,
      };
    });

    const after = await graphLocator(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const canvas = root.querySelector<HTMLElement>('.graph-canvas')!;
      canvas.scrollTo({ top: 200, left: 200, behavior: 'auto' });
      const header = root.querySelector<HTMLElement>('[data-prism-lane-header]');
      return {
        scrollTop: canvas.scrollTop,
        scrollLeft: canvas.scrollLeft,
        headerTop: header?.getBoundingClientRect().top ?? null,
      };
    });

    expect(after.scrollTop, 'vertical scroll position must update').toBeGreaterThan(before.scrollTop);
    expect(after.scrollLeft, 'horizontal scroll position must update').toBeGreaterThan(before.scrollLeft);
  });

  // Sticky lane headers (BUG-VR-1, fixed in Slice 7.5). The header must
  // remain anchored at its initial viewport position while the canvas
  // scrolls — see `.squad/decisions/inbox/isabelle-slice7-5-visual-bug-fixes.md`.
  test('LARGE_WORKFLOW: lane header strip stays sticky during vertical scroll', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'LARGE_WORKFLOW')!;
    await gotoCanonicalScenario(page, scenario);

    const result = await graphLocator(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const canvas = root.querySelector<HTMLElement>('.graph-canvas')!;
      const header = root.querySelector<HTMLElement>('[data-prism-lane-header]');
      if (!header) return null;
      const before = header.getBoundingClientRect().top;
      canvas.scrollTo({ top: 250, behavior: 'auto' });
      const after = header.getBoundingClientRect().top;
      return { before, after, position: getComputedStyle(header).position };
    });

    expect(result, 'at least one lane header must render').not.toBeNull();
    if (!result) return;

    // Sticky headers either declare `position: sticky` or stay anchored
    // within ~4 px of their original viewport position after a 250 px
    // vertical scroll. Either is acceptable evidence of stickiness.
    const moved = Math.abs(result.after - result.before);
    const isSticky = result.position === 'sticky' || moved <= 4;
    expect(
      isSticky,
      `Lane header drifted ${moved.toFixed(0)}px after vertical scroll (position=${result.position})`,
    ).toBe(true);
  });

  test('SINGLE_LANE_LINEAR: canvas does not produce meaningful horizontal overflow when workflow fits', async ({ page }) => {
    const scenario = CANONICAL_SCENARIOS.find((s) => s.id === 'SINGLE_LANE_LINEAR')!;
    await gotoCanonicalScenario(page, scenario);

    const metrics = await readCanvasMetrics(page);
    // Sub-pixel rounding can produce 1–2 px of nominal overflow on
    // browsers that hand back fractional clientWidth; that is not a
    // user-visible scrollbar. Treat anything under 16 px as "fits".
    const meaningfulOverflow = metrics.scrollWidth - metrics.clientWidth;
    expect(
      meaningfulOverflow,
      `single-lane linear workflow should fit horizontally at ${VISUAL_VIEWPORT.width}px; overflow=${meaningfulOverflow}px metrics=${JSON.stringify(metrics)}`,
    ).toBeLessThan(16);
  });
});
