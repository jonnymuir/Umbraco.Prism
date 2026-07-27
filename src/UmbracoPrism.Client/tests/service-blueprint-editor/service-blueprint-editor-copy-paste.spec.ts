import { expect, test } from '@playwright/test';
// TODO Slice E: re-cert after gateway-pill rendering + simulation reshape. See .squad/decisions/inbox/copilot-slice-d-close-out.md.

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

async function pressEditorShortcut(
  page: import('@playwright/test').Page,
  key: 'c' | 'v'
) {
  const isMac = process.platform === 'darwin';
  await page.locator('prism-service-blueprint-editor').evaluate((element, detail) => {
    element.dispatchEvent(new KeyboardEvent('keydown', {
      key: detail.key,
      bubbles: true,
      composed: true,
      metaKey: detail.isMac,
      ctrlKey: !detail.isMac,
    }));
  }, { key, isMac });
}

test.describe('ServiceBlueprint editor copy and paste', () => {
  test.fixme('stage copy and paste uses a new key, keeps transitions behind, and selects the pasted stage', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-stage="declaration"]').dblclick();
    await expect(page.locator('[data-prism-stage-detail="declaration"]')).toBeVisible();

    await pressEditorShortcut(page, 'c');
    await expect(page.locator('[data-prism-clipboard-state]')).toContainText('stage “Declaration” ready to paste');

    await page.locator('[data-prism-stage="application-form"]').dblclick();
    await pressEditorShortcut(page, 'v');

    await expect(page.locator('[data-prism-transition]')).toHaveCount(3);
    await expect(page.locator('[data-prism-stage="declaration-copy"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(page.locator('[data-prism-stage-detail="declaration-copy"]')).toBeVisible();
    await expect(page.locator('prism-step-inspector')).toBeFocused();
    await expect(page.locator('[data-prism-stage-detail="declaration-copy"]')).toContainText(
      'Add at least one outbound transition before publishing this stage.'
    );
  });

  test('action copy and paste works in the same stage and a different stage', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-stage="declaration"]').dblclick();
    await expect(page.locator('[data-prism-stage-detail="declaration"]')).toBeVisible();

    const originalAction = page.locator('[data-prism-stage-action="0"]');
    await originalAction.focus();
    await expect(originalAction).toHaveAttribute('data-prism-action-selected', 'true');

    await page.locator('[data-prism-copy]').click();
    await expect(page.locator('[data-prism-clipboard-state]')).toContainText('action “Load the declaration form.” ready to paste');

    await pressEditorShortcut(page, 'v');
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(2);
    await expect(page.locator('[data-prism-stage-action="1"]')).toHaveAttribute('data-prism-action-selected', 'true');
    await expect(page.locator('[data-prism-action-param="1-formDefinitionId"]')).toHaveValue('planning-declaration');

    await page.locator('[data-prism-stage="application-form"]').dblclick();
    await page.locator('[data-prism-paste]').click();

    await expect(page.locator('[data-prism-stage-detail="application-form"]')).toBeVisible();
    await expect(page.locator('[data-prism-stage-action]')).toHaveCount(2);
    await expect(page.locator('[data-prism-stage-action="1"]')).toHaveAttribute('data-prism-action-selected', 'true');
    await expect(page.locator('[data-prism-action-param="1-formDefinitionId"]')).toHaveValue('planning-declaration');
  });
});
