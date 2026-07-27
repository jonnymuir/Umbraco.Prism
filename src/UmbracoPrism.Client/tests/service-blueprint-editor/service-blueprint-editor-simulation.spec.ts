import { expect, test } from '@playwright/test';
// TODO Slice E: re-cert after gateway-pill rendering + simulation reshape. See .squad/decisions/inbox/copilot-slice-d-close-out.md.

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

test.describe('ServiceBlueprint editor path simulation', () => {
  test.fixme('starts from the initial stage, advances through the happy path, and highlights the graph path', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-simulation-start]').click();

    await expect(page.locator('[data-prism-simulation-initial-stage]')).toHaveText('Declaration');
    await expect(page.locator('[data-prism-simulation-current-stage]')).toHaveText('Declaration');
    await expect(page.locator('[data-prism-stage="declaration"]')).toHaveAttribute('data-prism-stage-simulation-current', 'true');
    await expect(page.locator('[data-prism-simulation-history]')).toContainText('Declaration');

    await page.locator('[data-prism-simulation-transition="0"]').click();
    await expect(page.locator('[data-prism-simulation-current-stage]')).toHaveText('Application Form');
    await expect(page.locator('[data-prism-transition="0"]')).toHaveAttribute('data-prism-transition-simulation-path', 'true');

    await page.locator('[data-prism-simulation-transition="1"]').click();
    await expect(page.locator('[data-prism-simulation-current-stage]')).toHaveText('Check your answers');

    await page.locator('[data-prism-simulation-transition="2"]').click();
    await expect(page.locator('[data-prism-simulation-current-stage]')).toHaveText('Application submitted');
    await expect(page.locator('[data-prism-simulation-stop-reason="terminal"]')).toContainText('end stage');
    await expect(page.locator('[data-prism-stage="submitted"]')).toHaveAttribute('data-prism-stage-simulation-current', 'true');
    await expect(page.locator('[data-prism-simulation-history]')).toContainText('Application submitted');
  });

  test.fixme('supports a rejection path and keeps the breadcrumb in sync', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--simulation-branches'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-simulation-start]').click();
    await page.locator('[data-prism-simulation-transition="0"]').click();
    await page.locator('[data-prism-simulation-transition="1"]').click();

    await expect(page.locator('[data-prism-simulation-current-stage]')).toHaveText('Reviewer decision');
    await expect(page.locator('[data-prism-simulation-transition="3"]')).toContainText('reject');

    await page.locator('[data-prism-simulation-transition="3"]').click();
    await expect(page.locator('[data-prism-simulation-current-stage]')).toHaveText('Application rejected');
    await expect(page.locator('[data-prism-simulation-stop-reason="terminal"]')).toContainText('end stage');
    await expect(page.locator('[data-prism-simulation-history]')).toContainText('Reviewer decision');
    await expect(page.locator('[data-prism-simulation-history]')).toContainText('Application rejected');
  });

  test.fixme('blocks invalid transitions and stops automatically at waiting stages', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--simulation-blockers'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await page.locator('[data-prism-simulation-start]').click();
    await page.locator('[data-prism-simulation-transition="0"]').click();
    await page.locator('[data-prism-simulation-transition="1"]').click();

    const blockedTransition = page.locator('[data-prism-simulation-transition="3"]');
    await expect(blockedTransition).toBeDisabled();
    await expect(page.locator('[data-prism-simulation-blocker="3"]')).toContainText('missing target stage');

    await page.locator('[data-prism-simulation-transition="4"]').click();
    await expect(page.locator('[data-prism-simulation-current-stage]')).toHaveText('Checks pending');
    await expect(page.locator('[data-prism-simulation-stop-reason="waiting"]')).toContainText('waiting stage');
    await expect(page.locator('[data-prism-stage="checks-pending"]')).toHaveAttribute('data-prism-stage-simulation-current', 'true');
  });
});
