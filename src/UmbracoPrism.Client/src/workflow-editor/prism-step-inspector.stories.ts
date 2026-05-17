import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-step-inspector.js';
import type { PrismStepInspectorElement } from './prism-step-inspector.js';
import { STUB_WORKFLOW } from './types.js';
import type { AuthoredWorkflow } from './types.js';

type StoryArgs = {
  workflow: AuthoredWorkflow | null;
  selectedStageKey: string | null;
};

function makeElement(args: StoryArgs): PrismStepInspectorElement {
  const el = document.createElement('prism-step-inspector') as PrismStepInspectorElement;
  el.workflow = args.workflow;
  el.selectedStageKey = args.selectedStageKey;
  el.style.cssText = 'display: block; width: 360px; height: 480px;';
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
  },
  render: (args) => makeElement(args),
};

export default meta;
type Story = StoryObj<StoryArgs>;

export const Empty: Story = {
  args: { workflow: null, selectedStageKey: null },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-step-inspector') as PrismStepInspectorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const container = root.querySelector('[data-prism-component="step-inspector"]');
    await expect(container).not.toBeNull();

    const emptyState = root.querySelector('.empty-state');
    await expect(emptyState).not.toBeNull();
  },
};

export const CaptureStage: Story = {
  args: { workflow: STUB_WORKFLOW, selectedStageKey: 'applicant-details' },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-step-inspector') as PrismStepInspectorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const container = root.querySelector('[data-prism-component="step-inspector"]')!;
    await expect(container).not.toBeNull();

    const stageDetail = root.querySelector('[data-prism-stage-detail="applicant-details"]');
    await expect(stageDetail).not.toBeNull();

    const h2 = root.querySelector('h2');
    await expect(h2).not.toBeNull();
    await expect(h2?.textContent?.trim()).toContain('Applicant Details');

    const h3s = root.querySelectorAll('h3');
    await expect(h3s.length).toBeGreaterThan(0);
  },
};

export const WaitingStage: Story = {
  args: { workflow: STUB_WORKFLOW, selectedStageKey: 'waiting-for-review' },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-step-inspector') as PrismStepInspectorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const h2 = root.querySelector('h2');
    await expect(h2?.textContent?.trim()).toContain('Waiting for Review');

    const waitingSection = root.querySelector('[id^="section-waiting"]');
    await expect(waitingSection).not.toBeNull();
  },
};

export const DecisionStage: Story = {
  args: { workflow: STUB_WORKFLOW, selectedStageKey: 'reviewer-assessment' },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-step-inspector') as PrismStepInspectorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const h2 = root.querySelector('h2');
    await expect(h2?.textContent?.trim()).toContain('Reviewer Assessment');

    const roleTag = root.querySelector('.role-tag');
    await expect(roleTag).not.toBeNull();
    await expect(roleTag?.textContent?.trim()).toContain('Planning Officer');
  },
};
