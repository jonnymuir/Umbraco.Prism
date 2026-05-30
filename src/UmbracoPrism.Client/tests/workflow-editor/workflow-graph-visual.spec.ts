import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('Workflow graph behavioural rendering', () => {
  test('graph workspace renders lane columns with stages and routes', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

    const storyEl = page.locator('prism-workflow-graph');
    await expect(storyEl).toBeVisible({ timeout: 10_000 });
    await page.waitForLoadState('networkidle');
    await storyEl.evaluate(async element => {
      await (element as { updateComplete?: Promise<unknown> }).updateComplete;
    });

    const lanes = storyEl.locator('[data-prism-role-lane]');
    await expect(lanes.first()).toBeVisible();
    expect(await lanes.count()).toBeGreaterThan(0);

    const stages = storyEl.locator('[data-prism-stage]');
    await expect(stages.first()).toBeVisible();
    expect(await stages.count()).toBeGreaterThan(0);

    const transitions = storyEl.locator('[data-prism-transition]');
    expect(await transitions.count()).toBeGreaterThan(0);

    await expect(storyEl.locator('.lane-header').first()).toBeVisible();
    await expect(storyEl.locator('.graph-canvas')).toBeVisible();
  });
});
