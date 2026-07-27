import { expect, test } from '@playwright/test';
import {
  CANONICAL_SCENARIOS,
  gotoCanonicalScenario,
  measureGraph,
  rectanglesOverlap,
  VISUAL_VIEWPORT,
} from './support/canvas-helpers';

/**
 * Concern 2 (geometry) from
 * `docs/testing/service-blueprint-editor-visual-tests.md`: two nodes attributed to
 * the same lane must never overlap. Cross-lane overlap is impossible while
 * the lane-fit invariant holds, so this spec only enforces the same-lane
 * rule — keeping the failure message specific.
 */

test.use({ viewport: { ...VISUAL_VIEWPORT } });

test.describe('ServiceBlueprint canvas — no-overlap invariant', () => {
  for (const scenario of CANONICAL_SCENARIOS) {
    test(`no two nodes in the same lane overlap — ${scenario.id}`, async ({ page }) => {
      await gotoCanonicalScenario(page, scenario);
      const geometry = await measureGraph(page);

      // Group nodes by their owning lane (attribute wins over centre).
      const groups = new Map<string, typeof geometry.nodes>();
      for (const node of geometry.nodes) {
        const lane = node.laneAttr ?? node.laneByCentre ?? '<unassigned>';
        const bucket = groups.get(lane) ?? [];
        bucket.push(node);
        groups.set(lane, bucket);
      }

      for (const [lane, nodes] of groups) {
        for (let i = 0; i < nodes.length; i++) {
          for (let j = i + 1; j < nodes.length; j++) {
            const a = nodes[i];
            const b = nodes[j];
            const overlaps = rectanglesOverlap(a, b);
            expect(
              overlaps,
              `Lane "${lane}": "${a.label || a.key}" (${a.kind}) overlaps "${b.label || b.key}" (${b.kind}). ` +
                `a=[${a.left.toFixed(0)},${a.top.toFixed(0)},${a.right.toFixed(0)},${a.bottom.toFixed(0)}] ` +
                `b=[${b.left.toFixed(0)},${b.top.toFixed(0)},${b.right.toFixed(0)},${b.bottom.toFixed(0)}]`,
            ).toBe(false);
          }
        }
      }
    });
  }
});
