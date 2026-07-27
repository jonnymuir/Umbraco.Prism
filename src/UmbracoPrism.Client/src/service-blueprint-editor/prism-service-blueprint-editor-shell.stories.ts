import type { Meta, StoryObj } from '@storybook/web-components';
import './prism-service-blueprint-editor-shell.js';
import type { PrismServiceBlueprintEditorShellElement } from './prism-service-blueprint-editor-shell.js';
import { PAYMENT_DEMO_SERVICE_BLUEPRINT, PLANNING_SERVICE_BLUEPRINT, cloneAuthoredServiceBlueprint } from './fixtures/index.js';
import type { AuthoredStage, AuthoredServiceBlueprint } from './types.js';
import { InMemoryServiceBlueprintSource } from './in-memory-service-blueprint-source.js';
import type { QueueDefinition } from './stage-assignment.js';

type ServiceBlueprintSeed = {
  blueprintKey: string;
  definitionKey: string;
  displayName: string;
  stages: Array<{
    stateKey: string;
    displayName: string;
    actor?: AuthoredStage['actor'];
    kind?: AuthoredStage['kind'];
    roleGates?: string[];
  }>;
  transitionActions: string[];
};

function cloneServiceBlueprint<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function buildServiceBlueprint(seed: ServiceBlueprintSeed): AuthoredServiceBlueprint {
  const serviceBlueprint = cloneServiceBlueprint(PLANNING_SERVICE_BLUEPRINT);
  const stages = seed.stages.map((stageSeed, index) => {
    const baseStage = serviceBlueprint.stages[Math.min(index, serviceBlueprint.stages.length - 1)];
    return {
      ...baseStage,
      stateKey: stageSeed.stateKey,
      displayName: stageSeed.displayName,
      actor: stageSeed.actor ?? baseStage.actor,
      kind: stageSeed.kind ?? baseStage.kind,
      roleGates: stageSeed.roleGates ?? [],
    };
  });

  const builtStages = stages;
  return {
    ...serviceBlueprint,
    definitionKey: seed.definitionKey,
    displayName: seed.displayName,
    initialStage: builtStages[0]?.stateKey ?? serviceBlueprint.initialStage,
    stages: builtStages,
    transitions: builtStages.slice(0, -1).flatMap((stage, index) => {
      const gatewayKey = `route-from-${stage.stateKey}`;
      const targetKey = builtStages[index + 1].stateKey;
      return [
        { fromState: stage.stateKey, toState: gatewayKey, action: 'route' },
        { fromState: gatewayKey, toState: targetKey, action: seed.transitionActions[index] ?? 'continue' },
      ];
    }),
    metadata: { gateways: builtStages.slice(0, -1).map(stage => ({
      key: `route-from-${stage.stateKey}`,
      displayName: `Route from ${stage.displayName}`,
      gatewayType: 'Split' as const,
      queueKey: stage.metadata?.queueKey ?? 'public',
      actor: stage.metadata?.actor,
      roleGates: [],
    })) },
  } as unknown as AuthoredServiceBlueprint;
}

function buildShellSource(): InMemoryServiceBlueprintSource {
  const planning = cloneServiceBlueprint(PLANNING_SERVICE_BLUEPRINT);
  const communityEnquiry = buildServiceBlueprint({
    blueprintKey: 'community-enquiry',
    definitionKey: 'community-enquiry',
    displayName: 'Community Enquiry',
    stages: [
      { stateKey: 'raise-enquiry', displayName: 'Raise enquiry', actor: 'public' },
      { stateKey: 'share-supporting-detail', displayName: 'Share supporting detail', actor: 'public' },
      {
        stateKey: 'review-enquiry',
        displayName: 'Review enquiry',
        actor: 'reviewer',
        kind: 'TaskList',
        roleGates: ['reviewer'],
      },
      {
        stateKey: 'enquiry-closed',
        displayName: 'Enquiry closed',
        actor: 'reviewer',
        kind: 'Confirmation',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'send to review', 'close enquiry'],
  });
  const informationRequest = buildServiceBlueprint({
    blueprintKey: 'information-request',
    definitionKey: 'information-request',
    displayName: 'Information Request',
    stages: [
      { stateKey: 'request-summary', displayName: 'Request summary', actor: 'public' },
      { stateKey: 'upload-evidence', displayName: 'Upload evidence', actor: 'public' },
      {
        stateKey: 'review-response-pack',
        displayName: 'Review response pack',
        actor: 'reviewer',
        kind: 'TaskList',
        roleGates: ['reviewer'],
      },
      {
        stateKey: 'response-sent',
        displayName: 'Response sent',
        actor: 'system',
        kind: 'Confirmation',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'submit evidence', 'send response'],
  });
  const paymentDemo = cloneAuthoredServiceBlueprint(PAYMENT_DEMO_SERVICE_BLUEPRINT);

  // blueprintKey for planning is 'planning' even though the definitionKey is
  // 'planning-application', so the shell's selector entries match the four
  // reference serviceBlueprints the existing Playwright suite drives.
  return new InMemoryServiceBlueprintSource([
    { blueprintKey: 'planning', serviceBlueprint: planning },
    { blueprintKey: 'community-enquiry', serviceBlueprint: communityEnquiry },
    { blueprintKey: 'information-request', serviceBlueprint: informationRequest },
    { blueprintKey: 'payment-demo', serviceBlueprint: paymentDemo },
  ]);
}

const REFERENCE_QUEUES: QueueDefinition[] = [
  { queueName: 'web-user', displayName: 'Applicant' },
  { queueName: 'business-user', displayName: 'Payments team' },
  { queueName: 'applicant', displayName: 'Applicant' },
  { queueName: 'public', displayName: 'Public' },
  { queueName: 'reviewer', displayName: 'Reviewer' },
  { queueName: 'payments', displayName: 'Payments' },
  { queueName: 'system', displayName: 'System' },
];

function makeShell(): PrismServiceBlueprintEditorShellElement {
  const element = document.createElement('prism-service-blueprint-editor-shell') as PrismServiceBlueprintEditorShellElement;
  element.blueprintKey = 'planning';
  element.serviceBlueprintSource = buildShellSource();
  element.availableQueues = REFERENCE_QUEUES;
  element.style.cssText = 'display:block;min-height:860px;';
  return element;
}

const meta: Meta = {
  title: 'Service Blueprint Editor/Editor Shell',
  component: 'prism-service-blueprint-editor-shell',
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
