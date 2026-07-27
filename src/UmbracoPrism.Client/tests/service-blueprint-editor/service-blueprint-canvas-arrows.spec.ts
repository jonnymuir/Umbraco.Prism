import { expect, test } from '@playwright/test';
import {
  CANONICAL_SCENARIOS,
  gotoCanonicalScenario,
  measureGraph,
  VISUAL_VIEWPORT,
} from './support/canvas-helpers';

/**
 * Concern 4 from `docs/testing/service-blueprint-editor-visual-tests.md`: every
 * route's SVG endpoint must land on the node it claims to attach to (±4
 * px), so authors can read the diagram as "from this stage, through this
 * gateway, to that stage".
 *
 * A handful of canonical layouts also have committed screenshot baselines
 * so a human can eyeball arrow geometry on PR review. Generous tolerance
 * (maxDiffPixelRatio 0.02) absorbs sub-pixel font rendering differences
 * between local and CI runs — see strategy doc.
 */
const ENDPOINT_TOLERANCE_PX = 4;

test.use({ viewport: { ...VISUAL_VIEWPORT } });

test.describe('ServiceBlueprint canvas — arrow endpoints land on nodes', () => {
  for (const scenario of CANONICAL_SCENARIOS) {
    test(`route endpoints sit within ±${ENDPOINT_TOLERANCE_PX}px of their node — ${scenario.id}`, async ({ page }) => {
      await gotoCanonicalScenario(page, scenario);
      const geometry = await measureGraph(page);

      if (geometry.routes.length === 0) {
        // Single-stage scenarios may render with no routes; that is fine.
        test.skip(true, `No routes rendered for ${scenario.id}`);
        return;
      }

      const nodeByKey = new Map(geometry.nodes.map((n) => [n.key, n]));

      for (const route of geometry.routes) {
        const fromNode = nodeByKey.get(route.from);
        const toNode = nodeByKey.get(route.to);
        expect(fromNode, `route ${route.key}: from-node "${route.from}" must exist`).toBeDefined();
        expect(toNode, `route ${route.key}: to-node "${route.to}" must exist`).toBeDefined();
        if (!fromNode || !toNode) continue;

        // The endpoint may attach to any edge of the node — we require it
        // to sit within the bounding box (plus tolerance) rather than at
        // any one specific connector point, since the slot-matrix picks
        // sides dynamically.
        expect(
          route.start.x,
          `route ${route.key} start.x=${route.start.x} must be within ${fromNode.kind} "${fromNode.key}" horizontal bounds [${fromNode.left}, ${fromNode.right}]`,
        ).toBeGreaterThanOrEqual(fromNode.left - ENDPOINT_TOLERANCE_PX);
        expect(route.start.x).toBeLessThanOrEqual(fromNode.right + ENDPOINT_TOLERANCE_PX);
        expect(
          route.start.y,
          `route ${route.key} start.y=${route.start.y} must be within ${fromNode.kind} "${fromNode.key}" vertical bounds [${fromNode.top}, ${fromNode.bottom}]`,
        ).toBeGreaterThanOrEqual(fromNode.top - ENDPOINT_TOLERANCE_PX);
        expect(route.start.y).toBeLessThanOrEqual(fromNode.bottom + ENDPOINT_TOLERANCE_PX);

        expect(
          route.end.x,
          `route ${route.key} end.x=${route.end.x} must be within ${toNode.kind} "${toNode.key}" horizontal bounds [${toNode.left}, ${toNode.right}]`,
        ).toBeGreaterThanOrEqual(toNode.left - ENDPOINT_TOLERANCE_PX);
        expect(route.end.x).toBeLessThanOrEqual(toNode.right + ENDPOINT_TOLERANCE_PX);
        expect(
          route.end.y,
          `route ${route.key} end.y=${route.end.y} must be within ${toNode.kind} "${toNode.key}" vertical bounds [${toNode.top}, ${toNode.bottom}]`,
        ).toBeGreaterThanOrEqual(toNode.top - ENDPOINT_TOLERANCE_PX);
        expect(route.end.y).toBeLessThanOrEqual(toNode.bottom + ENDPOINT_TOLERANCE_PX);
      }
    });
  }
});

test.describe('ServiceBlueprint canvas — canonical screenshot baselines', () => {
  // Only snapshot scenarios that fit the pinned viewport. LARGE_SERVICE_BLUEPRINT is
  // covered by scroll DOM assertions and would dominate the screenshot
  // budget with a low-signal long thin image.
  const screenshotScenarios = CANONICAL_SCENARIOS.filter((s) => !s.oversize);

  for (const scenario of screenshotScenarios) {
    test(`canvas snapshot — ${scenario.id}`, async ({ page }) => {
      await gotoCanonicalScenario(page, scenario);
      // Wait one extra frame after measurement so any reflow from the
      // measurement pass has settled.
      await page.waitForLoadState('networkidle');
      const graph = page.locator('prism-service-blueprint-graph');
      await expect(graph).toHaveScreenshot(`${scenario.id}.png`, {
        animations: 'disabled',
        maxDiffPixelRatio: 0.02,
      });
    });
  }
});
