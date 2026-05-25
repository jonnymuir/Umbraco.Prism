import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-workflow-graph.js';
import type { PrismWorkflowGraphElement } from './prism-workflow-graph.js';
import { STUB_WORKFLOW } from './types.js';
import type { AuthoredWorkflow } from './types.js';

const WORKSPACE_WORKFLOW: AuthoredWorkflow = {
  ...STUB_WORKFLOW,
  transitions: [
    ...STUB_WORKFLOW.transitions,
    { fromStage: 'waiting-for-review', toStage: 'reviewer-assessment', action: 'assign', requiresRole: 'reviewer', actions: [] },
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

export const LinearMode: Story = {
  args: { workflow: WORKSPACE_WORKFLOW },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 140));
    const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    root.querySelector<HTMLButtonElement>('.mode-toggle')!.click();
    await el.updateComplete;

    await expect(root.querySelector('[data-prism-linear-table]')).not.toBeNull();

    root.querySelector<HTMLButtonElement>('[data-prism-linear-filter="reviewer"]')!.click();
    await el.updateComplete;
    await expect(root.querySelectorAll('[data-prism-list-row]').length).toBe(1);

    root.querySelector<HTMLButtonElement>('[data-prism-linear-filter="__all__"]')!.click();
    await el.updateComplete;

    const titleInput = root.querySelector<HTMLInputElement>('[data-prism-list-row="applicant-details"] [data-prism-inline-field="displayName"]')!;
    titleInput.value = 'Applicant Details Updated';
    titleInput.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const moveDown = root.querySelector<HTMLButtonElement>('[data-prism-move-down="applicant-details"]')!;
    moveDown.click();
    await el.updateComplete;

    root.querySelector<HTMLButtonElement>('[data-prism-insert-after="reviewer-assessment"]')!.click();
    await el.updateComplete;
    fillCreateStageDialog(root, 'Case closure', 'case-closure', 'reviewer', 'confirmation');
    root.querySelector<HTMLButtonElement>('[data-prism-create-stage-submit]')!.click();
    await el.updateComplete;

    await expect(root.querySelectorAll('[data-prism-list-row]').length).toBe(WORKSPACE_WORKFLOW.stages.length + 1);
  },
};
