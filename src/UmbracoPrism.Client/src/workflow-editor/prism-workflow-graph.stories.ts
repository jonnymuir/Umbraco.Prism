import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-workflow-graph.js';
import type { PrismWorkflowGraphElement } from './prism-workflow-graph.js';
import { STUB_WORKFLOW } from './types.js';
import type { AuthoredWorkflow } from './types.js';

const WORKSPACE_WORKFLOW: AuthoredWorkflow = {
  ...STUB_WORKFLOW,
  transitions: [...STUB_WORKFLOW.transitions],
};

const GATEWAY_WORKFLOW: AuthoredWorkflow = {
  ...STUB_WORKFLOW,
  displayName: 'Planning Permission Gateway Draft',
  initialStageKey: 'draft',
  stages: [
    {
      stageKey: 'draft',
      displayName: 'Draft submission',
      description: 'Capture the initial applicant draft before review starts.',
      kind: 'Question',
      actor: 'public',
      actions: [],
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'applicant-amendments',
      displayName: 'Applicant amendments',
      description: 'Applicant lane work after the split.',
      kind: 'Question',
      actor: 'public',
      actions: [],
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'reviewer-assessment',
      displayName: 'Reviewer assessment',
      description: 'Reviewer lane work after the split.',
      kind: 'Question',
      actor: 'reviewer',
      actions: [],
      fields: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'decision-confirmed',
      displayName: 'Decision confirmed',
      description: 'The merged path continues here for the authored executable route.',
      kind: 'Confirmation',
      actor: 'public',
      actions: [],
      fields: [],
      roleGates: [],
    },
  ],
  transitions: [
    { fromStage: 'draft', toStage: 'applicant-amendments', action: 'continue applicant branch', actions: [] },
    { fromStage: 'draft', toStage: 'reviewer-assessment', action: 'start reviewer branch', actions: [] },
    { fromStage: 'applicant-amendments', toStage: 'decision-confirmed', action: 'complete applicant branch', actions: [] },
    { fromStage: 'reviewer-assessment', toStage: 'decision-confirmed', action: 'approve decision', requiresRole: 'reviewer', actions: [] },
  ],
  gateways: [
    {
      gatewayKey: 'review-split',
      displayName: 'Review split',
      kind: 'Split',
      laneKey: 'public',
      actor: 'public',
      roleGates: [],
    },
    {
      gatewayKey: 'decision-join',
      displayName: 'Decision join',
      kind: 'Join',
      laneKey: 'public',
      actor: 'public',
      roleGates: [],
    },
  ],
};

type StoryArgs = {
  workflow: AuthoredWorkflow | null;
};

function makeElement(args: StoryArgs): PrismWorkflowGraphElement {
  const el = document.createElement('prism-workflow-graph') as PrismWorkflowGraphElement;
  el.workflow = args.workflow;
  el.style.cssText = 'display:block;height:560px;';
  return el;
}

function fillCreateStageDialog(root: ShadowRoot, name: string, key: string, lane: string, type: string) {
  const nameInput = root.querySelector<HTMLInputElement>('[data-prism-create-stage-title]')!;
  nameInput.value = name;
  nameInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));

  const keyInput = root.querySelector<HTMLInputElement>('[data-prism-create-stage-key]')!;
  keyInput.value = key;
  keyInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));

  const laneInput = root.querySelector<HTMLInputElement>('[data-prism-create-stage-lane]')!;
  laneInput.value = lane;
  laneInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));

  const typeSelect = root.querySelector<HTMLSelectElement>('[data-prism-create-stage-type]')!;
  typeSelect.value = type;
  typeSelect.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
}

function fillCreateTransitionDialog(root: ShadowRoot, label: string, targetStageKey: string, conditionMode: string, conditionValue: string) {
  const labelInput = root.querySelector<HTMLInputElement>('[data-prism-create-transition-label]')!;
  labelInput.value = label;
  labelInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));

  const targetSelect = root.querySelector<HTMLSelectElement>('[data-prism-create-transition-target]')!;
  targetSelect.value = targetStageKey;
  targetSelect.dispatchEvent(new Event('change', { bubbles: true, composed: true }));

  const conditionModeSelect = root.querySelector<HTMLSelectElement>('[data-prism-create-transition-condition-mode]')!;
  conditionModeSelect.value = conditionMode;
  conditionModeSelect.dispatchEvent(new Event('change', { bubbles: true, composed: true }));

  if (conditionMode !== 'always') {
    const conditionValueInput = root.querySelector<HTMLInputElement>('[data-prism-create-transition-condition-value]')!;
    conditionValueInput.value = conditionValue;
    conditionValueInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
  }
}

const meta: Meta<StoryArgs> = {
  title: 'Workflow Editor/Workflow Graph',
  component: 'prism-workflow-graph',
  tags: ['autodocs'],
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-required-children', enabled: true },
        ],
      },
    },
  },
  args: {
    workflow: null,
  },
  render: args => makeElement(args),
};

export default meta;
type Story = StoryObj<StoryArgs>;

export const Empty: Story = {
  args: { workflow: null },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 100));
    const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
    await el.updateComplete;

    const container = el.shadowRoot?.querySelector('[data-prism-component="workflow-graph"]');
    await expect(container).not.toBeNull();
  },
};

export const WorkspaceCanvas: Story = {
  args: { workflow: WORKSPACE_WORKFLOW },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 120));
    const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(WORKSPACE_WORKFLOW.stages.length);
    await expect(root.querySelectorAll('[data-prism-transition]').length).toBe(WORKSPACE_WORKFLOW.transitions.length);
  },
};

export const InteractiveWorkspace: Story = {
  args: { workflow: WORKSPACE_WORKFLOW },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 160));
    const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    root.querySelector<HTMLButtonElement>('[data-prism-add-stage]')!.click();
    await el.updateComplete;
    await expect(root.querySelector('[data-prism-create-stage-dialog]')).not.toBeNull();

    fillCreateStageDialog(root, 'Evidence Review', 'evidence-review', 'reviewer', 'review');
    root.querySelector<HTMLButtonElement>('[data-prism-create-stage-submit]')!.click();
    await el.updateComplete;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(WORKSPACE_WORKFLOW.stages.length + 1);

    const declaration = root.querySelector<HTMLElement>('[data-prism-stage="applicant-details"]')!;
    let inspectorOpened = false;
    el.addEventListener('inspector-requested', event => {
      const detail = (event as CustomEvent<{ kind: string; stageKey?: string }>).detail;
      if (detail.kind === 'stage' && detail.stageKey === 'applicant-details') {
        inspectorOpened = true;
      }
    });

    declaration.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, composed: true }));
    await el.updateComplete;
    await expect(inspectorOpened).toBe(true);

    declaration.dispatchEvent(new MouseEvent('contextmenu', {
      bubbles: true,
      composed: true,
      clientX: 240,
      clientY: 220,
    }));
    await el.updateComplete;
    await expect(root.querySelector('[data-prism-context-menu]')).not.toBeNull();

    root.querySelector<HTMLElement>('[data-prism-transition-handle="waiting-for-review"]')!.dispatchEvent(
      new MouseEvent('click', { bubbles: true, composed: true, detail: 0 })
    );
    await el.updateComplete;
    await expect(root.querySelector('[data-prism-create-transition-dialog]')).not.toBeNull();

    fillCreateTransitionDialog(root, 'assign', 'confirmation', 'guard', 'case.readyForDecision == true');
    root.querySelector<HTMLButtonElement>('[data-prism-create-transition-submit]')!.click();
    await el.updateComplete;
    await expect(root.querySelectorAll('[data-prism-transition]').length).toBe(WORKSPACE_WORKFLOW.transitions.length + 1);

    root.querySelector<HTMLButtonElement>('[data-prism-fit-screen]')!.click();
    await el.updateComplete;
    await expect(Boolean(root.querySelector<HTMLElement>('[data-prism-zoom]')?.textContent?.includes('%'))).toBe(true);
  },
};

export const DeleteConfirmation: Story = {
  args: { workflow: WORKSPACE_WORKFLOW },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 140));
    const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    root.querySelector<HTMLButtonElement>('.mode-toggle')!.click();
    await el.updateComplete;

    root.querySelector<HTMLButtonElement>('[data-prism-delete-stage="reviewer-assessment"]')!.click();
    await el.updateComplete;

    await expect(root.querySelector('[data-prism-delete-stage-dialog]')).not.toBeNull();
    await expect(root.querySelectorAll('[data-prism-delete-stage-transitions] li').length).toBeGreaterThan(0);

    root.querySelector<HTMLButtonElement>('[data-prism-delete-stage-cancel]')!.click();
    await el.updateComplete;
    await expect(root.querySelector('[data-prism-delete-stage-dialog]')).toBeNull();
  },
};

export const GatewayRepresentation: Story = {
  args: { workflow: GATEWAY_WORKFLOW },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 140));
    const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-gateway]').length).toBe(2);
    await expect(root.querySelector('[data-prism-gateway-kind="Split"]')).not.toBeNull();
    await expect(root.querySelector('[data-prism-gateway-kind="Join"]')).not.toBeNull();
  },
};

export const GraphReadOnly: Story = {
  name: 'Read-only viewer (declarative HTML)',
  parameters: {
    docs: {
      description: {
        story:
          'Renders a published workflow purely from HTML attributes — no JS plumbing. ' +
          'Demonstrates the `<prism-workflow-graph read-only workflow-json="...">` recipe an ' +
          'integrator can drop into a Razor view to show a workflow diagram on a public page.',
      },
    },
  },
  render: () => {
    const container = document.createElement('div');
    container.style.cssText = 'display:block;height:560px;';
    const json = JSON.stringify(GATEWAY_WORKFLOW).replaceAll('"', '&quot;');
    container.innerHTML =
      `<prism-workflow-graph read-only workflow-json="${json}" style="display:block;height:100%;"></prism-workflow-graph>`;
    return container;
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 160));
    const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    // Read-only viewer: published workflow loaded from attribute only.
    await expect(el.readOnly).toBe(true);
    await expect(el.workflow).not.toBeNull();
    await expect(root.querySelector('[data-prism-read-only="true"]')).not.toBeNull();

    // No create affordances should be exposed.
    await expect(root.querySelector('[data-prism-add-stage]')).toBeNull();
    await expect(root.querySelector('[data-prism-add-gateway]')).toBeNull();
    await expect(root.querySelector('[data-prism-empty-add-stage]')).toBeNull();
    await expect(root.querySelector('[data-prism-context-menu]')).toBeNull();
    await expect(root.querySelector('[data-prism-create-stage-dialog]')).toBeNull();
    await expect(root.querySelector('[data-prism-create-gateway-dialog]')).toBeNull();
    await expect(root.querySelector('[data-prism-delete-stage-dialog]')).toBeNull();

    // Graph content still renders, keyboard navigation still works.
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBeGreaterThan(0);
    await expect(root.querySelectorAll('[data-prism-gateway]').length).toBeGreaterThan(0);
    await expect(root.querySelector('[role="application"]')).not.toBeNull();
  },
};
