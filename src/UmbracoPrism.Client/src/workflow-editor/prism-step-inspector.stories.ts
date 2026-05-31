import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-step-inspector.js';
import type { PrismStepInspectorElement } from './prism-step-inspector.js';
import { STUB_ACTION_CATALOG, STUB_WORKFLOW } from './types.js';
import type { ActionCatalogEntry, AuthoredWorkflow } from './types.js';

type StoryArgs = {
  workflow: AuthoredWorkflow | null;
  selectedStageKey: string | null;
  selectedGatewayKey?: string | null;
  actionCatalog: ActionCatalogEntry[];
};

function makeElement(args: StoryArgs): PrismStepInspectorElement {
  const el = document.createElement('prism-step-inspector') as PrismStepInspectorElement;
  el.workflow = args.workflow;
  el.selectedStageKey = args.selectedStageKey;
  el.selectedGatewayKey = args.selectedGatewayKey ?? null;
  el.actionCatalog = args.actionCatalog;
  el.addEventListener('workflow-updated', event => {
    const detail = (event as CustomEvent<{
      workflow: AuthoredWorkflow;
      selection?: { kind?: 'stage' | 'gateway'; stageKey?: string; gatewayKey?: string } | null;
    }>).detail;
    el.workflow = detail.workflow;
    if (detail.selection?.kind === 'gateway') {
      el.selectedGatewayKey = detail.selection.gatewayKey ?? null;
      el.selectedStageKey = null;
    } else if (detail.selection?.stageKey) {
      el.selectedStageKey = detail.selection.stageKey;
      el.selectedGatewayKey = null;
    } else {
      el.selectedStageKey = null;
      el.selectedGatewayKey = null;
    }
  });
  el.style.cssText = 'display:block;width:380px;height:640px;';
  return el;
}

const meta: Meta<StoryArgs> = {
  title: 'Workflow Editor/Step Inspector',
  component: 'prism-step-inspector',
  tags: ['autodocs'],
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'heading-order', enabled: true },
        ],
      },
    },
  },
  args: {
    workflow: null,
    selectedStageKey: null,
    selectedGatewayKey: null,
    actionCatalog: STUB_ACTION_CATALOG,
  },
  render: args => makeElement(args),
};

export default meta;
type Story = StoryObj<StoryArgs>;

export const Empty: Story = {
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 100));
    const el = canvasElement.querySelector('prism-step-inspector') as PrismStepInspectorElement;
    await el.updateComplete;
    await expect(el.shadowRoot?.querySelector('.empty-state')).not.toBeNull();
  },
};

export const EditableStage: Story = {
  args: {
    workflow: STUB_WORKFLOW,
    selectedStageKey: 'reviewer-assessment',
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 120));
    const el = canvasElement.querySelector('prism-step-inspector') as PrismStepInspectorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const title = root.querySelector<HTMLInputElement>('[data-prism-stage-title]')!;
    title.value = 'Applicant Intake';
    title.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const lane = root.querySelector<HTMLInputElement>('[data-prism-stage-lane]')!;
    lane.value = 'member';
    lane.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const stageType = root.querySelector<HTMLSelectElement>('[data-prism-stage-type]')!;
    stageType.value = 'review';
    stageType.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const actionEditor = root.querySelector('prism-workflow-action-editor')!;
    await expect(actionEditor).not.toBeNull();
    await expect(actionEditor.shadowRoot?.querySelectorAll('[data-prism-stage-action]').length).toBe(2);
    await expect(actionEditor.shadowRoot?.querySelector('[data-prism-action-forms-editor="1"]')).not.toBeNull();
    await expect(root.querySelector('[data-prism-stage-detail="reviewer-assessment"]')).not.toBeNull();
  },
};

export const ActionConfiguration: Story = {
  args: {
    workflow: STUB_WORKFLOW,
    selectedStageKey: 'reviewer-assessment',
  },
};

// A small gateway-shaped workflow so the inspector can render the new
// outgoing-routes section with a single route whose action editor mirrors
// the previous transition-action picker scope.
const GATEWAY_ROUTE_WORKFLOW: AuthoredWorkflow = {
  definitionKey: 'gateway-route-action-fixture',
  displayName: 'Gateway route action fixture',
  version: 1,
  schemaVersion: '1.0',
  instancePolicy: 'single',
  initialStageKey: 'submitted',
  stages: [
    {
      stageKey: 'submitted',
      displayName: 'Submitted',
      kind: 'Question',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stageKey: 'reviewer-assessment',
      displayName: 'Reviewer assessment',
      kind: 'Question',
      actor: 'reviewer',
      actions: [],
      components: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'applicant-amendments',
      displayName: 'Applicant amendments',
      kind: 'Question',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
  ],
  gateways: [
    {
      gatewayKey: 'review-split',
      displayName: 'Review split',
      kind: 'Split',
      laneKey: 'public',
      actor: 'public',
      source: 'submitted',
      roleGates: [],
      routes: [
        {
          id: 'submitted--route-for-review--reviewer-assessment',
          target: 'reviewer-assessment',
          trigger: 'route for review',
          requiresRole: 'reviewer',
          actions: [
            {
              type: 'forms.submit',
              timing: 'OnTransition',
            },
          ],
        },
      ],
    },
  ],
};

export const TransitionSelected: Story = {
  // Slice 3b.1 removed transition-only selection. Route editing now lives in
  // the gateway inspector — this story mounts a split gateway with two routes
  // so the editor-host gateway-route specs have a backing fixture.
  args: {
    workflow: GATEWAY_ROUTE_WORKFLOW,
    selectedStageKey: null,
    selectedGatewayKey: 'review-split',
  },
};

export const TransitionActionConfiguration: Story = {
  // The previous transition-action picker filter check now runs against a
  // route action editor mounted inside the gateway inspector's outgoing
  // routes panel. The action-editor data attributes (data-prism-action-*)
  // are identical so existing tests keep working.
  args: {
    workflow: GATEWAY_ROUTE_WORKFLOW,
    selectedStageKey: null,
    selectedGatewayKey: 'review-split',
  },
};

// Stage with no gateway — "+ Add route" button must be visible and
// must create the gateway on click. The `applicant-amendments` stage in
// GATEWAY_ROUTE_WORKFLOW has no Split gateway attached.
export const AddRouteNoGateway: Story = {
  args: {
    workflow: GATEWAY_ROUTE_WORKFLOW,
    selectedStageKey: 'applicant-amendments',
    selectedGatewayKey: null,
  },
};

// Gateway that already has one route — "+ Add route" button must be
// visible and must append a second route on click.
export const AddRouteExistingGateway: Story = {
  args: {
    workflow: GATEWAY_ROUTE_WORKFLOW,
    selectedStageKey: null,
    selectedGatewayKey: 'review-split',
  },
};
