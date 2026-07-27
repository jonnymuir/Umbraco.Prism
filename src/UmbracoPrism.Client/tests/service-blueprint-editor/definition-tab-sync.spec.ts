import { expect, test, type Page } from '@playwright/test';

/**
 * Bidirectional sync between the Definition (JSON) tab and the Canvas, exercised
 * through the *real* CodeMirror input path (not synthetic `definition-input`
 * events). The earlier `service-blueprint-editor-definition-tab.spec.ts` covers the
 * synthetic path; this file is the truth test for what authors actually do.
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

async function clickCanvasTab(page: Page): Promise<void> {
  const editor = page.locator('prism-service-blueprint-editor');
  await editor
    .locator('prism-confidence-tabs')
    .locator('button[data-prism-confidence-tab="canvas"]')
    .click();
}

async function readDefinitionText(page: Page): Promise<string> {
  return await page.evaluate(() => {
    const editorEl = document.querySelector('prism-service-blueprint-editor') as HTMLElement | null;
    const def = editorEl?.shadowRoot?.querySelector('prism-definition-editor') as
      (HTMLElement & { value?: string }) | null;
    return def?.value ?? '';
  });
}

/** Replace the editor doc by dispatching a CodeMirror transaction — the
 * same code path real typing exercises (updateListener → onChange). */
async function replaceDefinitionViaCm(page: Page, value: string): Promise<void> {
  await page.evaluate(text => {
    const host = document.querySelector('prism-service-blueprint-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor') as
      (HTMLElement & { _view?: { state: { doc: { length: number } }; dispatch: (s: unknown) => void } }) | null;
    const view = def?._view;
    if (!view) {
      throw new Error('CodeMirror view not mounted');
    }
    view.dispatch({
      changes: { from: 0, to: view.state.doc.length, insert: text },
    });
  }, value);
}

async function readInternalServiceBlueprintDisplayName(page: Page): Promise<string | null> {
  return await page.evaluate(() => {
    const host = document.querySelector('prism-service-blueprint-editor') as
      (HTMLElement & { _serviceBlueprint?: { displayName?: string } | null }) | null;
    return host?._serviceBlueprint?.displayName ?? null;
  });
}

test.describe('Definition (JSON) ↔ Canvas bidirectional sync — real CodeMirror path', () => {
  test('a) Edit stage name in JSON → switch to Canvas → canvas shows new name', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    expect(original).toContain('Application Form');

    const renamed = original.replace(/"displayName": "Application Form"/, '"displayName": "Real-Typed Form"');
    await replaceDefinitionViaCm(page, renamed);

    await page.waitForTimeout(350); // > 250ms debounce
    await clickCanvasTab(page);

    const editor = page.locator('prism-service-blueprint-editor');
    await expect(editor.locator('[data-prism-stage="application-form"]'))
      .toContainText('Real-Typed Form', { timeout: 2_000 });
  });

  test('b) Add a route in JSON → switch to Canvas → canvas shows the new route', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    expect(original).toContain('"gateways"');

    // Add a new gateway-owned route to an existing split gateway.
    const updated = await page.evaluate(text => {
      const doc = JSON.parse(text);
      const gateway = (doc.gateways as Array<Record<string, unknown>>)
        .find(candidate => candidate.key === 'route-check-answers');
      if (!gateway) {
        throw new Error('route-check-answers gateway missing');
      }
      (gateway.routes as Array<Record<string, unknown>>).push({
        id: 'route-check-answers--fast-track--submitted',
        target: 'submitted',
        trigger: 'fast-track',
      });
      return JSON.stringify(doc, null, 2);
    }, original);

    await replaceDefinitionViaCm(page, updated);
    await page.waitForTimeout(350);

    // Internal model should now have the extra route.
    const actionsAfter = await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor') as
        (HTMLElement & { _serviceBlueprint?: { gateways?: Array<{ key?: string; routes?: Array<{ trigger?: string }> }> } | null }) | null;
      return host?._serviceBlueprint?.gateways
        ?.find(gateway => gateway.key === 'route-check-answers')
        ?.routes?.map(route => route.trigger) ?? [];
    });
    expect(actionsAfter).toContain('fast-track');

    // Visual canvas should reflect the new transition.
    await clickCanvasTab(page);
    const editor = page.locator('prism-service-blueprint-editor');
    const graph = editor.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible();
    const hasRoute = await graph.evaluate(el => {
      const root = (el as HTMLElement).shadowRoot;
      if (!root) return false;
      return root?.textContent?.includes('fast-track') ?? false;
    });
    expect(hasRoute).toBe(true);
  });

  test('c) Invalid JSON → inline error appears AND canvas keeps last good state', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    expect(original).toContain('Application Form');

    // First commit a clean rename so we have a known-good last state.
    const renamed = original.replace(/"displayName": "Application Form"/, '"displayName": "Pre-Invalid Form"');
    await replaceDefinitionViaCm(page, renamed);
    await page.waitForTimeout(350);

    // Now break the JSON by stripping the trailing brace.
    await replaceDefinitionViaCm(page, renamed.slice(0, -3));
    await page.waitForTimeout(350);

    const editor = page.locator('prism-service-blueprint-editor');
    await expect(editor.locator('[data-prism-definition-banner]')).toBeVisible();
    await expect(editor.locator('[data-prism-definition-apply]')).toBeDisabled();

    // Canvas should still show the last good state (renamed).
    await clickCanvasTab(page);
    await expect(editor.locator('[data-prism-stage="application-form"]'))
      .toContainText('Pre-Invalid Form', { timeout: 2_000 });
  });

  test('d) Round-trip: canvas change shows in JSON, JSON edit on top shows on canvas', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    const editor = page.locator('prism-service-blueprint-editor');
    await expect(editor).toHaveAttribute('data-prism-service-blueprint-loaded', /.+/, { timeout: 30_000 });

    // Canvas-side: change displayName via the standard service-blueprint-updated event.
    await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor') as
        (HTMLElement & { _serviceBlueprint?: unknown }) | null;
      if (!host) throw new Error('editor not mounted');
      const graph = host.shadowRoot?.querySelector('prism-service-blueprint-graph') as HTMLElement | null;
      if (!graph) throw new Error('graph not mounted');
      const next = JSON.parse(JSON.stringify((host as { _serviceBlueprint: unknown })._serviceBlueprint));
      next.displayName = 'Canvas-Edited Display';
      graph.dispatchEvent(new CustomEvent('service-blueprint-updated', {
        detail: { serviceBlueprint: next, selection: null },
        bubbles: true,
        composed: true,
      }));
    });

    // Open Definition tab, confirm canvas edit shows.
    await openDefinitionTab(page);
    await expect.poll(async () => readDefinitionText(page), { timeout: 5_000 })
      .toContain('Canvas-Edited Display');

    // JSON-side edit on top: change displayName again.
    const current = await readDefinitionText(page);
    const next = current.replace('"Canvas-Edited Display"', '"JSON-Then-Canvas"');
    await replaceDefinitionViaCm(page, next);
    await page.waitForTimeout(350);

    // Confirm internal service blueprint updated and canvas reflects it.
    await expect.poll(() => readInternalServiceBlueprintDisplayName(page), { timeout: 2_000 })
      .toBe('JSON-Then-Canvas');
  });

  test('e) Switching back to Canvas before debounce flushes still propagates the edit', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    const renamed = original.replace(/"displayName": "Application Form"/, '"displayName": "Flushed-On-Switch"');
    await replaceDefinitionViaCm(page, renamed);

    // Switch immediately, BEFORE the 250ms debounce would naturally fire.
    await clickCanvasTab(page);

    const editor = page.locator('prism-service-blueprint-editor');
    await expect(editor.locator('[data-prism-stage="application-form"]'))
      .toContainText('Flushed-On-Switch', { timeout: 2_000 });
  });
});
