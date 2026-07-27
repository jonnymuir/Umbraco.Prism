import { expect, test, type Page } from '@playwright/test';

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

type SaveAttempt =
  | { kind: 'success' }
  | {
    kind: 'error';
    error: {
      title: string;
      summary: string;
      detailLines: string[];
      traceId: string;
      message: string;
    };
  };

const structuredSaveFailure = {
  title: 'We couldn’t save this service blueprint',
  summary: 'The host app rejected these changes. Review the details below and try again.',
  detailLines: [
    'ServiceBlueprint key did not match the route.',
    'Fix the service blueprint key and try again.',
    'System.InvalidOperationException: do not expose this detail',
    '   at SaveServiceBlueprint() in ServiceBlueprintController.cs:line 42',
  ],
  traceId: 'trace-save-001',
  message: 'System.InvalidOperationException: hidden internal failure',
};

async function configureSaveAttempts(page: Page, attempts: SaveAttempt[]): Promise<void> {
  await page.locator('prism-service-blueprint-editor').evaluate((node, plannedAttempts: SaveAttempt[]) => {
    const editor = node as HTMLElement & {
      serviceBlueprintSource?: {
        list: () => Promise<unknown>;
        load: (key: string) => Promise<unknown>;
        save: (key: string, serviceBlueprint: unknown) => Promise<void>;
      };
    };
    const currentSource = editor.serviceBlueprintSource;
    if (!currentSource) {
      throw new Error('ServiceBlueprint source not found.');
    }

    let attemptIndex = 0;
    Object.defineProperty(window, '__prismCopiedSaveError', {
      configurable: true,
      writable: true,
      value: '',
    });
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: async (value: string) => {
          (window as typeof window & { __prismCopiedSaveError: string }).__prismCopiedSaveError = value;
        },
      },
    });

    editor.serviceBlueprintSource = {
      list: () => currentSource.list(),
      load: (key: string) => currentSource.load(key),
      save: async (key: string, serviceBlueprint: unknown) => {
        const currentAttempt = plannedAttempts[Math.min(attemptIndex, plannedAttempts.length - 1)] ?? { kind: 'success' };
        attemptIndex += 1;
        if (currentAttempt.kind === 'error') {
          const error = new Error(currentAttempt.error.message);
          error.name = 'ServiceBlueprintSaveError';
          Object.assign(error, currentAttempt.error);
          throw error;
        }

        return currentSource.save(key, serviceBlueprint);
      },
    };
  }, attempts);
}

test.describe('ServiceBlueprint editor validation rail', () => {
  test('keeps detailed warning copy in Validation instead of repeating it across the canvas', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-stage="declaration"]').dblclick();
    const actionInput = page.locator('[data-prism-action-param="0-formDefinitionId"]');
    await expect(actionInput).toHaveValue('planning-declaration');
    await actionInput.evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = '';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });

    await expect(page.locator('[data-prism-action-errors="0"]')).toContainText('Form definition id is required');
    await expect(page.locator('[data-prism-validation-rail]')).toContainText(
      'Stage “Declaration” has an action that needs attention: “Load form” — Form definition id is required.'
    );
    await expect(page.locator('[data-prism-save]')).toBeEnabled();

    await page.locator('[data-prism-add-stage]').click();
    const createStageDialog = page.locator('[data-prism-create-stage-dialog]');
    await expect(createStageDialog).toBeVisible();
    await createStageDialog.locator('[data-prism-create-stage-title]').evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = 'Site visit';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });
    await createStageDialog.locator('[data-prism-create-stage-key]').evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = 'site-visit';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });
    await createStageDialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(createStageDialog).toBeHidden();

    const validationRail = page.locator('[data-prism-validation-rail]');
    await expect(validationRail).toContainText('Connect it through a gateway so authors can reach it.');
    await expect(validationRail).toContainText('Site visit');
    await expect(page.locator('[data-prism-save]')).toBeDisabled();
    await expect(page.locator('[data-prism-canvas-health-hint]')).toContainText('Open Validation');

    const canvasWarnings = await page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
      const graph = graphElement as HTMLElement;
      const shadowRoot = graph.shadowRoot;
      if (!shadowRoot) {
        throw new Error('Graph shadow root not found');
      }

      return {
        title: shadowRoot.querySelector('.validation-banner-title')?.textContent?.trim() ?? '',
        issues: Array.from(shadowRoot.querySelectorAll('.validation-link')).map(issue => issue.textContent?.trim() ?? ''),
      };
    });

    expect(canvasWarnings.title).toBe('');
    expect(canvasWarnings.issues).toEqual([]);

    const validationTab = page.getByRole('tab', { name: 'Validation' });
    await expect(validationTab).toBeVisible();
    await page.locator('[data-prism-open-validation]').click();
    await expect(validationTab).toHaveAttribute('aria-selected', 'true');
    await page.locator('[data-prism-validation-issue]').filter({ hasText: 'Site visit' }).first().click();
    await expect(page.locator('[data-prism-stage-detail="site-visit"]')).toBeVisible();
  });

  test('shows plain-language issues and jumps to the affected stage or field', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });

    await page.locator('[data-prism-stage="declaration"]').dblclick();
    const actionInput = page.locator('[data-prism-action-param="0-formDefinitionId"]');
    await expect(actionInput).toHaveValue('planning-declaration');
    await actionInput.evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = '';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });

    await expect(page.locator('[data-prism-action-errors="0"]')).toContainText('Form definition id is required');
    await expect(page.locator('[data-prism-validation-rail]')).toContainText(
      'Stage “Declaration” has an action that needs attention: “Load form” — Form definition id is required.'
    );

    await page.locator('[data-prism-add-stage]').click();
    const createStageDialog = page.locator('[data-prism-create-stage-dialog]');
    await expect(createStageDialog).toBeVisible();
    await createStageDialog.locator('[data-prism-create-stage-title]').evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = 'Site visit';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });
    await createStageDialog.locator('[data-prism-create-stage-key]').evaluate(element => {
      const input = element as HTMLInputElement;
      input.value = 'site-visit';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });
    await createStageDialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(createStageDialog).toBeHidden();

    const validationTab = page.getByRole('tab', { name: 'Validation' });
    await validationTab.click();
    await expect(validationTab).toHaveAttribute('aria-selected', 'true');

    const unreachableIssue = page.locator('[data-prism-validation-issue="stage-unreachable-site-visit"]');
    if (await unreachableIssue.count()) {
      await unreachableIssue.click();
      await expect(page.locator('[data-prism-stage-detail="site-visit"]')).toBeVisible();
      await validationTab.click();
      await expect(validationTab).toHaveAttribute('aria-selected', 'true');
    }

    await page.locator('[data-prism-validation-issue*="declaration-action-0-formDefinitionId"]').click();
    await expect(page.locator('[data-prism-stage-detail="declaration"]')).toBeVisible();
    await expect(actionInput).toBeFocused();
  });

  test('reports a successful save in plain language', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await configureSaveAttempts(page, [{ kind: 'success' }]);

    await page.locator('[data-prism-save]').click();

    await expect(page.locator('[data-prism-save-error]')).toHaveCount(0);
    await expect(page.locator('[data-prism-save-status]')).toContainText('Service blueprint saved.');
    await expect(page.locator('[data-prism-toast]')).toContainText('Service blueprint saved.');
  });

  test('shows structured save failures in plain language', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await configureSaveAttempts(page, [{ kind: 'error', error: structuredSaveFailure }]);

    await page.locator('[data-prism-save]').click();

    const saveError = page.locator('[data-prism-save-error]');
    await expect(saveError).toBeVisible();
    await expect(saveError).toContainText(structuredSaveFailure.title);
    await expect(saveError).toContainText(structuredSaveFailure.summary);
    await expect(saveError).toContainText('ServiceBlueprint key did not match the route.');
    await expect(saveError).toContainText('Fix the service blueprint key and try again.');
    await expect(saveError).toContainText(`Reference: ${structuredSaveFailure.traceId}`);
    await expect(saveError).not.toContainText('InvalidOperationException');
    await expect(saveError).not.toContainText('SaveServiceBlueprint()');
    await expect(page.locator('[data-prism-save-status]')).toContainText(
      structuredSaveFailure.summary
    );
  });

  test('keeps save failures visible and copyable for support handoff', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await configureSaveAttempts(page, [{ kind: 'error', error: structuredSaveFailure }]);

    await page.locator('[data-prism-save]').click();

    const saveError = page.locator('[data-prism-save-error]');
    await expect(saveError).toBeVisible();

    await page.waitForTimeout(3_500);
    await expect(saveError).toBeVisible();
    await expect(page.locator('[data-prism-save-error-details]')).toHaveValue(
      [
        structuredSaveFailure.title,
        structuredSaveFailure.summary,
        'ServiceBlueprint key did not match the route.',
        'Fix the service blueprint key and try again.',
        'do not expose this detail',
        `Reference: ${structuredSaveFailure.traceId}`,
      ].join('\n')
    );

    await page.locator('[data-prism-copy-save-error]').click();
    await expect(page.locator('[data-prism-save-error-copy-status]')).toContainText('Save error details copied.');

    const copiedError = await page.evaluate(() =>
      (window as typeof window & { __prismCopiedSaveError: string }).__prismCopiedSaveError
    );
    expect(copiedError).toContain(structuredSaveFailure.title);
    expect(copiedError).toContain('ServiceBlueprint key did not match the route.');
    expect(copiedError).toContain('do not expose this detail');
    expect(copiedError).toContain(`Reference: ${structuredSaveFailure.traceId}`);
    expect(copiedError).not.toContain('InvalidOperationException');
    expect(copiedError).not.toContain('SaveServiceBlueprint()');
  });

  test('clears the save error surface after a successful retry', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await configureSaveAttempts(page, [
      { kind: 'error', error: structuredSaveFailure },
      { kind: 'success' },
    ]);

    await page.locator('[data-prism-save]').click();
    await expect(page.locator('[data-prism-save-error]')).toBeVisible();

    await page.locator('[data-prism-save]').click();
    await expect(page.locator('[data-prism-save-error]')).toHaveCount(0);
    await expect(page.locator('[data-prism-save-status]')).toContainText('Service blueprint saved.');
    await expect(page.locator('[data-prism-toast]')).toContainText('Service blueprint saved.');
  });

  test('dismiss button removes the save error surface without needing a retry', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-editor-host--planning-service-blueprint'));

    await expect(page.locator('prism-service-blueprint-editor')).toBeVisible({ timeout: 10_000 });
    await configureSaveAttempts(page, [{ kind: 'error', error: structuredSaveFailure }]);

    await page.locator('[data-prism-save]').click();

    const saveError = page.locator('[data-prism-save-error]');
    await expect(saveError).toBeVisible();
    await expect(saveError).toContainText(structuredSaveFailure.title);

    await page.locator('[data-prism-dismiss-save-error]').click();

    await expect(saveError).toHaveCount(0);
    await expect(page.locator('[data-prism-save-error-copy-status]')).toHaveCount(0);
  });
});
