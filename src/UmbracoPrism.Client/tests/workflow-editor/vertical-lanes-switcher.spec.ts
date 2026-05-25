import { expect, test, type Page } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function waitForWorkflowLoad(page: Page, workflowKey: string): Promise<void> {
  await expect(page.locator('prism-workflow-editor')).toHaveAttribute('data-prism-workflow-loaded', workflowKey, {
    timeout: 30_000,
  });
}

test.describe('Vertical lanes, workflow switching, and graph-only proof', () => {
  test('workflow switching updates the rendered workflow shell and graph content', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-shell--reference-shell'));

    const selector = page.getByRole('combobox', { name: 'Select workflow' });
    const editor = page.locator('prism-workflow-editor');

    await waitForWorkflowLoad(page, 'planning');
    await selector.focus();
    await expect(selector).toBeFocused();
    await expect(selector).toHaveAttribute('aria-label', 'Select workflow');

    await selector.selectOption('community-enquiry');
    await waitForWorkflowLoad(page, 'community-enquiry');
    await expect(selector).toHaveValue('community-enquiry');
    await expect(editor.locator('.editor-title')).toHaveText('Community Enquiry');
    await expect(editor.locator('[data-prism-stage="review-enquiry"]')).toBeVisible();

    await selector.selectOption('information-request');
    await waitForWorkflowLoad(page, 'information-request');
    await expect(selector).toHaveValue('information-request');
    await expect(editor.locator('.editor-title')).toHaveText('Information Request');
    await expect(editor.locator('[data-prism-stage="review-response-pack"]')).toBeVisible();
  });

  test('switching workflows still renders role-first lanes for the loaded definition', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-shell--reference-shell'));

    const selector = page.getByRole('combobox', { name: 'Select workflow' });
    await waitForWorkflowLoad(page, 'planning');

    await selector.selectOption('payment-demo');
    await waitForWorkflowLoad(page, 'payment-demo');

    const graphCanvas = page.locator('prism-workflow-graph').getByRole('application');
    const lanes = page.locator('prism-workflow-graph').locator('[data-prism-role-lane]');

    await expect(graphCanvas).toHaveAttribute('aria-roledescription', /role-first/i);
    expect(await lanes.count()).toBeGreaterThan(1);
    await expect(page.locator('prism-workflow-editor').locator('[data-prism-stage="review-payment"]')).toBeVisible();
    await expect(page.locator('prism-workflow-editor').locator('[data-prism-stage="payment-received"]')).toBeVisible();
  });

  test('graph-canvas is the vertical scroll surface in the graph workspace', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 560 });
    await page.goto(storyUrl('workflow-editor-editor-host--simulation-branches'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - .graph-canvas should scroll vertically (overflow-y: auto)
    // - The page body should NOT scroll (window.scrollY stays 0)
    // - Vertical lanes stacked layout increases canvas scrollHeight
    const scrollResult = await page.locator('prism-workflow-graph').evaluate(graphElement => {
      const graph = graphElement as HTMLElement;
      const shadowRoot = graph.shadowRoot;
      const canvas = shadowRoot?.querySelector<HTMLElement>('.graph-canvas');
      if (!canvas) {
        return null;
      }

      const before = canvas.scrollTop;
      canvas.scrollTop = 220;
      return {
        before,
        after: canvas.scrollTop,
        overflowY: getComputedStyle(canvas).overflowY,
        scrollHeight: canvas.scrollHeight,
        clientHeight: canvas.clientHeight,
      };
    });

    expect(scrollResult).not.toBeNull();
    expect(scrollResult?.after ?? 0).toBeGreaterThan(scrollResult?.before ?? 0);
    expect(scrollResult?.overflowY === 'auto' || scrollResult?.overflowY === 'scroll').toBeTruthy();
    expect(scrollResult?.scrollHeight ?? 0).toBeGreaterThan(scrollResult?.clientHeight ?? 0);
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  });

  test.fixme('graph-only editor removes the list workspace after the shell simplification lands', async ({ page }) => {
    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - remove the mode toggle that switches to list view
    // - keep [data-prism-role-lane] graph semantics as the sole authoring workspace
    // - rely on collapsible drawers, not a secondary list workspace, for density management
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));
  });
});
