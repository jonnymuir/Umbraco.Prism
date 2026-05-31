import type { Meta, StoryObj } from '@storybook/web-components';
import './prism-workflow-editor-shell.js';
import type { PrismWorkflowEditorShellElement } from './prism-workflow-editor-shell.js';
import { PLANNING_WORKFLOW } from './fixtures/index.js';
import type { AuthoredWorkflow, AuthoredStage } from './types.js';
import { InMemoryWorkflowSource } from './in-memory-workflow-source.js';

type WorkflowSeed = {
  workflowKey: string;
  definitionKey: string;
  displayName: string;
  stages: Array<{
    stageKey: string;
    displayName: string;
    actor?: AuthoredStage['actor'];
    kind?: AuthoredStage['kind'];
    roleGates?: string[];
  }>;
  transitionActions: string[];
};

function cloneWorkflow<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function buildWorkflow(seed: WorkflowSeed): AuthoredWorkflow {
  const workflow = cloneWorkflow(PLANNING_WORKFLOW);
  const stages = seed.stages.map((stageSeed, index) => {
    const baseStage = workflow.stages[Math.min(index, workflow.stages.length - 1)];
    return {
      ...baseStage,
      stageKey: stageSeed.stageKey,
      displayName: stageSeed.displayName,
      actor: stageSeed.actor ?? baseStage.actor,
      kind: stageSeed.kind ?? baseStage.kind,
      roleGates: stageSeed.roleGates ?? [],
    };
  });

  const builtStages = stages;
  return {
    ...workflow,
    definitionKey: seed.definitionKey,
    displayName: seed.displayName,
    initialStageKey: builtStages[0]?.stageKey ?? workflow.initialStageKey,
    stages: builtStages,
    gateways: builtStages.slice(0, -1).map((stage, index) => ({
      gatewayKey: `route-from-${stage.stageKey}`,
      displayName: `Route from ${stage.displayName}`,
      kind: 'Split' as const,
      source: stage.stageKey,
      roleGates: [],
      routes: [{
        id: `${stage.stageKey}--${seed.transitionActions[index] ?? 'continue'}--${builtStages[index + 1].stageKey}`,
        target: builtStages[index + 1].stageKey,
        trigger: seed.transitionActions[index] ?? 'continue',
        actions: [],
      }],
    })),
  };
}

function buildShellSource(): InMemoryWorkflowSource {
  const planning = cloneWorkflow(PLANNING_WORKFLOW);
  const communityEnquiry = buildWorkflow({
    workflowKey: 'community-enquiry',
    definitionKey: 'community-enquiry',
    displayName: 'Community Enquiry',
    stages: [
      { stageKey: 'raise-enquiry', displayName: 'Raise enquiry', actor: 'public' },
      { stageKey: 'share-supporting-detail', displayName: 'Share supporting detail', actor: 'public' },
      {
        stageKey: 'review-enquiry',
        displayName: 'Review enquiry',
        actor: 'reviewer',
        kind: 'TaskList',
        roleGates: ['reviewer'],
      },
      {
        stageKey: 'enquiry-closed',
        displayName: 'Enquiry closed',
        actor: 'reviewer',
        kind: 'Confirmation',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'send to review', 'close enquiry'],
  });
  const informationRequest = buildWorkflow({
    workflowKey: 'information-request',
    definitionKey: 'information-request',
    displayName: 'Information Request',
    stages: [
      { stageKey: 'request-summary', displayName: 'Request summary', actor: 'public' },
      { stageKey: 'upload-evidence', displayName: 'Upload evidence', actor: 'public' },
      {
        stageKey: 'review-response-pack',
        displayName: 'Review response pack',
        actor: 'reviewer',
        kind: 'TaskList',
        roleGates: ['reviewer'],
      },
      {
        stageKey: 'response-sent',
        displayName: 'Response sent',
        actor: 'system',
        kind: 'Confirmation',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'submit evidence', 'send response'],
  });
  const paymentDemo = buildWorkflow({
    workflowKey: 'payment-demo',
    definitionKey: 'payment-demo',
    displayName: 'Payment Demo',
    stages: [
      { stageKey: 'start-payment', displayName: 'Start payment', actor: 'public' },
      { stageKey: 'capture-card-details', displayName: 'Capture card details', actor: 'public' },
      {
        stageKey: 'review-payment',
        displayName: 'Review payment',
        actor: 'reviewer',
        kind: 'TaskList',
        roleGates: ['reviewer'],
      },
      {
        stageKey: 'payment-received',
        displayName: 'Payment received',
        actor: 'system',
        kind: 'Confirmation',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'submit payment', 'confirm payment'],
  });

  // workflowKey for planning is 'planning' even though the definitionKey is
  // 'planning-application', so the shell's selector entries match the four
  // reference workflows the existing Playwright suite drives.
  return new InMemoryWorkflowSource([
    { workflowKey: 'planning', workflow: planning },
    { workflowKey: 'community-enquiry', workflow: communityEnquiry },
    { workflowKey: 'information-request', workflow: informationRequest },
    { workflowKey: 'payment-demo', workflow: paymentDemo },
  ]);
}

function makeShell(): PrismWorkflowEditorShellElement {
  const element = document.createElement('prism-workflow-editor-shell') as PrismWorkflowEditorShellElement;
  element.workflowKey = 'planning';
  element.workflowSource = buildShellSource();
  element.style.cssText = 'display:block;min-height:860px;';
  return element;
}

const meta: Meta = {
  title: 'Workflow Editor/Editor Shell',
  component: 'prism-workflow-editor-shell',
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-required-children', enabled: true },
        ],
      },
    },
  },
  render: () => makeShell(),
};

export default meta;
type Story = StoryObj;

export const ReferenceShell: Story = {};

export const NarrowViewportTablet: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
  render: () => {
    const element = makeShell();
    element.style.cssText = 'display:block;width:768px;min-height:860px;';
    return element;
  },
};

export const NarrowViewportMobile: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  render: () => {
    const element = makeShell();
    element.style.cssText = 'display:block;width:375px;min-height:667px;';
    return element;
  },
};
