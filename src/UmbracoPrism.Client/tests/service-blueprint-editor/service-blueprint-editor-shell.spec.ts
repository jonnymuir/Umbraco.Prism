import { expect, test, type Page } from '@playwright/test';
import { measureGraph, rectanglesOverlap } from './support/canvas-helpers';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function waitForServiceBlueprintLoad(page: Page, serviceBlueprintKey: string): Promise<void> {
  await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute('data-prism-service-blueprint-loaded', serviceBlueprintKey, {
    timeout: 30_000,
  });
}

async function loadPaymentDemo(page: Page): Promise<void> {
  await page.goto(storyUrl('service-blueprint-editor-editor-shell--reference-shell'));

  const selector = page.getByRole('combobox', { name: 'Select service blueprint' });
  await expect(selector).toBeVisible();
  await selector.selectOption('payment-demo');
  await waitForServiceBlueprintLoad(page, 'payment-demo');
}

test.describe('ServiceBlueprint editor shell proof', () => {
  test('service blueprint switching changes the rendered service blueprint, not just the selector state', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-shell--reference-shell'));

    const shell = page.locator('[data-prism-component="service-blueprint-editor-shell"]');
    const selector = page.getByRole('combobox', { name: 'Select service blueprint' });
    const editor = page.locator('prism-service-blueprint-editor');
    const editorTitle = editor.locator('.editor-title');

    await expect(shell).toBeVisible({ timeout: 10_000 });
    await expect(selector).toBeVisible();

    await waitForServiceBlueprintLoad(page, 'planning');
    await expect(shell).toHaveAttribute('data-prism-active-service-blueprint', 'planning');
    await expect(editorTitle).toHaveText('Planning Application');
    await expect(editor.locator('[data-prism-stage="application-form"]')).toBeVisible();

    await selector.selectOption('community-enquiry');
    await waitForServiceBlueprintLoad(page, 'community-enquiry');
    await expect(shell).toHaveAttribute('data-prism-active-service-blueprint', 'community-enquiry');
    await expect(editorTitle).toHaveText('Community Enquiry');
    await expect(editor.locator('[data-prism-stage="review-enquiry"]')).toBeVisible();
    await expect(editor.locator('[data-prism-stage="application-form"]')).toHaveCount(0);

    await selector.selectOption('payment-demo');
    await waitForServiceBlueprintLoad(page, 'payment-demo');
    await expect(shell).toHaveAttribute('data-prism-active-service-blueprint', 'payment-demo');
    await expect(editorTitle).toHaveText('Payment Demo');
    await expect(editor.locator('[data-prism-stage="payment-complete"]')).toBeVisible();
    await expect(editor.locator('[data-prism-stage="confirm-payment-received"]')).toBeVisible();
    await expect(editor.locator('[data-prism-stage="review-enquiry"]')).toHaveCount(0);
  });

  test('service blueprint switcher keeps the host-supplied serviceBlueprintSource alive while remounting the editor', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-shell--reference-shell'));

    const selector = page.getByRole('combobox', { name: 'Select service blueprint' });
    const editor = page.locator('prism-service-blueprint-editor');

    await waitForServiceBlueprintLoad(page, 'planning');

    const initialHostWiring = await editor.evaluate(node => {
      const source = (node as unknown as { serviceBlueprintSource?: object }).serviceBlueprintSource;
      const availableQueues = ((node as unknown as { availableQueues?: Array<{ queueName: string }> }).availableQueues ?? [])
        .map(queue => queue.queueName);
      return source
        ? { sourceName: source.constructor.name, queueNames: availableQueues }
        : null;
    });
    expect(initialHostWiring).not.toBeNull();
    expect(initialHostWiring?.queueNames).toContain('payments');

    await selector.selectOption('information-request');
    await waitForServiceBlueprintLoad(page, 'information-request');

    const hostWiringSurvived = await editor.evaluate((node, expected) => {
      const source = (node as unknown as { serviceBlueprintSource?: object }).serviceBlueprintSource;
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

  test('payment demo uses host-provided queues, stays validation-clean, and can still save', async ({ page }) => {
    await loadPaymentDemo(page);

    const editor = page.locator('prism-service-blueprint-editor');

    await expect(editor.locator('.editor-title')).toHaveText('Payment Demo');
    await expect(editor.locator('[data-prism-stage="confirm-payment-received"]')).toHaveAttribute(
      'aria-label',
      'Confirm payment received, Payments team queue'
    );
    await expect(editor.locator('[data-prism-save]')).toBeEnabled();

    await editor.locator('[data-prism-save]').click();
    await expect(editor.locator('[data-prism-toast]')).toContainText('Service blueprint saved.');
    await expect(editor.locator('[data-prism-save]')).toBeEnabled();

    await page.getByRole('tab', { name: 'Validation' }).click();
    await expect(page.locator('[data-prism-validation-issue]')).toHaveCount(0);
    await expect(page.locator('[data-prism-save-status]')).toContainText('Service blueprint saved.');
  });

  test.fixme('payment demo graph keeps node copy readable without overlap', async ({ page }) => {
    await loadPaymentDemo(page);

    const geometry = await measureGraph(page);
    expect(geometry.nodes.length).toBeGreaterThan(0);

    for (let index = 0; index < geometry.nodes.length; index += 1) {
      for (let nextIndex = index + 1; nextIndex < geometry.nodes.length; nextIndex += 1) {
        const left = geometry.nodes[index];
        const right = geometry.nodes[nextIndex];
        expect(
          rectanglesOverlap(left, right),
          `"${left.label || left.key}" overlaps "${right.label || right.key}" in the payment demo graph.`,
        ).toBe(false);
      }
    }

    const routeChipRects = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const root = (graphElement as HTMLElement).shadowRoot;
      if (!root) {
        throw new Error('Graph shadow root not found');
      }

      const scene = root.querySelector<HTMLElement>('.react-flow__viewport');
      if (!scene) {
        throw new Error('Graph scene not found');
      }

      const sceneRect = scene.getBoundingClientRect();
      const rel = (rect: DOMRect) => ({
        left: rect.left - sceneRect.left,
        right: rect.right - sceneRect.left,
        top: rect.top - sceneRect.top,
        bottom: rect.bottom - sceneRect.top,
      });

      return Array.from(root.querySelectorAll<HTMLElement>('[data-prism-transition]'))
        .map(chip => ({
          label: chip.textContent?.trim() ?? '',
          ...rel(chip.getBoundingClientRect()),
        }));
    });

    for (const chip of routeChipRects) {
      for (const node of geometry.nodes) {
        expect(
          rectanglesOverlap(chip, node),
          `Route label "${chip.label}" overlaps "${node.label || node.key}" in the payment demo graph.`,
        ).toBe(false);
      }
    }

    const textMetrics = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const root = (graphElement as HTMLElement).shadowRoot;
      if (!root) {
        throw new Error('Graph shadow root not found');
      }

      return Array.from(root.querySelectorAll<HTMLElement>('.node-label, .pill-trigger'))
        .map(element => ({
          text: element.textContent?.trim() ?? '',
          scrollWidth: element.scrollWidth,
          clientWidth: element.clientWidth,
        }))
        .filter(metric => metric.text.length > 0);
    });

    expect(textMetrics.length).toBeGreaterThan(0);
    for (const metric of textMetrics) {
      expect(
        metric.scrollWidth,
        `"${metric.text}" is clipped in the payment demo graph.`,
      ).toBeLessThanOrEqual(metric.clientWidth + 1);
    }
  });

  test('payment demo graph copy stays product-facing and drops implementation-detail gateway badges', async ({ page }) => {
    await loadPaymentDemo(page);

    const graphCopy = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const root = (graphElement as HTMLElement).shadowRoot;
      if (!root) {
        throw new Error('Graph shadow root not found');
      }

      return {
        subtitle: root.querySelector('.service-blueprint-subtitle')?.textContent?.trim() ?? '',
        hint: root.querySelector('.graph-hint')?.textContent?.trim() ?? '',
        roledescription: root.querySelector('.graph-canvas')?.getAttribute('aria-roledescription') ?? '',
        gatewayBadges: Array.from(root.querySelectorAll<HTMLElement>('.gateway-kind-badge')).map(element => element.textContent?.trim() ?? ''),
        metaCopy: Array.from(root.querySelectorAll<HTMLElement>('.node-meta')).map(element => element.textContent?.trim() ?? ''),
      };
    });

    expect(graphCopy.subtitle).toBe('Visual service blueprint map');
    expect(graphCopy.hint).not.toContain('queue-owned stages');
    expect(graphCopy.hint).not.toContain('outgoing routes');
    expect(graphCopy.roledescription).toBe('Service blueprint graph editor');
    expect(graphCopy.gatewayBadges).toEqual([]);
    expect(graphCopy.metaCopy.join(' ')).not.toContain('related route');
    expect(graphCopy.metaCopy.join(' ')).not.toContain('Split gateway');
    expect(graphCopy.metaCopy.join(' ')).not.toContain('Join gateway');
  });

  test('payment demo canvas removes the extra confirmation route gateway once the route shape is simplified', async ({ page }) => {
    await loadPaymentDemo(page);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph.locator('[data-prism-gateway]')).toHaveCount(2);
    await expect(graph.locator('[data-prism-gateway="confirm-payment-route"]')).toHaveCount(0);
  });

  test('payment demo graph visual cleanup stays stable', async ({ page }) => {
    await loadPaymentDemo(page);

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toHaveScreenshot('payment-demo-cleanup.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.02,
    });
  });

  test('graph canvas pans its content while the window stays anchored', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 560 });
    await page.goto(storyUrl('service-blueprint-editor-editor-host--simulation-branches'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const stageTops = () => page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const shadowRoot = (graphElement as HTMLElement).shadowRoot!;
      return Array.from(shadowRoot.querySelectorAll<HTMLElement>('[data-prism-stage-card]'))
        .map(node => node.getBoundingClientRect().top);
    });
    const canvasBox = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const rect = (graphElement as HTMLElement).shadowRoot!
        .querySelector<HTMLElement>('.graph-canvas')!.getBoundingClientRect();
      return { x: rect.left, y: rect.top, height: rect.height };
    });

    const before = await stageTops();
    const viewportHeight = page.viewportSize()!.height;
    const startX = canvasBox.x + 24;
    // Drag from the midpoint of the canvas's on-screen band: the editor-host
    // story is taller than the constrained viewport and the HUD can wrap,
    // so neither the canvas centre nor a fixed offset is reliably visible.
    const visibleBottom = Math.min(canvasBox.y + canvasBox.height, viewportHeight - 10);
    const startY = (canvasBox.y + visibleBottom) / 2;
    await page.mouse.move(startX, startY);
    await page.mouse.down();
    await page.mouse.move(startX, startY - 200, { steps: 6 });
    await page.mouse.up();
    const after = await stageTops();

    expect(before[0] - after[0], 'pane drag must pan the graph content').toBeGreaterThan(100);
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  });

  test('graph-canvas scrolling does not move shell chrome (outline, inspector, toolbar)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.goto(storyUrl('service-blueprint-editor-editor-shell--reference-shell'));

    await waitForServiceBlueprintLoad(page, 'planning');

    const outline = page.locator('[data-prism-service-blueprint-outline]');
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
    await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
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
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
  });

  test.fixme('properties drawer collapse/expand controls stay accessible', async ({ page }) => {
    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - [data-prism-panel-toggle="properties"] button with aria-controls + aria-expanded
    // - [data-prism-panel="properties"] region that remains labelled when collapsed
    // - Enter/Space toggles, Esc collapses when focus is inside the drawer, and focus returns to the toggle
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
  });

  test.fixme('graph-only editor removes list workspace affordances from the shell', async ({ page }) => {
    // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
    // - no toolbar button named "List view"
    // - no [data-prism-linear-table] surface rendered
    // - shell stays on the graph canvas while outline/properties drawers handle density
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
  });
});
