import { expect, test } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

// Slice 3b.1: route creation/editing relocated to the gateway inspector's
// outgoing-routes panel. The drag handle, keyboard 't' shortcut, and the
// dedicated create-transition dialog have all been retired. The single
// behavioural test below covers Tangy's review item #5: "Author editing a
// gateway's outgoing route can set the condition that fires it from the
// gateway inspector".
test.describe('Gateway-first route editing', () => {
  test("author editing a gateway's outgoing route can set the condition that fires it from the gateway inspector", async ({ page }) => {
    await page.goto(storyUrl('workflow-editor-editor-host--gateway-representation'));

    const editor = page.locator('prism-workflow-editor');
    await expect(editor).toBeVisible({ timeout: 10_000 });

    const outline = editor.locator('prism-workflow-outline');
    await outline.locator('[data-prism-outline-gateway="review-split"]').click();

    const inspector = editor.locator('prism-step-inspector');
    await expect(inspector.locator('[data-prism-gateway-detail="review-split"]')).toBeVisible();

    const routeBlock = inspector.locator('[data-prism-route-target="reviewer-assessment"]');
    await expect(routeBlock).toBeVisible();

    await routeBlock.locator('[data-prism-route-condition-mode]').selectOption('guard');
    const conditionInput = routeBlock.locator('[data-prism-route-condition-value]');
    await conditionInput.fill('application.readyForReview == true');
    await conditionInput.press('Enter');
    await conditionInput.blur();

    // (a) Inspector reflects the updated condition value.
    await expect(routeBlock.locator('[data-prism-route-condition-value]'))
      .toHaveValue('application.readyForReview == true');

    // (b) Underlying transition condition is updated in the workflow model.
    const updatedCondition = await inspector.evaluate(node => {
      const el = node as unknown as { workflow: { transitions: Array<{ toStage: string; condition?: string }> } | null };
      const transition = el.workflow?.transitions.find(t => t.toStage === 'reviewer-assessment');
      return transition?.condition ?? null;
    });
    expect(updatedCondition).toContain('application.readyForReview == true');

    // (c) The polite live region announced the condition update.
    const announcement = await inspector.evaluate(node => {
      const announcer = (node as HTMLElement).shadowRoot?.getElementById('inspector-announcer');
      return announcer?.textContent?.trim() ?? '';
    });
    expect(announcement).toMatch(/route condition updated/i);
  });
});
