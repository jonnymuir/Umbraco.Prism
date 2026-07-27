import { expect, test, type Page } from '@playwright/test';

/**
 * Definition editor UX essentials: wheel scrolling and Find (Cmd/Ctrl+F).
 * Covers the fixes applied in squad/82-named-lanes-editor-slice.
 */

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function openDefinitionTab(page: Page): Promise<void> {
  const editor = page.locator('prism-service-blueprint-editor');
  await expect(editor).toBeVisible({ timeout: 10_000 });
  await expect(editor).toHaveAttribute('data-prism-service-blueprint-loaded', /.+/, { timeout: 30_000 });

  const definitionTab = editor
    .locator('prism-confidence-tabs')
    .locator('button[data-prism-confidence-tab="definition"]');
  await definitionTab.click();
  await expect(editor.locator('[data-prism-definition-panel]')).toBeVisible();
  await page.waitForFunction(() => {
    const host = document.querySelector('prism-service-blueprint-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor');
    return !!def?.shadowRoot?.querySelector('.cm-content');
  }, { timeout: 10_000 });
}

async function focusDefinitionEditor(page: Page): Promise<void> {
  await page.evaluate(() => {
    const host = document.querySelector('prism-service-blueprint-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor') as HTMLElement | null;
    const content = def?.shadowRoot?.querySelector('.cm-content') as HTMLElement | null;
    content?.focus();
  });
}

test.describe('Definition editor UX — wheel scrolling + Find', () => {
  test('Mouse wheel scrolling container is properly configured for scrolling', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    // Verify the parent doesn't have overflow:hidden (which was the original bug).
    const parentOverflow = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor') as HTMLElement | null;
      if (!def) {
        throw new Error('prism-definition-editor not found');
      }
      const style = window.getComputedStyle(def);
      return style.overflow;
    });

    // The parent should not have overflow:hidden, which was blocking wheel events.
    expect(parentOverflow).not.toBe('hidden');

    // Verify the CodeMirror scroller has overflow: auto (allowing scrolling).
    const { scrollerOverflow, isActuallyScrollable, clientHeight, scrollHeight } = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      const scroller = def?.shadowRoot?.querySelector('.cm-scroller') as HTMLElement | null;
      if (!scroller) {
        throw new Error('CodeMirror scroller not found');
      }
      const style = window.getComputedStyle(scroller);
      return {
        scrollerOverflow: style.overflowY,
        isActuallyScrollable: scroller.scrollHeight > scroller.clientHeight,
        clientHeight: scroller.clientHeight,
        scrollHeight: scroller.scrollHeight,
      };
    });

    expect(scrollerOverflow).toBe('auto');

    // CRITICAL: The content must actually overflow the visible area.
    // If scrollHeight <= clientHeight, there's no scrollbar and trackpad/wheel has nothing to scroll.
    expect(isActuallyScrollable).toBe(true);
    expect(scrollHeight).toBeGreaterThan(clientHeight);

    // Verify scrolling actually works by programmatically scrolling.
    const scrollWorked = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      const scroller = def?.shadowRoot?.querySelector('.cm-scroller') as HTMLElement | null;
      if (!scroller) {
        return false;
      }
      const before = scroller.scrollTop;
      scroller.scrollTop = 100;
      const after = scroller.scrollTop;
      return after > before;
    });

    expect(scrollWorked).toBe(true);
  });

  test('Cmd/Ctrl+F opens the CodeMirror search panel', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);
    await focusDefinitionEditor(page);

    // The search panel should not be visible initially.
    const panelBefore = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      return !!def?.shadowRoot?.querySelector('.cm-search');
    });
    expect(panelBefore).toBe(false);

    // Press Cmd/Ctrl+F to open the search panel.
    const isMac = await page.evaluate(() => navigator.platform.toLowerCase().includes('mac'));
    if (isMac) {
      await page.keyboard.press('Meta+f');
    } else {
      await page.keyboard.press('Control+f');
    }
    await page.waitForTimeout(100);

    const panelAfter = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      return !!def?.shadowRoot?.querySelector('.cm-search');
    });
    expect(panelAfter).toBe(true);

    // The search panel should have an input field.
    const hasInput = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      return !!def?.shadowRoot?.querySelector('.cm-search input');
    });
    expect(hasInput).toBe(true);
  });

  test('Esc dismisses the search panel', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);
    await focusDefinitionEditor(page);

    // Open the search panel.
    const isMac = await page.evaluate(() => navigator.platform.toLowerCase().includes('mac'));
    if (isMac) {
      await page.keyboard.press('Meta+f');
    } else {
      await page.keyboard.press('Control+f');
    }
    await page.waitForTimeout(100);

    const panelOpen = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      return !!def?.shadowRoot?.querySelector('.cm-search');
    });
    expect(panelOpen).toBe(true);

    // Press Esc to close the panel.
    await page.keyboard.press('Escape');
    await page.waitForTimeout(100);

    const panelClosed = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      return !!def?.shadowRoot?.querySelector('.cm-search');
    });
    expect(panelClosed).toBe(false);
  });

  test('Line numbers are visible', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const hasLineNumbers = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      return !!def?.shadowRoot?.querySelector('.cm-lineNumbers');
    });
    expect(hasLineNumbers).toBe(true);
  });

  test('Select-all (Cmd/Ctrl+A) works', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);
    await focusDefinitionEditor(page);

    const docLength = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor') as
        (HTMLElement & { _view?: { state: { doc: { length: number } } } }) | null;
      return def?._view?.state.doc.length ?? 0;
    });
    expect(docLength).toBeGreaterThan(0);

    // Select all.
    const isMac = await page.evaluate(() => navigator.platform.toLowerCase().includes('mac'));
    if (isMac) {
      await page.keyboard.press('Meta+a');
    } else {
      await page.keyboard.press('Control+a');
    }
    await page.waitForTimeout(100);

    const selectionLength = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor') as
        (HTMLElement & { _view?: { state: { selection: { main: { from: number; to: number } } } } }) | null;
      const sel = def?._view?.state.selection.main;
      return sel ? sel.to - sel.from : 0;
    });

    expect(selectionLength).toBe(docLength);
  });
});
