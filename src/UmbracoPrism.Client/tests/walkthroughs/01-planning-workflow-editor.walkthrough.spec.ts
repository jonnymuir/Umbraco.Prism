// Executable counterpart of docs/walkthroughs/planning-workflow-editor.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { readdir, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { test, expect } from '../support/shared-app-host-fixture';
import {
  step,
  signIn,
  resetWorkflows,
  businessAppOrigin,
  type PageHealthCheck,
} from './support/walkthrough';

const WALKTHROUGH_KEY = 'planning-workflow-editor';
const authoredWorkflowRoot = path.resolve(process.cwd(), '../UmbracoPrism.MockBusinessApp/workflow-authored');
const planningWorkflowPath = path.join(authoredWorkflowRoot, 'planning.workflow.json');
const provenancePath = path.join(authoredWorkflowRoot, '.provenance');

// URL pattern for the workflow editor SPA — stays constant across all steps.
// `workflow-editor.html` is served by MockBusinessApp (Isabelle's Wave 1 foundation deliverable).
const editorUrl = /workflow-editor\.html/;

// Reusable health check: all steps take place on the same SPA page.
function editorHealthCheck(override: Partial<PageHealthCheck> = {}): PageHealthCheck {
  return {
    url: editorUrl,
    heading: /planning application/i,
    bodyMustNotContain: /\b(404|Not Found|An error occurred|Server Error)\b/i,
    ...override,
  };
}

test.describe('Planning Workflow Editor walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  let originalPlanningWorkflow = '';
  let originalProvenanceFiles: string[] = [];

  test.beforeEach(async ({ request }) => {
    await resetWorkflows(request);
    originalPlanningWorkflow = await readFile(planningWorkflowPath, 'utf8');
    originalProvenanceFiles = await readdir(provenancePath).catch(() => []);
  });

  test.afterEach(async () => {
    await writeFile(planningWorkflowPath, originalPlanningWorkflow, 'utf8');

    const currentProvenanceFiles = await readdir(provenancePath).catch(() => []);
    const originalFiles = new Set(originalProvenanceFiles);
    await Promise.all(
      currentProvenanceFiles
        .filter(file => !originalFiles.has(file))
        .map(file => rm(path.join(provenancePath, file), { force: true }))
    );
  });

  // ---------------------------------------------------------------------------
  // SKIP RATIONALE
  //
  // This walkthrough exercises the live business-app host for the current editor
  // slice: role-first graph framing, inspector-first editing, and supporting
  // confidence surfaces (list mode, validation, preview, simulation, help).
  //
  // When both PRs have merged:
  //   1. Remove the `test.skip(true, ...)` line.
  //   2. Run with `CAPTURE_SCREENSHOTS=1` against the localhost-auth config to
  //      populate docs/images/walkthroughs/planning-workflow-editor/.
  //   3. Commit the screenshots and update this spec if any selectors drift.
  //
  // See SKILL.md R6 for the screenshot capture workflow.
  // ---------------------------------------------------------------------------

  test('happy path: authoring a planning permission workflow with the role-first workspace', async ({ page, appHost }) => {
    await signIn(page);

    // ─── Step 1: Load the reference shell from the business app ────────────────
    // The shell redirects /workflow-editor → /workflow-editor.html?workflow=planning
    // and demonstrates the minimal downstream integration surface.
    const workflowListResponse = page.waitForResponse(
      response =>
        response.request().method() === 'GET' &&
        response.url().includes('/api/workflow-authoring/workflows') &&
        response.status() === 200
    );
    await page.goto(`${businessAppOrigin}/workflow-editor`);
    await workflowListResponse;
    await expect(page).toHaveURL(/\/workflow-editor\.html\?workflow=planning(?:&|$)/);
    await expect(page.getByRole('heading', { name: /compose the editor into your app/i })).toBeVisible();
    await expect(page.getByText(/this shell stays focused on authoring/i)).toBeVisible();
    await expect(page.getByText(/let your business app own runtime workflows and domain actions/i)).toBeVisible();
    await expect(page.locator('[data-prism-component="workflow-editor-shell"]')).toHaveAttribute(
      'data-prism-active-workflow',
      'planning'
    );
    await expect(page.locator('prism-workflow-editor')).toHaveAttribute('data-prism-workflow-loaded', 'planning', {
      timeout: 30_000,
    });
    await expect(page.getByRole('combobox', { name: 'Workflow definition' })).toHaveValue('planning');
    await expect(page.getByRole('textbox', { name: 'Authoring API base' })).toHaveValue(businessAppOrigin);
    await expect(page.getByText(/<prism-workflow-editor/i)).toBeVisible();
    await expect(page.getByText(`authoring-api-base="${businessAppOrigin}"`)).toBeVisible();
    await expect(page.getByText(/4 workflow definitions discovered/i)).toBeVisible();
    await expect(page.locator('#workflow-key option[value="planning"]')).toContainText('planning');
    await expect(page.locator('#workflow-key option[value="planning"]')).toContainText('planning-application');
    await expect(page.getByRole('alert')).toHaveCount(0);

    // Wait for the workflow data to load before asserting page health. The custom-element host
    // reflects data-prism-workflow-loaded="{key}" after the API fetch completes. On slower CI
    // hardware, the page.goto() 'load' event fires before the JS module executes and the
    // async workflow fetch finishes, causing the heading check to race and timeout.
    //
    // Enhanced diagnostics on failure: captures page state, screenshots, and trace to pinpoint
    // the exact readiness failure mode (module not loaded, fetch not started, fetch failed, etc.).
    try {
      await expect(page.locator('prism-workflow-editor')).toHaveAttribute('data-prism-workflow-loaded', /.+/, {
        timeout: 30_000,
      });
    } catch (e) {
      // Diagnostic: capture the exact state when readiness fails.
      const diagnostics = await page.evaluate(() => {
        const editorElement = document.querySelector('prism-workflow-editor');
        const loadedAttr = editorElement?.getAttribute('data-prism-workflow-loaded') ?? 'element-not-found';
        const bodySnippet = document.body.innerText.substring(0, 500);
        const customElementDefined = !!customElements.get('prism-workflow-editor');
        const moduleScripts = Array.from(document.querySelectorAll('script[type="module"]'))
          .map(s => (s as HTMLScriptElement).src || '(inline)')
          .join(', ');
        return {
          loadedAttr,
          bodySnippet,
          customElementDefined,
          moduleScripts,
          url: window.location.href,
        };
      });
      
      // Capture a screenshot of the failed state for visual inspection.
      await page.screenshot({ 
        path: 'test-results/planning-editor-readiness-failure.png',
        fullPage: true 
      });
      
      console.error('❌ Workflow editor readiness timeout. Diagnostics:', JSON.stringify(diagnostics, null, 2));
      throw new Error(`Workflow editor failed to load within 30s. State: ${JSON.stringify(diagnostics)}`);
    }

    await step(page, '01-workflow-editor-loaded.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-editor-shell"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 2: Graph view shows the planning permission stages in role lanes ──
    // prism-workflow-graph renders in "graph" mode by default (aria role="application").
    // The graph uses horizontal role-first swim lanes, not a generic node field.
    // The live authored planning seed stages are: declaration, application-form,
    // check-answers, submitted.
    const graphCanvas = page.getByRole('application');
    await expect(graphCanvas).toBeVisible({ timeout: 10_000 });
    await expect(graphCanvas).toHaveAttribute('aria-roledescription', /role-first/i);

    // Role lanes should be structurally visible with semantic labels
    await expect(page.locator('[data-prism-role-lane]')).not.toHaveCount(0);

    await expect(graphCanvas.getByText('Declaration')).toBeVisible({ timeout: 10_000 });

    await step(page, '02-graph-view-stages.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-graph"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 3: Click a stage to open the step inspector ─────────────────────
    // Use the graph's keyboard inspector shortcut so the walkthrough follows the
    // accessible selection contract even when surrounding chrome overlaps pointer hits.
    const declarationStage = page.locator('[data-prism-stage="declaration"]');
    await declarationStage.focus();
    await declarationStage.press('e');

    const stepInspector = page.locator('[data-prism-component="step-inspector"]');
    await expect(stepInspector).toBeVisible({ timeout: 10_000 });

    await step(page, '03-step-inspector-open.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="step-inspector"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 4: Step inspector shows stage properties ─────────────────────────
    // The inspector panel lists the stage's display name, kind (Capture/Review/Decision),
    // and the tree of polymorphic form components (section → fieldset → field).
    await expect(stepInspector.getByRole('heading', { name: 'Declaration' })).toBeVisible({ timeout: 5_000 });

    await step(page, '04-step-inspector-properties.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="step-inspector"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 5: Toggle to stage list view ────────────────────────────────────
    // The mode-toggle button ("List view", aria-pressed="false") switches the graph
    // canvas (role="application") to a keyboard-first table of stage rows.
    // This mirrors the keyboard-accessible contract tested in workflow-graph-keyboard.spec.ts.
    const toggleBtn = page.locator('prism-workflow-graph').getByRole('button', { name: 'List view' });
    await expect(toggleBtn).toBeVisible({ timeout: 5_000 });
    await toggleBtn.focus();
    await toggleBtn.press('Enter');

    const stageTable = page.locator('[data-prism-linear-table]');
    await expect(stageTable).toBeVisible({ timeout: 5_000 });

    await step(page, '05-stage-list-view.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-graph"]',
    }), WALKTHROUGH_KEY);

    // Switch back to graph view for the supporting-surface checks.
    const graphToggleBtn = page.locator('prism-workflow-graph').getByRole('button', { name: 'Graph view' });
    await graphToggleBtn.focus();
    await graphToggleBtn.press('Enter');
    await expect(graphCanvas).toBeVisible({ timeout: 5_000 });

    // ─── Step 6: Help opens without an embedded conversation surface ───────────
    // Issue #74 locks the UX: no embedded AI conversation pane in the editor.
    // Supporting tabs for validation, preview, and simulation are allowed, but
    // AI conversation stays external to preserve inspector focus.
    await expect(page.locator('[data-prism-component="conversation-pane"]')).toHaveCount(0);
    const helpButton = page.locator('[data-prism-help]');
    await expect(helpButton).toBeVisible({ timeout: 5_000 });
    await helpButton.focus();
    await helpButton.press('Enter');
    await expect(page.locator('[data-prism-shortcut-dialog]')).toBeVisible({ timeout: 10_000 });

    await step(page, '06-shortcut-guide.png', editorHealthCheck({
      screenshotSelector: '[data-prism-shortcut-dialog]',
    }), WALKTHROUGH_KEY);

    await page.locator('[data-prism-help-close]').click();
    await expect(page.locator('[data-prism-shortcut-dialog]')).toHaveCount(0);

    // ─── Step 7: Validation rail stays attached to the workspace ───────────────
    const validationRail = page.locator('[data-prism-validation-rail]');
    await expect(validationRail).toBeVisible({ timeout: 5_000 });
    await expect(validationRail).toContainText(/validation and save/i);
    await expect(page.locator('[data-prism-save-status]')).toContainText(/save/i);

    await step(page, '07-validation-rail.png', editorHealthCheck({
      screenshotSelector: '[data-prism-validation-rail]',
    }), WALKTHROUGH_KEY);

    // ─── Step 8: Stage preview reflects the selected stage ─────────────────────
    const stagePreview = page.locator('[data-prism-stage-preview]');
    await expect(stagePreview).toBeVisible({ timeout: 10_000 });
    await expect(stagePreview.locator('[data-prism-preview-stage-name]')).toContainText('Declaration', { timeout: 10_000 });

    await step(page, '08-stage-preview.png', editorHealthCheck({
      screenshotSelector: '[data-prism-stage-preview]',
    }), WALKTHROUGH_KEY);

    // ─── Step 9: Simulation starts from the workflow's initial stage ───────────
    const simulationPanel = page.locator('[data-prism-simulation-panel]');
    await expect(simulationPanel).toBeVisible({ timeout: 5_000 });
    await simulationPanel.locator('[data-prism-simulation-start]').click();
    await expect(simulationPanel.locator('[data-prism-simulation-current-stage]')).toContainText('Declaration', { timeout: 10_000 });

    await step(page, '09-simulation-started.png', editorHealthCheck({
      screenshotSelector: '[data-prism-simulation-panel]',
    }), WALKTHROUGH_KEY);
  });
});
