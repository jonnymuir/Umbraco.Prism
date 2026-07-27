import { expect, test } from '@playwright/test';
import {
  CANONICAL_SCENARIOS,
  gotoCanonicalScenario,
  measureGraph,
  VISUAL_VIEWPORT,
} from './support/canvas-helpers';

/**
 * Concern 2 (typography) from
 * `docs/testing/service-blueprint-editor-visual-tests.md`: stage and gateway
 * display-name labels must not be clipped within the documented title
 * length (≤ 40 chars). This catches "text crashing" — labels rendering
 * with `text-overflow: ellipsis` or visibly overflowing their card.
 *
 * Labels longer than 40 chars are out of contract (the create-stage and
 * create-gateway dialogs already cap visible names well below this).
 */
const DOCUMENTED_TITLE_LIMIT = 40;

test.use({ viewport: { ...VISUAL_VIEWPORT } });

test.describe('ServiceBlueprint canvas — text fits without crashing', () => {
  for (const scenario of CANONICAL_SCENARIOS) {
    test(`stage and gateway labels are not clipped — ${scenario.id}`, async ({ page }) => {
      await gotoCanonicalScenario(page, scenario);
      const geometry = await measureGraph(page);

      const candidates = geometry.nodes.filter(
        (node) => node.label.length > 0 && node.label.length <= DOCUMENTED_TITLE_LIMIT,
      );
      expect(candidates.length, 'at least one labelled node should be measurable').toBeGreaterThan(0);

      for (const node of candidates) {
        // scrollWidth > clientWidth means the label is wider than its
        // container, so text-overflow / ellipsis would clip it.
        expect(
          node.scrollWidth,
          `${node.kind} "${node.label}" label is clipped: scrollWidth=${node.scrollWidth} > clientWidth=${node.clientWidth}`,
        ).toBeLessThanOrEqual(node.clientWidth + 1);
      }
    });
  }
});
