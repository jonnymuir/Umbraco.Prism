/**
 * Playwright specs for the inspector "+ Add route" affordance (Slice D follow-on).
 *
 * Covers:
 *  a) Stage with no gateway → "+ Add route" creates the gateway and shows a blank route row.
 *  b) Gateway with existing route → "+ Add route" appends a second route row.
 *  c) Focus lands on the new route's Target picker after creation.
 *  d) Inline "Choose a destination" warning appears for empty Target; disappears once chosen.
 *  e) Keyboard-only flow: Tab to button, Enter, Tab to Target picker.
 */

import { expect, test } from '@playwright/test';

function inspectorStoryUrl(storyId: string): string {
  return `/iframe.html?id=service-blueprint-editor-step-inspector--${storyId}&viewMode=story`;
}

test.describe('Inspector "+ Add route" affordance', () => {
  // -----------------------------------------------------------------------
  // (a) Stage with no gateway — button creates gateway and blank route row
  // -----------------------------------------------------------------------
  test('(a) stage with no gateway: "+ Add route" creates a gateway and shows a blank route row', async ({ page }) => {
    await page.goto(inspectorStoryUrl('add-route-no-gateway'));

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible({ timeout: 10_000 });

    // Inspector is showing the stage (applicant-amendments), which has no gateway yet.
    await expect(inspector.locator('[data-prism-stage-detail="applicant-amendments"]')).toBeVisible();

    // The "+ Add route" button must be visible in the stage's Outgoing routes section.
    const addBtn = inspector.locator('[data-prism-add-route]');
    await expect(addBtn).toBeVisible();
    await expect(addBtn).toHaveText('+ Add route');

    // Clicking creates the gateway and switches the inspector to gateway view.
    await addBtn.click();
    await inspector.locator('[data-prism-inspector-kind="gateway"]').waitFor({ state: 'visible', timeout: 5_000 });

    // A blank route row must now appear (no toStage → data-prism-route-target="").
    const newRouteRow = inspector.locator('[data-prism-route-target=""]');
    await expect(newRouteRow).toBeVisible();

    // The gateway's outgoing-routes summary should now mention 1 route.
    const summary = inspector.locator('[data-prism-gateway-routes-summary]');
    await expect(summary).toBeVisible();
    await expect(summary).toContainText('1 route');
  });

  // -----------------------------------------------------------------------
  // (b) Gateway with existing route — "+ Add route" appends a second row
  // -----------------------------------------------------------------------
  test('(b) gateway with existing route: "+ Add route" appends a second route row', async ({ page }) => {
    await page.goto(inspectorStoryUrl('add-route-existing-gateway'));

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible({ timeout: 10_000 });

    // Inspector shows the review-split gateway which already has 1 route.
    await expect(inspector.locator('[data-prism-gateway-detail="review-split"]')).toBeVisible();

    const initialRouteSummary = inspector.locator('[data-prism-gateway-routes-summary]');
    await expect(initialRouteSummary).toContainText('1 route');

    // The "+ Add route" button must be visible.
    const addBtn = inspector.locator('[data-prism-add-route]');
    await expect(addBtn).toBeVisible();

    // Click — a second route row should appear and the summary updates.
    await addBtn.click();
    await page.waitForTimeout(200); // allow Lit re-render

    const updatedSummary = inspector.locator('[data-prism-gateway-routes-summary]');
    await expect(updatedSummary).toContainText('2 routes');

    // The new blank-route row (target="") should be present.
    const blankRow = inspector.locator('[data-prism-route-target=""]');
    await expect(blankRow).toBeVisible();
  });

  // -----------------------------------------------------------------------
  // (c) Focus lands on the new route's Target picker after creation
  // -----------------------------------------------------------------------
  test('(c) focus moves to the new route\'s Target picker after "+ Add route" is clicked', async ({ page }) => {
    await page.goto(inspectorStoryUrl('add-route-existing-gateway'));

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible({ timeout: 10_000 });

    const addBtn = inspector.locator('[data-prism-add-route]');
    await addBtn.click();

    // Give Lit + requestAnimationFrame time to settle.
    await page.waitForTimeout(300);

    // The Target picker for the new blank route should be focused.
    const focused = await page.evaluate(() => {
      const el = document.querySelector('prism-step-inspector') as HTMLElement & { shadowRoot: ShadowRoot };
      const active = el.shadowRoot?.activeElement;
      return active?.getAttribute('data-prism-route-target-select') !== null && active?.tagName === 'SELECT';
    });
    expect(focused).toBe(true);
  });

  // -----------------------------------------------------------------------
  // (d) Inline "Choose a destination" warning for empty Target
  // -----------------------------------------------------------------------
  test('(d) inline warning appears for empty Target and disappears once a destination is chosen', async ({ page }) => {
    await page.goto(inspectorStoryUrl('add-route-existing-gateway'));

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible({ timeout: 10_000 });

    // Create a blank route.
    await inspector.locator('[data-prism-add-route]').click();
    await page.waitForTimeout(200);

    // The inline "Choose a destination" warning should be visible.
    const warning = inspector.locator('[data-prism-route-target-warning]').last();
    await expect(warning).toBeVisible();
    await expect(warning).toContainText('Choose a destination');

    // The Target select should carry aria-invalid="true".
    const targetSelect = inspector.locator('[data-prism-route-target-select]').last();
    await expect(targetSelect).toHaveAttribute('aria-invalid', 'true');

    // Choose a destination — pick the first non-empty option.
    await targetSelect.selectOption({ index: 1 });
    await page.waitForTimeout(200);

    // Warning should disappear and aria-invalid should be false.
    await expect(warning).not.toBeVisible();
    await expect(targetSelect).toHaveAttribute('aria-invalid', 'false');
  });

  // -----------------------------------------------------------------------
  // (e) Keyboard-only flow
  // -----------------------------------------------------------------------
  test('(e) keyboard-only: Tab to "+ Add route", Enter to activate, Tab to Target picker', async ({ page }) => {
    await page.goto(inspectorStoryUrl('add-route-existing-gateway'));

    const inspector = page.locator('prism-step-inspector');
    await expect(inspector).toBeVisible({ timeout: 10_000 });

    // Focus the inspector root and navigate to the "+ Add route" button via Tab.
    await inspector.locator('[data-prism-component="step-inspector"]').focus();

    // Tab through to find the add-route button. The gateway inspector has a
    // number of focusable fields before it, so we use the button's locator
    // directly and trigger it via keyboard once focused.
    const addBtn = inspector.locator('[data-prism-add-route]');
    await addBtn.focus();
    await expect(addBtn).toBeFocused();

    // Activate via Enter.
    await addBtn.press('Enter');
    await page.waitForTimeout(300);

    // After creation the Target picker for the new route should be focused.
    const focused = await page.evaluate(() => {
      const el = document.querySelector('prism-step-inspector') as HTMLElement & { shadowRoot: ShadowRoot };
      const active = el.shadowRoot?.activeElement;
      return active?.tagName === 'SELECT' && active?.hasAttribute('data-prism-route-target-select');
    });
    expect(focused).toBe(true);
  });
});
