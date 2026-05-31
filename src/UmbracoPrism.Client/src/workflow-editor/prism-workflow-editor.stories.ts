import type { Meta, StoryObj } from '@storybook/web-components';
import { expect, waitFor, within } from '@storybook/test';
import './prism-workflow-editor.js';
import type { PrismWorkflowEditorElement } from './prism-workflow-editor.js';
import { PLANNING_WORKFLOW, LEAVE_REQUEST_STARTER_WORKFLOW, cloneAuthoredWorkflow } from './fixtures/index.js';
import type { AuthoredWorkflow } from './types.js';
import { InMemoryWorkflowSource } from './in-memory-workflow-source.js';
import { withDerivedTransitions } from './workflow-routes.js';

function makeEditor(workflow: AuthoredWorkflow = PLANNING_WORKFLOW): PrismWorkflowEditorElement {
  const el = document.createElement('prism-workflow-editor') as PrismWorkflowEditorElement;
  // Stories drive the editor by injecting the workflow directly. The Save
  // button still needs a `workflowSource` to resolve, so wire an in-memory
  // one seeded with the same workflow — this proves the integrator pattern.
  el.workflowSource = new InMemoryWorkflowSource([workflow]);
  el.workflowKey = workflow.definitionKey;
  el.initialWorkflow = workflow;
  el.style.cssText = 'display: block; width: 1200px; height: 700px;';
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
      kind: 'Question',
      actor: 'reviewer',
      actions: [],
      fields: [],
      roleGates: ['reviewer'],
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
  workflow.gateways = [
    {
      gatewayKey: 'review-decision-routes',
      displayName: 'Review decision routes',
      kind: 'Split',
      source: 'review-decision',
      laneKey: 'reviewer',
      roleGates: [],
      routes: [
        { id: 'review-decision--approve--approved', target: 'approved', trigger: 'approve' },
        { id: 'review-decision--reject--rejected', target: 'rejected', trigger: 'reject' },
        {
          id: 'review-decision--request-more-checks--checks-pending',
          target: 'checks-pending',
          trigger: 'request more checks',
          condition: 'siteVisitRequired == true',
        },
      ],
    },
    {
      gatewayKey: 'declaration-routes',
      displayName: 'Declaration routes',
      kind: 'Split',
      source: 'declaration',
      laneKey: 'applicant',
      roleGates: [],
      routes: [
        { id: 'declaration--continue--application-form', target: 'application-form', trigger: 'continue' },
      ],
    },
    {
      gatewayKey: 'application-form-routes',
      displayName: 'Application form routes',
      kind: 'Split',
      source: 'application-form',
      laneKey: 'applicant',
      roleGates: [],
      routes: [
        { id: 'application-form--submit--review-decision', target: 'review-decision', trigger: 'submit for review' },
      ],
    },
  ];
  return withDerivedTransitions(workflow);
}

function makeSimulationBlockerWorkflow(): AuthoredWorkflow {
  const workflow = makeSimulationBranchWorkflow();
  workflow.displayName = 'Planning Application Simulation Blockers';
  const rejectGateway = (workflow.gateways ?? []).find(g => g.gatewayKey === 'review-decision-routes');
  if (rejectGateway) {
    rejectGateway.routes = (rejectGateway.routes ?? []).map(route =>
      route.trigger === 'reject'
        ? { ...route, target: 'missing-rejection-stage' }
        : route
    );
  }
  return withDerivedTransitions(workflow);
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

    const container = root.querySelector('[data-prism-component="workflow-editor"]');
    await expect(container).not.toBeNull();

    const title = root.querySelector('.editor-title');
    await expect(title?.textContent?.trim()).toBe('Planning Application');

    const graph = root.querySelector('prism-workflow-graph');
    await expect(graph).not.toBeNull();
    await expect(graph?.shadowRoot?.querySelectorAll('[data-prism-role-lane]').length ?? 0).toBeGreaterThan(0);

    const inspector = root.querySelector('prism-step-inspector');
    await expect(inspector).not.toBeNull();

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

export const GatewayRepresentation: Story = {
  name: 'Gateway Representation',
  render: () => makeEditor(cloneAuthoredWorkflow(LEAVE_REQUEST_STARTER_WORKFLOW)),
};
