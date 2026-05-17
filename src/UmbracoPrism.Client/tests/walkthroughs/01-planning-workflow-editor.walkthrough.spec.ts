// Executable counterpart of docs/walkthroughs/planning-workflow-editor.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import {
  step,
  signIn,
  resetWorkflows,
  businessAppOrigin,
  type PageHealthCheck,
} from './support/walkthrough';

const appHost = new LiveAppHost();
const WALKTHROUGH_KEY = 'planning-workflow-editor';

// URL pattern for the workflow editor SPA — stays constant across all steps.
// `workflow-editor.html` is served by MockBusinessApp (Isabelle's Wave 1 foundation deliverable).
const editorUrl = /workflow-editor\.html/;

// Reusable health check: all steps take place on the same SPA page.
function editorHealthCheck(override: Partial<PageHealthCheck> = {}): PageHealthCheck {
  return {
    url: editorUrl,
    heading: /planning permission/i,
    bodyMustNotContain: /\b(404|Not Found|An error occurred|Server Error)\b/i,
    ...override,
  };
}

test.describe('Planning Workflow Editor walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await resetWorkflows(request);
  });

  // ---------------------------------------------------------------------------
  // SKIP RATIONALE
  //
  // This walkthrough depends on two Wave 1 foundation deliverables that have not
  // yet landed:
  //   • Isabelle — `workflow-editor.html` served by MockBusinessApp with
  //                `<prism-workflow-graph>`, `<prism-conversation-pane>`, and
  //                `<prism-step-inspector>` wired to the planning workflow.
  //   • Blathers — `/api/workflow-authoring/planning-permission` (GET + PATCH)
  //                endpoints that load and persist the workflow definition.
  //
  // When both PRs have merged:
  //   1. Remove the `test.skip(true, ...)` line.
  //   2. Run with `CAPTURE_SCREENSHOTS=1` against the localhost-auth config to
  //      populate docs/images/walkthroughs/planning-workflow-editor/.
  //   3. Commit the screenshots and update this spec if any selectors drift.
  //
  // See SKILL.md R6 for the screenshot capture workflow.
  // ---------------------------------------------------------------------------

  test('happy path: authoring a planning permission workflow with natural language', async ({ page }) => {
    await signIn(page);

    // ─── Step 1: Load the workflow editor ──────────────────────────────────────
    // The editor is served at /workflow-editor.html?workflow=planning.
    // MockBusinessApp (Isabelle's deliverable) resolves "planning" → the seeded
    // planning-permission workflow definition and calls GET /api/workflow-authoring/planning-permission.
    await page.goto(`${businessAppOrigin}/workflow-editor.html?workflow=planning`);

    // Wait for the workflow data to load before asserting page health. The custom element
    // sets data-prism-workflow-loaded="{key}" after the API fetch completes. On slower CI
    // hardware, the page.goto() 'load' event fires before the JS module executes and the
    // async workflow fetch finishes, causing the heading check to race and timeout.
    //
    // Enhanced diagnostics on failure: captures page state, screenshots, and trace to pinpoint
    // the exact readiness failure mode (module not loaded, fetch not started, fetch failed, etc.).
    try {
      await page.waitForSelector('[data-prism-workflow-loaded]:not([data-prism-workflow-loaded=""])', {
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
      screenshotSelector: '[data-prism-component="workflow-graph"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 2: Graph view shows the planning permission stages ───────────────
    // prism-workflow-graph renders in "graph" mode by default (aria role="application").
    // The STUB_WORKFLOW stages are: applicant-details, check-answers, waiting-for-review,
    // reviewer-assessment, confirmation.
    const graphCanvas = page.getByRole('application');
    await expect(graphCanvas).toBeVisible({ timeout: 10_000 });

    // Each stage node has data-prism-stage="{stageKey}" in shadow DOM.
    // "applicant-details" is the initial stage (first node in the graph).
    await expect(page.locator('[data-prism-stage="applicant-details"]')).toBeVisible({ timeout: 10_000 });

    await step(page, '02-graph-view-stages.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-graph"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 3: Click a stage to open the step inspector ─────────────────────
    // Clicking the "applicant-details" node dispatches a stage-selected CustomEvent.
    // prism-step-inspector renders on the right with the stage's fields/components.
    await page.locator('[data-prism-stage="applicant-details"]').click();

    const stepInspector = page.locator('[data-prism-component="step-inspector"]');
    await expect(stepInspector).toBeVisible({ timeout: 10_000 });

    await step(page, '03-step-inspector-open.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="step-inspector"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 4: Step inspector shows stage properties ─────────────────────────
    // The inspector panel lists the stage's display name, kind (Capture/Review/Decision),
    // and the tree of polymorphic form components (section → fieldset → field).
    // data-prism-stage-detail="applicant-details" is set on the inspector root.
    await expect(page.locator('[data-prism-stage-detail="applicant-details"]')).toBeVisible({ timeout: 5_000 });

    await step(page, '04-step-inspector-properties.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="step-inspector"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 5: Toggle to stage list view ────────────────────────────────────
    // The mode-toggle button ("List view", aria-pressed="false") switches the graph
    // canvas (role="application") to a linear list (role="listbox") of stage cards.
    // This mirrors the keyboard-accessible contract tested in workflow-graph-keyboard.spec.ts.
    const toggleBtn = page.locator('prism-workflow-graph').getByRole('button', { name: 'List view' });
    await expect(toggleBtn).toBeVisible({ timeout: 5_000 });
    await toggleBtn.click();

    const stageList = page.getByRole('listbox');
    await expect(stageList).toBeVisible({ timeout: 5_000 });

    await step(page, '05-stage-list-view.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-graph"]',
    }), WALKTHROUGH_KEY);

    // Switch back to graph view so the conversation pane step is clearer
    await page.locator('prism-workflow-graph').getByRole('button', { name: 'Graph view' }).click();
    await expect(graphCanvas).toBeVisible({ timeout: 5_000 });

    // ─── Step 6: Type a natural language change request ────────────────────────
    // prism-conversation-pane exposes data-prism-conversation-input for the textarea.
    // Getters pierce shadow DOM; getByRole('textbox') finds the textarea inside shadow DOM.
    const conversationPane = page.locator('[data-prism-component="conversation-pane"]');
    await expect(conversationPane).toBeVisible({ timeout: 10_000 });

    const nlInput = page.getByRole('textbox');
    await expect(nlInput).toBeVisible({ timeout: 5_000 });
    await nlInput.fill('Add an identity verification step before the reviewer assessment stage');

    await step(page, '06-nl-request-typed.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="conversation-pane"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 7: Submit and receive a proposal diff ────────────────────────────
    // The Send button POSTs to /api/workflow-authoring/planning-permission/proposals.
    // Blathers' endpoint responds with an AuthoringProposal envelope.
    // prism-proposal-diff renders inside the conversation thread.
    const nlRequestInflight = page.waitForRequest(
      req =>
        req.url().includes('/api/workflow-authoring/workflows/planning/preview') &&
        req.method() === 'POST'
    );

    await page.getByRole('button', { name: /send/i }).click();
    await nlRequestInflight;

    const proposalDiff = page.locator('[data-prism-component="proposal-diff"]');
    await expect(proposalDiff).toBeVisible({ timeout: 15_000 });

    await step(page, '07-proposal-diff.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="conversation-pane"]',
    }), WALKTHROUGH_KEY);

    // ─── Step 8: Accept the proposal ──────────────────────────────────────────
    // "Accept all" PATCHes /api/workflow-authoring/planning-permission with the updated
    // definition. The workflow graph refreshes; the new ID&V stage appears.
    // Validation status "pass" is required for Accept all to be enabled (STUB_PROPOSAL).
    const acceptBtn = page.getByRole('button', { name: /accept all/i });
    await expect(acceptBtn).toBeEnabled({ timeout: 5_000 });

    const applyRequest = page.waitForRequest(
      req =>
        req.url().includes('/api/workflow-authoring/workflows/planning/apply') &&
        req.method() === 'POST'
    );

    await acceptBtn.click();
    await applyRequest;

    // ─── Step 9: Workflow graph reflects the applied change ────────────────────
    // After apply, prism-workflow-graph re-renders with the updated definition.
    // The ID&V stage (injected by the agent) must now appear as a node.
    await expect(page.locator('[data-prism-stage="id-verification"]')).toBeVisible({ timeout: 10_000 });

    await step(page, '09-proposal-applied.png', editorHealthCheck({
      screenshotSelector: '[data-prism-component="workflow-graph"]',
    }), WALKTHROUGH_KEY);
  });
});
