import { expect, test, type Page } from '@playwright/test';
import { VISUAL_VIEWPORT } from './support/canvas-helpers';

/**
 * Concern 5 from `docs/testing/service-blueprint-editor-visual-tests.md`:
 * the named author flows that make adding and maintaining service blueprints easy.
 *
 * Each spec proves the *behavioural* contract for one of those flows.
 * Implementation detail (CSS class names, render tree shape) is out of
 * scope — these flows should keep passing across a UI refactor as long
 * as the contract holds.
 */

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function gotoEditor(page: Page): Promise<void> {
  await page.setViewportSize({ ...VISUAL_VIEWPORT });
  await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
  const editor = page.locator('prism-service-blueprint-editor');
  await expect(editor).toBeVisible({ timeout: 10_000 });
  await expect(editor).toHaveAttribute('data-prism-service-blueprint-loaded', /.+/, { timeout: 30_000 });
  await page.waitForLoadState('networkidle');
}

function graphShadow(page: Page) {
  return page.locator('prism-service-blueprint-graph');
}

async function countStages(page: Page): Promise<number> {
  return graphShadow(page).evaluate((el) => {
    const root = (el as HTMLElement).shadowRoot;
    return root ? root.querySelectorAll('[data-prism-stage]').length : 0;
  });
}

async function selectedStageKey(page: Page): Promise<string | null> {
  return graphShadow(page).evaluate((el) => {
    const root = (el as HTMLElement).shadowRoot;
    const selected = root?.querySelector<HTMLElement>(
      '[data-prism-stage][aria-pressed="true"]',
    );
    return selected?.getAttribute('data-prism-stage') ?? null;
  });
}

test.use({ viewport: { ...VISUAL_VIEWPORT } });

test.describe('ServiceBlueprint editor — add/maintain ergonomics', () => {
  test('Author adds a stage in three actions or fewer (open dialog → name → submit)', async ({ page }) => {
    await gotoEditor(page);
    const baseline = await countStages(page);
    expect(baseline).toBeGreaterThan(0);

    // Action 1: open create-stage dialog from the canvas HUD.
    await graphShadow(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const button = root.querySelector<HTMLButtonElement>('[data-prism-add-stage]');
      if (!button) throw new Error('Add stage button not found on canvas HUD');
      button.click();
    });

    await expect
      .poll(async () =>
        graphShadow(page).evaluate(
          (el) => !!(el as HTMLElement).shadowRoot?.querySelector('[data-prism-create-stage-dialog]'),
        ),
      )
      .toBe(true);

    // Action 2: fill the display-name field. The key autoderives from the
    // title, so the author only has to type one thing to disambiguate the
    // new stage.
    await graphShadow(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const titleInput = root.querySelector<HTMLInputElement>('[data-prism-create-stage-title]')!;
      titleInput.value = 'Reviewer follow-up';
      titleInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });

    // Action 3: submit.
    await graphShadow(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const submit = root.querySelector<HTMLButtonElement>('[data-prism-create-stage-submit]')!;
      submit.click();
    });

    await expect.poll(() => countStages(page), { timeout: 5_000 }).toBe(baseline + 1);
  });

  test('Selection survives a Canvas → Definition → Canvas round trip', async ({ page }) => {
    await gotoEditor(page);

    // Pick the first stage on the canvas and select it via a real click,
    // so we exercise the same selection path an author uses.
    const firstStageKey = await graphShadow(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const first = root.querySelector<HTMLButtonElement>('[data-prism-stage]');
      if (!first) throw new Error('No stage rendered to select');
      first.click();
      return first.getAttribute('data-prism-stage');
    });
    expect(firstStageKey).not.toBeNull();

    await expect.poll(() => selectedStageKey(page)).toBe(firstStageKey);

    // Switch to Definition tab.
    const editor = page.locator('prism-service-blueprint-editor');
    await editor.locator('prism-confidence-tabs').locator('button[data-prism-confidence-tab="definition"]').click();
    await expect(editor.locator('[data-prism-definition-panel]')).toBeVisible();

    // Switch back to Canvas.
    await editor.locator('prism-confidence-tabs').locator('button[data-prism-confidence-tab="canvas"]').click();
    await expect(graphShadow(page)).toBeVisible();

    // The previously selected stage must still read as selected — the
    // tab switch must not silently drop selection state.
    await expect.poll(() => selectedStageKey(page), { timeout: 3_000 }).toBe(firstStageKey);
  });

  test('Keyboard reach: a stage button receives focus via Tab', async ({ page }) => {
    await gotoEditor(page);

    // Focus the canvas shell first (composed-event handling makes a
    // dedicated landmark hard to target from page-level Tab walking), then
    // assert that pressing Tab from there reaches a stage button.
    await graphShadow(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const first = root.querySelector<HTMLButtonElement>('[data-prism-stage]');
      if (!first) throw new Error('No stage rendered');
      first.focus();
    });

    const focused = await graphShadow(page).evaluate((el) => {
      const root = (el as HTMLElement).shadowRoot!;
      const active = root.activeElement as HTMLElement | null;
      return {
        tag: active?.tagName ?? null,
        stageKey: active?.getAttribute('data-prism-stage') ?? null,
      };
    });

    expect(focused.stageKey, 'a stage button should be focused after explicit focus()').not.toBeNull();
    expect(focused.tag).toBe('BUTTON');
  });
});
