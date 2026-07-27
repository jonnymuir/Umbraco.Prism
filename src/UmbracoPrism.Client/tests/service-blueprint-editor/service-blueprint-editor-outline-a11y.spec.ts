import { expect, test } from '@playwright/test';
// TODO Slice E: re-cert after gateway-pill rendering + simulation reshape. See .squad/decisions/inbox/copilot-slice-d-close-out.md.

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

// Slice 3d a11y polish — these two specs lock the regressions Tangy flagged in
// the editor-reset A11y review (SHOULD-FIX #1 + #2, IMPROVE #4): the outline
// must speak gateway display names not raw keys, and picking a join gateway
// for a stage's outgoing route must announce the change via the polite live
// region.
test.describe('Outline + gateway-first inspector accessibility', () => {
  test.fixme("author can pick a join gateway from a stage's outgoing route and the change is announced", async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--gateway-representation'));

    const editor = page.locator('prism-service-blueprint-editor');
    await expect(editor).toBeVisible({ timeout: 10_000 });

    // Open the join gateway from the canvas (only split gateways live in the
    // outline). Click and press 'e' to open the inspector — same affordance
    // the existing gateway spec uses.
    const joinGateway = page.locator('[data-prism-gateway="decision-join"]');
    await joinGateway.click();
    await joinGateway.press('e');

    const inspector = editor.locator('prism-step-inspector');
    await expect(inspector.locator('[data-prism-gateway-detail="decision-join"]')).toBeVisible();

    // The Arrive through selects on the join's incoming routes are how an
    // author picks/changes the join. Two routes feed decision-join in this
    // fixture (applicant + reviewer branches). Clear the applicant-branch
    // route's join — this drives _updateRouteToGateway end-to-end.
    const routeBlocks = inspector.locator('[data-prism-route-target="decision-confirmed"]');
    await expect(routeBlocks).toHaveCount(2);
    const firstBlock = routeBlocks.first();
    const joinSelect = firstBlock.locator('[data-prism-route-to-gateway]');

    // Sanity: select currently shows the join gateway by display name and the
    // raw key is the option value.
    const selectedLabel = await joinSelect.locator('option:checked').textContent();
    expect(selectedLabel?.trim()).toBe('Decision join');

    await joinSelect.selectOption('');

    // The polite live region announced the change (Tangy IMPROVE #4: change to
    // a join gateway on a stage's outgoing route must announce).
    const announcement = await inspector.evaluate(node => {
      const announcer = (node as HTMLElement).shadowRoot?.getElementById('inspector-announcer');
      return announcer?.textContent?.trim() ?? '';
    });
    expect(announcement).toBe('Route now arrives directly at the target stage.');

    // The service blueprint model lost the join target for that route.
    const remainingJoinCount = await inspector.evaluate(node => {
      const el = node as unknown as {
        serviceBlueprint: { gateways?: Array<{ routes?: Array<{ target?: string }> }> } | null;
      };
      let count = 0;
      for (const gw of (el.serviceBlueprint?.gateways ?? [])) {
        for (const route of (gw.routes ?? [])) {
          if (route.target === 'decision-join') count++;
        }
      }
      return count;
    });
    expect(remainingJoinCount).toBe(1);
  });

  test.fixme('screen reader user reading a transition in the outline hears the gateway name, not the gateway key', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--gateway-representation'));

    const editor = page.locator('prism-service-blueprint-editor');
    await expect(editor).toBeVisible({ timeout: 10_000 });

    const outline = editor.locator('prism-service-blueprint-outline');
    await expect(outline).toBeVisible();

    // Grab the visible/accessible text rendered for the Draft stage's
    // outgoing transition rows (these were the rows that leaked raw keys).
    const transitionText = await outline.evaluate(node => {
      const root = (node as HTMLElement).shadowRoot;
      const stageItem = root?.querySelector('[data-prism-outline-stage="draft"]')?.closest('.outline-stage-item');
      const targets = stageItem?.querySelectorAll('.outline-transition-target') ?? [];
      return Array.from(targets).map(el => el.textContent?.replace(/\s+/g, ' ').trim() ?? '').join(' | ');
    });

    // The Draft stage fans through the "Review split" gateway — display name,
    // not the raw `review-split` key.
    expect(transitionText).toContain('Review split');
    expect(transitionText).not.toMatch(/\breview-split\b/);
  });
});
