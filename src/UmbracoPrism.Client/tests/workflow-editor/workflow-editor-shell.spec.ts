import { expect, test, type Page } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function waitForWorkflowLoad(page: Page, workflowKey: string): Promise<void> {
  await expect(page.locator('prism-workflow-editor')).toHaveAttribute('data-prism-workflow-loaded', workflowKey, {
    timeout: 30_000,
  });
}

test.describe('Workflow editor shell proof', () => {
  test('workflow switching changes the rendered workflow, not just the selector state', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-shell--reference-shell'));

    const shell = page.locator('[data-prism-component="workflow-editor-shell"]');
    const selector = page.getByRole('combobox', { name: 'Select workflow' });
    const editor = page.locator('prism-workflow-editor');
    const editorTitle = editor.locator('.editor-title');

    await expect(shell).toBeVisible({ timeout: 10_000 });
    await expect(selector).toBeVisible();

    await waitForWorkflowLoad(page, 'planning');
    await expect(shell).toHaveAttribute('data-prism-active-workflow', 'planning');
    await expect(editorTitle).toHaveText('Planning Application');
    await expect(editor.locator('[data-prism-stage="application-form"]')).toBeVisible();

    await selector.selectOption('community-enquiry');
    await waitForWorkflowLoad(page, 'community-enquiry');
    await expect(shell).toHaveAttribute('data-prism-active-workflow', 'community-enquiry');
    await expect(editorTitle).toHaveText('Community Enquiry');
    await expect(editor.locator('[data-prism-stage="review-enquiry"]')).toBeVisible();
    await expect(editor.locator('[data-prism-stage="application-form"]')).toHaveCount(0);

    await selector.selectOption('payment-demo');
    await waitForWorkflowLoad(page, 'payment-demo');
    await expect(shell).toHaveAttribute('data-prism-active-workflow', 'payment-demo');
    await expect(editorTitle).toHaveText('Payment Demo');
    await expect(editor.locator('[data-prism-stage="payment-complete"]')).toBeVisible();
    await expect(editor.locator('[data-prism-stage="confirm-payment-received"]')).toBeVisible();
    await expect(editor.locator('[data-prism-stage="review-enquiry"]')).toHaveCount(0);
  });

  test('workflow switcher keeps the host-supplied workflowSource alive while remounting the editor', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-shell--reference-shell'));

    const selector = page.getByRole('combobox', { name: 'Select workflow' });
    const editor = page.locator('prism-workflow-editor');

    await waitForWorkflowLoad(page, 'planning');

    const initialHostWiring = await editor.evaluate(node => {
      const source = (node as unknown as { workflowSource?: object }).workflowSource;
      const availableQueues = ((node as unknown as { availableQueues?: Array<{ queueName: string }> }).availableQueues ?? [])
        .map(queue => queue.queueName);
      return source
        ? { sourceName: source.constructor.name, queueNames: availableQueues }
        : null;
    });
    expect(initialHostWiring).not.toBeNull();
    expect(initialHostWiring?.queueNames).toContain('payments');

    await selector.selectOption('information-request');
    await waitForWorkflowLoad(page, 'information-request');

    const hostWiringSurvived = await editor.evaluate((node, expected) => {
      const source = (node as unknown as { workflowSource?: object }).workflowSource;
      const availableQueues = ((node as unknown as { availableQueues?: Array<{ queueName: string }> }).availableQueues ?? [])
        .map(queue => queue.queueName);
      return source
        ? source.constructor.name === expected.sourceName
          && JSON.stringify(availableQueues) === JSON.stringify(expected.queueNames)
        : false;
    }, initialHostWiring);
    expect(hostWiringSurvived).toBe(true);

    await expect(editor.locator('.editor-title')).toHaveText('Information Request');
    await expect(editor.locator('[data-prism-stage="review-response-pack"]')).toBeVisible();
  });

  test('payment demo uses host-provided queues and stays validation-clean in the editor', async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-shell--reference-shell'));

    const selector = page.getByRole('combobox', { name: 'Select workflow' });
    const editor = page.locator('prism-workflow-editor');

    await selector.selectOption('payment-demo');
    await waitForWorkflowLoad(page, 'payment-demo');

    await expect(editor.locator('.editor-title')).toHaveText('Payment Demo');
    await expect(editor.locator('[data-prism-stage="confirm-payment-received"]')).toHaveAttribute(
      'aria-label',
      'Confirm payment received, Payments team queue'
    );

    await page.getByRole('tab', { name: 'Validation' }).click();
    await expect(page.locator('[data-prism-validation-issue]')).toHaveCount(0);
  });

  test('graph-canvas is the scrollable region while shell chrome stays anchored', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 560 });
    await page.goto(storyUrl('workflow-editor-editor-host--simulation-branches'));

    await expect(page.locator('prism-workflow-editor')).toBeVisible({ timeout: 10_000 });

    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - .graph-canvas should have overflow-y: auto (or scroll)
    // - .graph-canvas should be the scrollable region containing the graph workspace
    // - .graph-viewport should NOT scroll (it's the container, not the scrollable surface)
    // - The surrounding shell (outline, inspector, toolbar) should stay anchored while canvas scrolls
    const scrollState = await page.locator('prism-workflow-graph').evaluate(graphElement => {
      const graph = graphElement as HTMLElement;
      const shadowRoot = graph.shadowRoot;
      const canvas = shadowRoot?.querySelector<HTMLElement>('.graph-canvas');
      if (!canvas) {
        return null;
      }

      const before = canvas.scrollTop;
      canvas.scrollTop = 240;
      return {
        before,
        after: canvas.scrollTop,
        canvasOverflowY: getComputedStyle(canvas).overflowY,
      };
    });

    expect(scrollState).not.toBeNull();
    expect(scrollState?.after ?? 0).toBeGreaterThan(scrollState?.before ?? 0);
    expect(scrollState?.canvasOverflowY === 'auto' || scrollState?.canvasOverflowY === 'scroll').toBeTruthy();
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  });

  test('graph-canvas scrolling does not move shell chrome (outline, inspector, toolbar)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.goto(storyUrl('workflow-editor-editor-shell--reference-shell'));

    await waitForWorkflowLoad(page, 'planning');

    const outline = page.locator('[data-prism-workflow-outline]');
    const inspector = page.locator('[data-prism-component="step-inspector"]');
    const toolbar = page.locator('.editor-toolbar');

    await expect(outline).toBeVisible({ timeout: 10_000 });
    await expect(inspector).toBeVisible({ timeout: 10_000 });
    await expect(toolbar).toBeVisible({ timeout: 10_000 });

    // Capture initial positions of shell chrome
    const outlineBefore = await outline.boundingBox();
    const inspectorBefore = await inspector.boundingBox();
    const toolbarBefore = await toolbar.boundingBox();

    // Scroll the graph-canvas
    await page.locator('prism-workflow-graph').evaluate(graphElement => {
      const graph = graphElement as HTMLElement;
      const shadowRoot = graph.shadowRoot;
      const canvas = shadowRoot?.querySelector<HTMLElement>('.graph-canvas');
      if (canvas) {
        canvas.scrollTop = 150;
      }
    });

    // Wait a bit for any unintended reflows
    await page.waitForTimeout(200);

    // Verify shell chrome positions haven't moved
    const outlineAfter = await outline.boundingBox();
    const inspectorAfter = await inspector.boundingBox();
    const toolbarAfter = await toolbar.boundingBox();

    expect(outlineAfter?.y).toBe(outlineBefore?.y);
    expect(inspectorAfter?.y).toBe(inspectorBefore?.y);
    expect(toolbarAfter?.y).toBe(toolbarBefore?.y);
    
    // Window body should still be at scroll position 0
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  });

  test.fixme('outline drawer collapse/expand controls stay accessible', async ({ page }) => {
    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - [data-prism-panel-toggle="outline"] button with aria-controls + aria-expanded
    // - [data-prism-panel="outline"] region that collapses without removing the toggle from tab order
    // - toggle should restore focus to the currently selected outline item when re-expanded
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));
  });

  test.fixme('properties drawer collapse/expand controls stay accessible', async ({ page }) => {
    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - [data-prism-panel-toggle="properties"] button with aria-controls + aria-expanded
    // - [data-prism-panel="properties"] region that remains labelled when collapsed
    // - Enter/Space toggles, Esc collapses when focus is inside the drawer, and focus returns to the toggle
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));
  });

  test.fixme('graph-only editor removes list workspace affordances from the shell', async ({ page }) => {
    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - no toolbar button named "List view"
    // - no [data-prism-linear-table] surface rendered
    // - shell stays on the graph canvas while outline/properties drawers handle density
    await page.goto(storyUrl('workflow-editor-editor-host--planning-workflow'));
  });
});
