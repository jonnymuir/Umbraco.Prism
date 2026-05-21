import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';

test.use({
  launchOptions: {
    args: ['--font-render-hinting=none'],
  },
});

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function loadWorkspaceStory(page: Page) {
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto(storyUrl('workflow-editor-workflow-graph--workspace-canvas'));

  const storyEl = page.locator('prism-workflow-graph');
  await expect(storyEl).toBeVisible({ timeout: 10_000 });
  await page.waitForLoadState('networkidle');
  await page.evaluate(async () => {
    await document.fonts.ready;
  });
  await storyEl.evaluate(async element => {
    (element as HTMLElement).style.width = '1280px';
    (element as HTMLElement).style.height = '560px';
    (element as HTMLElement).style.setProperty('--uui-font-family', 'Arial, Helvetica, sans-serif');
    await (element as { updateComplete?: Promise<unknown> }).updateComplete;
  });

  return storyEl;
}

test.describe('Workflow graph Storybook visual regression', () => {
  test('graph workspace matches the baseline canvas', async ({ page }) => {
    const storyEl = await loadWorkspaceStory(page);

    await expect(storyEl).toHaveScreenshot('workflow-graph-workspace-canvas.png', {
      animations: 'disabled',
      caret: 'hide',
      scale: 'css',
      maxDiffPixels: 80
    });
  });

  test('list mode matches the baseline workspace layout', async ({ page }) => {
    const storyEl = await loadWorkspaceStory(page);

    await page.getByRole('button', { name: 'List view' }).click();
    await expect(page.getByRole('region', { name: /workflow stages/i })).toBeVisible({ timeout: 5_000 });
    await storyEl.evaluate(async element => {
      await (element as { updateComplete?: Promise<unknown> }).updateComplete;
    });

    await expect(storyEl).toHaveScreenshot('workflow-graph-workspace-list-mode.png', {
      animations: 'disabled',
      caret: 'hide',
      scale: 'css',
      maxDiffPixels: 80
    });
  });
});
