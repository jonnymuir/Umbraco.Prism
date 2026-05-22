import { readFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';

test.use({
  launchOptions: {
    args: [
      '--font-render-hinting=none',
      '--disable-font-subpixel-positioning',
      '--disable-lcd-text',
      '--force-color-profile=srgb',
    ],
  },
});

const VISUAL_TEST_FONT_FAMILY = 'Workflow Graph Visual Test';
const VISUAL_TEST_FONT_CSS = [
  { weight: 400, file: '../assets/fonts/inter-400.ttf' },
  { weight: 600, file: '../assets/fonts/inter-600.ttf' },
  { weight: 700, file: '../assets/fonts/inter-700.ttf' },
]
  .map(({ weight, file }) => {
    const encoded = readFileSync(new URL(file, import.meta.url)).toString('base64');
    return `
      @font-face {
        font-family: '${VISUAL_TEST_FONT_FAMILY}';
        font-style: normal;
        font-weight: ${weight};
        font-display: block;
        src: url(data:font/ttf;base64,${encoded}) format('truetype');
      }
    `;
  })
  .join('\n');

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function applyDeterministicFont(page: Page, storyEl: ReturnType<Page['locator']>) {
  await page.addStyleTag({ content: VISUAL_TEST_FONT_CSS });
  await page.evaluate(async () => {
    await document.fonts.ready;
  });
  await storyEl.evaluate(async (element, fontFamily) => {
    const root = (element as HTMLElement).shadowRoot;
    if (!root) {
      return;
    }

    let style = root.querySelector<HTMLStyleElement>('[data-prism-visual-font-lock]');
    if (!style) {
      style = document.createElement('style');
      style.setAttribute('data-prism-visual-font-lock', '');
      style.textContent = `
        :host {
          --uui-font-family: '${fontFamily}', sans-serif !important;
          font-family: '${fontFamily}', sans-serif !important;
          font-kerning: none;
          font-synthesis: none;
          -webkit-font-smoothing: antialiased;
        }

        :host *,
        :host *::before,
        :host *::after {
          font-family: inherit !important;
          font-kerning: none;
          text-rendering: geometricPrecision;
          -webkit-font-smoothing: antialiased;
        }
      `;
      root.append(style);
    }

    await document.fonts.ready;
    await (element as { updateComplete?: Promise<unknown> }).updateComplete;
  }, VISUAL_TEST_FONT_FAMILY);
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
    await (element as { updateComplete?: Promise<unknown> }).updateComplete;
  });
  await applyDeterministicFont(page, storyEl);

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
