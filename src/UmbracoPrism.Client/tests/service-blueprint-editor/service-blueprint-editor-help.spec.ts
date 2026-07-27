import { expect, test } from '@playwright/test';
import { SERVICE_BLUEPRINT_SHORTCUT_GROUPS } from '../../src/service-blueprint-editor/editor-shortcuts';
// TODO Slice E: re-cert after gateway-pill rendering + simulation reshape. See .squad/decisions/inbox/copilot-slice-d-close-out.md.

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function dispatchEditorShortcut(
  page: import('@playwright/test').Page,
  detail: { key: string; shiftKey?: boolean; ctrlKey?: boolean; metaKey?: boolean }
) {
  await page.locator('prism-service-blueprint-editor').evaluate((element, shortcut) => {
    element.dispatchEvent(new KeyboardEvent('keydown', {
      key: shortcut.key,
      bubbles: true,
      composed: true,
      shiftKey: shortcut.shiftKey ?? false,
      ctrlKey: shortcut.ctrlKey ?? false,
      metaKey: shortcut.metaKey ?? false,
    }));
  }, detail);
}

function editorModifier() {
  const isMac = process.platform === 'darwin';
  return { ctrlKey: !isMac, metaKey: isMac };
}

test.describe('ServiceBlueprint editor help and shortcut reference', () => {
  test('help button and F1 open the shortcut guide, and the list stays aligned with the exported shortcut map', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    const helpButton = page.locator('[data-prism-help]');
    await helpButton.focus();
    await helpButton.click();

    const dialog = page.locator('[data-prism-shortcut-dialog]');
    await expect(dialog).toBeVisible();

    for (const group of SERVICE_BLUEPRINT_SHORTCUT_GROUPS) {
      const groupSection = dialog.locator(`[data-prism-shortcut-group="${group.id}"]`);
      await expect(groupSection).toContainText(group.title);
      for (const shortcut of group.shortcuts) {
        const row = groupSection.locator(`[data-prism-shortcut="${shortcut.id}"]`);
        await expect(row).toContainText(shortcut.command);
        await expect(row).toContainText(shortcut.context);
        for (const label of shortcut.labels) {
          await expect(row).toContainText(label);
        }
      }
    }

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(helpButton).toBeFocused();

    await dispatchEditorShortcut(page, { key: 'F1' });
    await expect(dialog).toBeVisible();
  });

  test.fixme('save and redo shortcuts stay discoverable and wired to the host editor commands', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-stage="declaration"]').dblclick();

    const titleInput = page.locator('[data-prism-stage-title]');
    await titleInput.fill('Declaration shortcut check');
    await titleInput.press('Tab');

    await dispatchEditorShortcut(page, { key: 's', ...editorModifier() });
    await expect(page.locator('[data-prism-toast]')).toContainText('ServiceBlueprint saved and published.');
    await expect(page.locator('[data-prism-save]')).toHaveAttribute('aria-keyshortcuts', 'Control+S Meta+S');
    await expect(page.locator('[data-prism-help]')).toHaveAttribute('aria-keyshortcuts', 'F1');

    await page.locator('[data-prism-undo]').click();
    await expect(page.locator('[data-prism-stage="declaration"]')).toContainText('Declaration');

    await dispatchEditorShortcut(page, { key: 'y', ...editorModifier() });
    await expect(page.locator('[data-prism-stage="declaration"]')).toContainText('Declaration shortcut check');
    await expect(page.locator('[data-prism-redo]')).toHaveAttribute(
      'aria-keyshortcuts',
      'Control+Y Meta+Y Control+Shift+Z Meta+Shift+Z'
    );
  });

  test('empty service blueprints show getting-started guidance and still expose help', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--empty-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('[data-prism-empty-state="graph"]')).toContainText('Start building your service blueprint');
    await expect(page.locator('[data-prism-empty-state="graph"]')).toContainText('Add the next stage before you branch');

    const dialog = page.locator('[data-prism-shortcut-dialog]');
    // The story play() function clicks the help button and opens the dialog as part of
    // Storybook's own interaction test. Wait for it to finish, dismiss it, then verify
    // the button opens it again so we are testing a real user interaction here.
    await expect(dialog).toBeVisible({ timeout: 5_000 });
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();

    await page.locator('[data-prism-help]').click();
    await expect(dialog).toBeVisible();
  });
});
