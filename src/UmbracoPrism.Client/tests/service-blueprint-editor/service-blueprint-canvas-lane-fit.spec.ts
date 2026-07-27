import { expect, test } from '@playwright/test';
import {
  CANONICAL_SCENARIOS,
  gotoCanonicalScenario,
  measureGraph,
  VISUAL_VIEWPORT,
} from './support/canvas-helpers';

/**
 * Concern 1 from `docs/testing/service-blueprint-editor-visual-tests.md`:
 * every stage card and gateway node must render fully inside its declared
 * lane's bounding box.
 *
 * A 2 px tolerance absorbs the lane's own border/padding rounding without
 * letting a node visibly cross into a sibling lane.
 */
const LANE_TOLERANCE_PX = 2;

test.use({ viewport: { ...VISUAL_VIEWPORT } });

test.describe('ServiceBlueprint canvas — lane fit invariant', () => {
  for (const scenario of CANONICAL_SCENARIOS) {
    test(`every node stays inside its lane column — ${scenario.id}`, async ({ page }) => {
      await gotoCanonicalScenario(page, scenario);
      const geometry = await measureGraph(page);

      expect(geometry.lanes.length, 'at least one lane must render').toBeGreaterThan(0);
      expect(geometry.nodes.length, 'at least one node must render').toBeGreaterThan(0);

      for (const node of geometry.nodes) {
        // Prefer the explicit data-prism-queue attribute when present
        // (gateways carry it because they are not DOM children of the queue
        // column they belong to — see Slice 5 history note).
        const expectedLaneKey = node.laneAttr ?? node.laneByCentre;
        expect(expectedLaneKey, `node ${node.key} (${node.kind}) must be attributable to a lane`).not.toBeNull();

        const lane = geometry.lanes.find((l) => l.key === expectedLaneKey);
        expect(lane, `lane ${expectedLaneKey} (owner of ${node.key}) must exist`).toBeDefined();
        if (!lane) continue;

        expect(
          node.left,
          `${node.kind} "${node.label || node.key}" left edge (${node.left}) must stay inside lane "${lane.key}" (${lane.left}..${lane.right})`,
        ).toBeGreaterThanOrEqual(lane.left - LANE_TOLERANCE_PX);
        expect(
          node.right,
          `${node.kind} "${node.label || node.key}" right edge (${node.right}) must stay inside lane "${lane.key}" (${lane.left}..${lane.right})`,
        ).toBeLessThanOrEqual(lane.right + LANE_TOLERANCE_PX);
        expect(
          node.top,
          `${node.kind} "${node.label || node.key}" top edge must stay below lane top`,
        ).toBeGreaterThanOrEqual(lane.top - LANE_TOLERANCE_PX);
      }
    });
  }
});
