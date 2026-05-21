import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-step-inspector.js';
import type { PrismStepInspectorElement } from './prism-step-inspector.js';
import { STUB_ACTION_CATALOG, STUB_WORKFLOW } from './types.js';
import type { ActionCatalogEntry, AuthoredWorkflow } from './types.js';

type StoryArgs = {
  workflow: AuthoredWorkflow | null;
  selectedStageKey: string | null;
  selectedTransitionIndex?: number | null;
  actionCatalog: ActionCatalogEntry[];
};

function makeElement(args: StoryArgs): PrismStepInspectorElement {
  const el = document.createElement('prism-step-inspector') as PrismStepInspectorElement;
  el.workflow = args.workflow;
  el.selectedStageKey = args.selectedStageKey;
  el.selectedTransitionIndex = args.selectedTransitionIndex ?? null;
  el.actionCatalog = args.actionCatalog;
  el.addEventListener('workflow-updated', event => {
    const detail = (event as CustomEvent<{
      workflow: AuthoredWorkflow;
      selection?: { kind?: 'stage' | 'transition'; stageKey?: string; transitionIndex?: number } | null;
    }>).detail;
    el.workflow = detail.workflow;
    if (detail.selection?.kind === 'transition') {
      el.selectedTransitionIndex = detail.selection.transitionIndex ?? null;
      el.selectedStageKey = null;
    } else if (detail.selection?.stageKey) {
      el.selectedStageKey = detail.selection.stageKey;
      el.selectedTransitionIndex = null;
    } else {
      el.selectedStageKey = null;
      el.selectedTransitionIndex = null;
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
    selectedTransitionIndex: null,
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

    const actor = root.querySelector<HTMLSelectElement>('[data-prism-stage-actor]')!;
    actor.value = 'member';
    actor.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
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

export const TransitionSelected: Story = {
  args: {
    workflow: STUB_WORKFLOW,
    selectedTransitionIndex: 0,
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 100));
    const el = canvasElement.querySelector('prism-step-inspector') as PrismStepInspectorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    await expect(root.querySelector('[data-prism-inspector-kind="transition"]')).not.toBeNull();

    const labelInput = root.querySelector<HTMLInputElement>('[data-prism-transition-label]')!;
    labelInput.value = 'route-for-review';
    labelInput.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const actionPreset = root.querySelector<HTMLSelectElement>('[data-prism-transition-action]')!;
    actionPreset.value = 'submit';
    actionPreset.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const targetSelect = root.querySelector<HTMLSelectElement>('[data-prism-transition-target]')!;
    targetSelect.value = 'reviewer-assessment';
    targetSelect.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const conditionMode = root.querySelector<HTMLSelectElement>('[data-prism-transition-condition-mode]')!;
    conditionMode.value = 'event';
    conditionMode.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    const conditionValue = root.querySelector<HTMLInputElement>('[data-prism-transition-condition-value]')!;
    conditionValue.value = 'application-submitted';
    conditionValue.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await el.updateComplete;

    await expect(root.querySelector<HTMLInputElement>('[data-prism-transition-label]')?.value).toBe('submit');
    await expect(root.querySelector('[data-prism-transition-detail="applicant-details-submit-reviewer-assessment"]')).not.toBeNull();
  },
};

export const TransitionActionConfiguration: Story = {
  args: {
    workflow: STUB_WORKFLOW,
    selectedTransitionIndex: 0,
  },
};
