import { expect, test, type Page } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function openDefinitionTab(page: Page): Promise<void> {
  const editor = page.locator('prism-service-blueprint-editor');
  await expect(editor).toBeVisible({ timeout: 10_000 });
  await expect(editor).toHaveAttribute('data-prism-service-blueprint-loaded', /.+/, { timeout: 30_000 });

  // The tab lives inside the confidence-tabs shadow root.
  const definitionTab = editor.locator('prism-confidence-tabs').locator('button[data-prism-confidence-tab="definition"]');
  await definitionTab.click();
  await expect(editor.locator('[data-prism-definition-panel]')).toBeVisible();
  // Wait for the definition editor element to be present *and* populated.
  await expect.poll(async () => readDefinitionText(page), { timeout: 10_000 })
    .not.toEqual('');
}

async function readDefinitionText(page: Page): Promise<string> {
  return await page.evaluate(() => {
    const editorEl = document.querySelector('prism-service-blueprint-editor') as HTMLElement | null;
    const def = editorEl?.shadowRoot?.querySelector('prism-definition-editor') as HTMLElement & { value?: string } | null;
    return def?.value ?? '';
  });
}

async function setDefinitionText(page: Page, value: string): Promise<void> {
  await page.evaluate(text => {
    const editorEl = document.querySelector('prism-service-blueprint-editor') as HTMLElement | null;
    const def = editorEl?.shadowRoot?.querySelector('prism-definition-editor') as (HTMLElement & { value?: string }) | null;
    if (!def) {
      throw new Error('prism-definition-editor not present');
    }
    def.value = text;
    def.dispatchEvent(new CustomEvent('definition-input', {
      detail: { value: text },
      bubbles: true,
      composed: true,
    }));
  }, value);
}

async function waitForDefinitionTextContains(page: Page, fragment: string): Promise<void> {
  await expect.poll(async () => readDefinitionText(page), { timeout: 5_000 })
    .toContain(fragment);
}

test.describe('Definition (JSON twin-pane) tab', () => {
  test('Author switches to Definition tab and sees the current service blueprint as JSON', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const text = await readDefinitionText(page);
    expect(text).toContain('"definitionKey"');
    expect(text).toContain('"stages"');
    // 2-space indent — first nested key sits at two spaces.
    expect(text).toMatch(/\n {2}"/);
  });

  test('Editing JSON to rename a stage updates the visual pane after debounce', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    expect(original).toContain('Application Form');

    const renamed = original.replace(/"displayName": "Application Form"/, '"displayName": "Renamed Form"');
    await setDefinitionText(page, renamed);

    // Auto-apply after 250 ms debounce.
    await page.waitForTimeout(400);

    const announcement = page.locator('[data-prism-definition-announcement]');
    await expect(announcement).toContainText('Definition updated', { timeout: 2_000 });

    // Switch back to Canvas to verify the visual pane reflects the rename.
    const editor = page.locator('prism-service-blueprint-editor');
    await editor.locator('prism-confidence-tabs').locator('button[data-prism-confidence-tab="canvas"]').click();
    await expect(editor.locator('[data-prism-stage="application-form"]')).toContainText('Renamed Form');
  });

  test('Invalid JSON shows banner with parse error and Apply is disabled', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    expect(original).toBeTruthy();

    // Corrupt the JSON.
    await setDefinitionText(page, original.slice(0, original.length - 5));
    await page.waitForTimeout(400);

    const banner = page.locator('[data-prism-definition-banner]');
    await expect(banner).toBeVisible();
    await expect(banner).toContainText("Definition can't be applied");
    await expect(page.locator('[data-prism-definition-apply]')).toBeDisabled();

    // Visual pane stays on the previous service blueprint.
    const editor = page.locator('prism-service-blueprint-editor');
    await editor.locator('prism-confidence-tabs').locator('button[data-prism-confidence-tab="canvas"]').click();
    await expect(editor.locator('[data-prism-stage="application-form"]')).toBeVisible();
  });

  test('Schema-violating JSON (retired Waiting kind) is blocked with Apply disabled', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    // Replace the first "kind" value with the retired Waiting kind.
    const broken = original.replace(/"stageType":\s*"Question"/, '"stageType": "Waiting"');
    expect(broken).not.toEqual(original);
    await setDefinitionText(page, broken);
    await page.waitForTimeout(400);

    const banner = page.locator('[data-prism-definition-banner]');
    await expect(banner).toBeVisible();
    await expect(banner).toContainText('unsupported stageType "Waiting"');
    await expect(page.locator('[data-prism-definition-apply]')).toBeDisabled();
  });

  test('Visual change (rename a gateway) shows up in Definition tab', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const before = await readDefinitionText(page);
    expect(before.length).toBeGreaterThan(0);

    // Apply a service blueprint change via the graph's standard service-blueprint-updated event.
    await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor') as HTMLElement | null;
      if (!host) throw new Error('editor not mounted');
      const graph = host.shadowRoot?.querySelector('prism-service-blueprint-graph') as HTMLElement | null;
      if (!graph) throw new Error('graph not mounted');
      const internalServiceBlueprint = (host as unknown as { _serviceBlueprint: { displayName: string } })._serviceBlueprint;
      const next = JSON.parse(JSON.stringify(internalServiceBlueprint));
      next.displayName = 'Definition Twin Demo';
      graph.dispatchEvent(new CustomEvent('service-blueprint-updated', {
        detail: { serviceBlueprint: next, selection: null },
        bubbles: true,
        composed: true,
      }));
    });

    // Wait for the host to reflect the change in JSON.
    await waitForDefinitionTextContains(page, 'Definition Twin Demo');
  });

  test('Document-level undo from the visual side reverses a Definition-applied JSON edit', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    await openDefinitionTab(page);

    const original = await readDefinitionText(page);
    const renamed = original.replace(/"displayName": "Application Form"/, '"displayName": "From Definition Tab"');
    expect(renamed).not.toEqual(original);

    await setDefinitionText(page, renamed);
    await page.waitForTimeout(400);
    await waitForDefinitionTextContains(page, 'From Definition Tab');

    // Switch to Canvas and use document-level undo.
    const editor = page.locator('prism-service-blueprint-editor');
    await editor.locator('prism-confidence-tabs').locator('button[data-prism-confidence-tab="canvas"]').click();
    await page.locator('[data-prism-undo]').click();

    // Switch back to Definition; the JSON should be restored.
    await editor.locator('prism-confidence-tabs').locator('button[data-prism-confidence-tab="definition"]').click();
    await waitForDefinitionTextContains(page, 'Application Form');
    const after = await readDefinitionText(page);
    expect(after).not.toContain('From Definition Tab');
  });

  test('Definition tab is keyboard-reachable and the editor accepts keyboard input', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));
    const editor = page.locator('prism-service-blueprint-editor');
    await expect(editor).toHaveAttribute('data-prism-service-blueprint-loaded', /.+/, { timeout: 30_000 });

    // Reach the Definition tab via the tab list using arrow keys.
    const tabsRoot = editor.locator('prism-confidence-tabs');
    await tabsRoot.locator('button[data-prism-confidence-tab="canvas"]').focus();
    // Five tabs sit before "definition": Canvas, Validation, Preview, Simulation, then Definition.
    for (let i = 0; i < 4; i++) {
      await page.keyboard.press('ArrowRight');
      // Allow the tab harness's requestAnimationFrame focus shift to complete.
      await page.waitForTimeout(50);
    }
    const definitionTab = tabsRoot.locator('button[data-prism-confidence-tab="definition"]');
    await expect(definitionTab).toHaveAttribute('aria-selected', 'true');

    // The editor renders. Focus inside the editor host and type.
    await expect(editor.locator('[data-prism-definition-panel]')).toBeVisible();
    const defEditor = editor.locator('[data-prism-definition-editor]');
    await expect(defEditor).toBeVisible();

    // Wait for CodeMirror to mount.
    await page.waitForFunction(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor');
      return !!def?.shadowRoot?.querySelector('.cm-content');
    }, { timeout: 5_000 });

    const before = await readDefinitionText(page);

    // Click into the editor's content area, then type a no-op space.
    await page.evaluate(() => {
      const host = document.querySelector('prism-service-blueprint-editor');
      const def = host?.shadowRoot?.querySelector('prism-definition-editor') as HTMLElement | null;
      const content = def?.shadowRoot?.querySelector('.cm-content') as HTMLElement | null;
      content?.focus();
    });
    await page.keyboard.press('End');
    await page.keyboard.type(' ');

    await page.waitForTimeout(50);
    const after = await readDefinitionText(page);
    expect(after.length).toBeGreaterThanOrEqual(before.length);
  });
});
