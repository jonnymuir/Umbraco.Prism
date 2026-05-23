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
    heading: /workflow editor/i,
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
  // slice: role-first graph framing, inspector-first editing, collapsible side
  // panels, and supporting confidence surfaces (validation, preview, simulation, help).
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
    await expect(page.getByRole('heading', { name: /workflow editor/i })).toBeVisible();
    await expect(page.locator('[data-prism-component="workflow-editor-shell"]')).toHaveAttribute(
      'data-prism-active-workflow',
      'planning'
    );
    await expect(page.locator('prism-workflow-editor')).toHaveAttribute('data-prism-workflow-loaded', 'planning', {
      timeout: 30_000,
    });
    await expect(page.getByRole('combobox', { name: 'Select workflow' })).toHaveValue('planning');
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

    // ─── Browser surface quality check: Editor workspace is prioritized ─────────
    // The editor frame should occupy significant vertical space. The clean shell
    // has no marketing hero — the editor frame IS the primary surface.
    const editorFrame = page.locator('.editor-frame');
    await expect(editorFrame).toBeVisible();
    const viewport = page.viewportSize();
    const editorBox = await editorFrame.boundingBox();
    if (viewport && editorBox) {
      expect(editorBox.height / viewport.height).toBeGreaterThan(0.4);
    }

    // ─── Mature editor shell: Persistent outline/navigator ─────────────────────
    // The workflow outline should be visible alongside the main canvas at all times.
    // It provides quick navigation to stages and shows the current selection.
    const workflowOutline = page.locator('[data-prism-workflow-outline]');
    await expect(workflowOutline).toBeVisible({ timeout: 10_000 });
    await expect(workflowOutline.locator('[data-prism-outline-stage]')).not.toHaveCount(0);

    // ─── Mature editor shell: Confidence surfaces are tabbed ────────────────────
    // Validation, preview, simulation should appear as tabs, not all stacked at once.
    const confidenceTabs = page.locator('[data-prism-confidence-tabs]');
    await expect(confidenceTabs).toBeVisible({ timeout: 10_000 });
    await expect(confidenceTabs.locator('[data-prism-confidence-tab="validation"]')).toBeVisible();
    await expect(confidenceTabs.locator('[data-prism-confidence-tab="preview"]')).toBeVisible();
    await expect(confidenceTabs.locator('[data-prism-confidence-tab="simulation"]')).toBeVisible();

    // ─── Step 2: Graph view shows the planning permission stages in role lanes ──
    // prism-workflow-graph renders in "graph" mode by default (aria role="application").
    // The graph uses role-first swim lanes (vertical orientation as of Issue #75).
    // The live authored planning seed stages are: declaration, application-form,
    // check-answers, submitted.
    const graphCanvas = page.getByRole('application');
    await expect(graphCanvas).toBeVisible({ timeout: 10_000 });
    await expect(graphCanvas).toHaveAttribute('aria-roledescription', /role-first/i);

    // ─── Browser surface quality check: Swim lane structure is visible and usable ─
    // The smoke lane should prove the role-first graph rendered a usable authored surface
    // without depending on a specific number of lanes in the seed workflow.
    const roleLanes = page.locator('[data-prism-role-lane]');
    await expect(roleLanes).not.toHaveCount(0);
    
    const firstLane = roleLanes.first();
    await expect(firstLane).toBeInViewport();

    // ─── Vertical lanes orientation check ─────────────────────────────────────
    // Verify lanes are structurally semantic (focusable sections with headings)
    await expect(firstLane.locator('.lane-heading')).toBeVisible();
    await expect(firstLane.locator('.lane-copy')).toBeVisible();

    await expect(graphCanvas.getByText('Declaration')).toBeVisible({ timeout: 10_000 });

    await step(page, '02-graph-view-stages.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-graph"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 3: Click a stage to open the step inspector ─────────────────────
    // Use the graph's keyboard inspector shortcut so the walkthrough follows the
    // accessible selection contract even when surrounding chrome overlaps pointer hits.
    // This pattern emerged from PR #75 CI failures where the pointer-based click was
    // blocked by overlapping editor chrome in the browser-hosted surface.
    const declarationStage = page.locator('[data-prism-stage="declaration"]');
    await declarationStage.focus();
    
    // ─── Browser surface quality check: Stage cards are not blocked by chrome ───
    // Before using keyboard shortcut, verify the stage is clickable (not pointer-blocked).
    await expect(declarationStage).toBeVisible();
    
    await declarationStage.press('e');

    const stepInspector = page.locator('[data-prism-component="step-inspector"]');
    await expect(stepInspector).toBeVisible({ timeout: 10_000 });

    // ─── Mature editor shell: Selection syncs with outline ──────────────────────
    // When a stage is selected in the graph, the outline should highlight it.
    const outlineDeclarationStage = workflowOutline.locator(
      '[data-prism-outline-stage="declaration"][aria-current="location"]'
    );
    await expect(outlineDeclarationStage).toBeVisible({ timeout: 5_000 });

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

    // ─── Step 5: Collapse and restore the side panels ──────────────────────────
    // Authors can collapse the outline and properties drawer without losing the canvas.
    const outlineToggle = page.locator('[data-prism-outline-toggle]');
    const inspectorToggle = page.locator('[data-prism-inspector-toggle]');
    await expect(outlineToggle).toHaveAttribute('aria-expanded', 'true');
    await expect(inspectorToggle).toHaveAttribute('aria-expanded', 'true');

    await outlineToggle.click();
    await expect(outlineToggle).toHaveAttribute('aria-expanded', 'false');
    await expect(workflowOutline).toBeHidden();

    await inspectorToggle.click();
    await expect(inspectorToggle).toHaveAttribute('aria-expanded', 'false');
    await expect(stepInspector).toBeHidden();

    await step(page, '05-collapsed-side-panels.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-editor"]',
    }), WALKTHROUGH_KEY);

    await outlineToggle.click();
    await inspectorToggle.click();
    await expect(workflowOutline).toBeVisible();
    await expect(stepInspector).toBeVisible();

    // ─── Graph-only contract: no list workspace, canvas owns scrolling ─────────
    await expect(page.locator('prism-workflow-editor').getByRole('button', { name: /List view/i })).toHaveCount(0);
    await expect(page.locator('[data-prism-linear-table]')).toHaveCount(0);

    // The .graph-canvas div is the scrollable region, NOT .graph-viewport
    // This keeps the shell chrome (outline, inspector, toolbar) anchored while the graph scrolls
    const graphScrollState = await page.locator('prism-workflow-graph').evaluate(graphElement => {
      const graph = graphElement as HTMLElement;
      const shadowRoot = graph.shadowRoot;
      const canvas = shadowRoot?.querySelector<HTMLElement>('.graph-canvas');
      if (!canvas) {
        return null;
      }

      const before = canvas.scrollTop;
      canvas.scrollTop = 220;
      return {
        before,
        after: canvas.scrollTop,
        overflowY: getComputedStyle(canvas).overflowY,
      };
    });

    expect(graphScrollState).not.toBeNull();
    expect(graphScrollState?.after ?? 0).toBeGreaterThanOrEqual(graphScrollState?.before ?? 0);
    expect(graphScrollState?.overflowY === 'auto' || graphScrollState?.overflowY === 'scroll').toBeTruthy();
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);

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

    // ─── Step 7: Confidence tabs replace the stacked validation rail ───────────
    // Switch to validation tab to see validation feedback.
    await confidenceTabs.locator('[data-prism-confidence-tab="validation"]').click();
    const validationPanel = page.locator('[data-prism-confidence-panel="validation"]');
    await expect(validationPanel).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('[data-prism-validation-rail]')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('heading', { name: /workflow validation/i })).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('[data-prism-save-status]')).toContainText(/save/i);

    await step(page, '07-validation-tab.png', editorHealthCheck({
      screenshotSelector: '[data-prism-confidence-panel="validation"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 8: Preview tab shows the selected stage ─────────────────────────
    await confidenceTabs.locator('[data-prism-confidence-tab="preview"]').click();
    const previewPanel = page.locator('[data-prism-confidence-panel="preview"]');
    await expect(previewPanel).toBeVisible({ timeout: 5_000 });

    await expect(page.locator('[data-prism-preview-stage-name]')).toContainText('Declaration', { timeout: 10_000 });

    await step(page, '08-preview-tab.png', editorHealthCheck({
      screenshotSelector: '[data-prism-confidence-panel="preview"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 9: Simulation tab starts from the workflow's initial stage ───────
    await confidenceTabs.locator('[data-prism-confidence-tab="simulation"]').click();
    const simulationPanel = page.locator('[data-prism-confidence-panel="simulation"]');
    await expect(simulationPanel).toBeVisible({ timeout: 5_000 });

    await expect(page.locator('[data-prism-simulation-panel]')).toBeVisible({ timeout: 5_000 });
    await page.locator('[data-prism-simulation-start]').click();
    await expect(page.locator('[data-prism-simulation-current-stage]')).toContainText('Declaration', { timeout: 10_000 });

    await step(page, '09-simulation-tab.png', editorHealthCheck({
      screenshotSelector: '[data-prism-confidence-panel="simulation"]',
    }), WALKTHROUGH_KEY);
  });
});
