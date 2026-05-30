import type { Meta, StoryObj } from '@storybook/web-components';
import { expect, waitFor, within } from '@storybook/test';
import './prism-workflow-editor.js';
import type { PrismWorkflowEditorElement } from './prism-workflow-editor.js';
import { PLANNING_WORKFLOW } from './fixtures/index.js';
import { STUB_ACTION_CATALOG, type AuthoredWorkflow } from './types.js';
import { projectWorkflowLocally } from './workflow-runtime-projection.js';

/**
 * Stubs window.fetch for authoring API URLs so stories work fully offline.
 * Called from each story's render function; the original fetch is restored
 * shortly after to avoid cross-story contamination.
 */
function stubFetchFor(el: PrismWorkflowEditorElement): void {
  const originalFetch = window.fetch;
  const WORKFLOW_API_RE = /\/api\/workflow-authoring\/workflows/;
  const ACTION_CATALOG_RE = /\/api\/workflow-authoring\/action-catalog/;

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const urlStr =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : (input as Request).url;
    if (ACTION_CATALOG_RE.test(urlStr)) {
      return new Response(JSON.stringify(STUB_ACTION_CATALOG), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }
    if (WORKFLOW_API_RE.test(urlStr)) {
      const method = (init?.method ?? 'GET').toUpperCase();
      if (method === 'GET')
        return new Response(JSON.stringify(PLANNING_WORKFLOW), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      if (method === 'POST') {
        const body = init?.body ? JSON.parse(init.body as string) : {};
        if (urlStr.endsWith('/project')) {
          return new Response(JSON.stringify(projectWorkflowLocally(body as AuthoredWorkflow)), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          });
        }
        return new Response(JSON.stringify(body), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      return new Response(null, { status: 204 });
    }
    return originalFetch(input, init);
  };

  // Restore after story element is removed from the DOM
  const observer = new MutationObserver(() => {
    if (!document.contains(el)) {
      window.fetch = originalFetch;
      observer.disconnect();
    }
  });
  observer.observe(document.body, { childList: true, subtree: true });
}

function makeEditor(workflow: AuthoredWorkflow = PLANNING_WORKFLOW): PrismWorkflowEditorElement {
  const el = document.createElement('prism-workflow-editor') as PrismWorkflowEditorElement;
  // Inject the fixture directly — no API fetch needed
  el.initialWorkflow = workflow;
  el.workflowKey = workflow.definitionKey;
  el.style.cssText = 'display: block; width: 1200px; height: 700px;';
  // Also stub fetch so preview/apply calls work offline if triggered
  stubFetchFor(el);
  return el;
}

function makeEmptyWorkflow(): AuthoredWorkflow {
  const workflow = JSON.parse(JSON.stringify(PLANNING_WORKFLOW)) as AuthoredWorkflow;
  return {
    ...workflow,
    displayName: 'Empty Workflow',
    initialStageKey: '',
    stages: [],
    transitions: [],
    gateways: [],
  };
}

function makeSimulationBranchWorkflow(): AuthoredWorkflow {
  const workflow = JSON.parse(JSON.stringify(PLANNING_WORKFLOW)) as AuthoredWorkflow;
  workflow.displayName = 'Planning Application Simulation';
  workflow.stages = [
    workflow.stages[0],
    workflow.stages[1],
    {
      stageKey: 'review-decision',
      displayName: 'Reviewer decision',
      description: 'Reviewer chooses whether to approve, reject, or request more checks.',
      kind: 'TaskList',
      actor: 'reviewer',
      actions: [],
      fields: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'checks-pending',
      displayName: 'Checks pending',
      description: 'The application is paused while further checks run.',
      kind: 'Waiting',
      actor: 'reviewer',
      actions: [],
      fields: [],
      roleGates: ['reviewer'],
      waiting: {
        allowDefer: false,
        content: 'Additional planning checks are running before the application can progress.',
      },
    },
    {
      stageKey: 'approved',
      displayName: 'Application approved',
      description: 'The application has been approved.',
      kind: 'Confirmation',
      actor: 'reviewer',
      actions: [],
      fields: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'rejected',
      displayName: 'Application rejected',
      description: 'The application has been rejected.',
      kind: 'Confirmation',
      actor: 'reviewer',
      actions: [],
      fields: [],
      roleGates: ['reviewer'],
    },
  ];
  workflow.transitions = [
    { fromStage: 'declaration', toStage: 'application-form', action: 'continue', actions: [] },
    { fromStage: 'application-form', toStage: 'review-decision', action: 'submit for review', actions: [] },
    { fromStage: 'review-decision', toStage: 'approved', action: 'approve', actions: [] },
    { fromStage: 'review-decision', toStage: 'rejected', action: 'reject', actions: [] },
    {
      fromStage: 'review-decision',
      toStage: 'checks-pending',
      action: 'request more checks',
      condition: 'siteVisitRequired == true',
      actions: [],
    },
  ];
  return workflow;
}

function makeSimulationBlockerWorkflow(): AuthoredWorkflow {
  const workflow = makeSimulationBranchWorkflow();
  workflow.displayName = 'Planning Application Simulation Blockers';
  workflow.transitions = workflow.transitions.map(transition =>
    transition.action === 'reject'
      ? { ...transition, toStage: 'missing-rejection-stage' }
      : transition
  );
  return workflow;
}

const meta: Meta = {
  title: 'Workflow Editor/Editor Host',
  component: 'prism-workflow-editor',
  tags: ['autodocs'],
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-required-children', enabled: true },
          { id: 'aria-dialog-name', enabled: true },
        ],
      },
    },
    layout: 'fullscreen',
  },
  render: () => makeEditor(),
};

export default meta;
type Story = StoryObj;

export const PlanningWorkflow: Story = {
  name: 'Planning Workflow',
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-workflow-editor') as PrismWorkflowEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;

    // Root container is present with correct test hooks
    const container = root.querySelector('[data-prism-component="workflow-editor"]');
    await expect(container).not.toBeNull();

    // Workflow name appears in the header
    const title = root.querySelector('.editor-title');
    await expect(title?.textContent?.trim()).toBe('Planning Application');

    // Graph panel is rendered
    const graph = root.querySelector('prism-workflow-graph');
    await expect(graph).not.toBeNull();
    await expect(graph?.shadowRoot?.querySelectorAll('[data-prism-role-lane]').length ?? 0).toBeGreaterThan(0);

    // Inspector panel is rendered
    const inspector = root.querySelector('prism-step-inspector');
    await expect(inspector).not.toBeNull();

    // Modal is NOT open by default
    const backdrop = root.querySelector('.modal-backdrop');
    await expect(backdrop).toBeNull();
  },
};

export const WithStageSelected: Story = {
  name: 'Stage Selected',
  render: () => makeEditor(),
  play: async ({ canvasElement }) => {
    const el = canvasElement.querySelector('prism-workflow-editor') as PrismWorkflowEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const graph = root.querySelector('prism-workflow-graph');
    const inspector = root.querySelector('prism-step-inspector');
    await expect(graph).not.toBeNull();
    await expect(inspector).not.toBeNull();

    const graphCanvas = within(graph!.shadowRoot as unknown as HTMLElement);
    const declarationStage = graphCanvas.getByRole('button', { name: 'Declaration, Applicant lane' }) as HTMLButtonElement;
    declarationStage.click();

    await waitFor(() =>
      expect(
        root
          .querySelector('prism-stage-preview')
          ?.shadowRoot
          ?.querySelector('[data-prism-preview-stage-name]')
          ?.textContent
          ?.trim()
      ).toBe('Declaration')
    );
  },
};

export const EmptyWorkflow: Story = {
  name: 'Empty Workflow',
  render: () => {
    const el = makeEditor();
    el.initialWorkflow = makeEmptyWorkflow();
    return el;
  },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-workflow-editor') as PrismWorkflowEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const graph = root.querySelector('prism-workflow-graph');
    await expect(graph).not.toBeNull();
    await expect(graph?.shadowRoot?.querySelector('[data-prism-empty-state="graph"]')).not.toBeNull();

    const helpButton = root.querySelector<HTMLElement>('[data-prism-help]');
    helpButton?.click();
    await new Promise(r => setTimeout(r, 50));
    await expect(root.querySelector('[data-prism-shortcut-dialog]')).not.toBeNull();
  },
};

export const SimulationBranches: Story = {
  name: 'Simulation Branches',
  render: () => makeEditor(makeSimulationBranchWorkflow()),
};

export const SimulationBlockers: Story = {
  name: 'Simulation Blockers',
  render: () => makeEditor(makeSimulationBlockerWorkflow()),
};

// NOTE: `GatewayRepresentation` story removed in Slice 1.5 — it required the
// LEAVE_REQUEST_STARTER_WORKFLOW fixture (gateway-shaped) which lives with the
// Slice 5 canvas/slot-matrix work. Reinstate alongside that slice.
