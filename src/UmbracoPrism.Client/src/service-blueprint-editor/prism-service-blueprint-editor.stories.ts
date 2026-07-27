import type { Meta, StoryObj } from '@storybook/web-components';
import { expect, waitFor } from '@storybook/test';
import './prism-service-blueprint-editor.js';
import type { PrismServiceBlueprintEditorElement } from './prism-service-blueprint-editor.js';
import { PLANNING_SERVICE_BLUEPRINT, LEAVE_REQUEST_STARTER_SERVICE_BLUEPRINT, cloneAuthoredServiceBlueprint } from './fixtures/index.js';
import type { AuthoredServiceBlueprint } from './types.js';
import { InMemoryServiceBlueprintSource } from './in-memory-service-blueprint-source.js';
import type { QueueDefinition } from './stage-assignment.js';

const STORY_QUEUES: QueueDefinition[] = [
  { queueName: 'web-user', displayName: 'Applicant' },
  { queueName: 'business-user', displayName: 'Payments team' },
  { queueName: 'applicant', displayName: 'Applicant' },
  { queueName: 'reviewer', displayName: 'Reviewer' },
  { queueName: 'payments', displayName: 'Payments' },
  { queueName: 'public', displayName: 'Public' },
  { queueName: 'system', displayName: 'System' },
];

function makeEditor(serviceBlueprint: AuthoredServiceBlueprint = PLANNING_SERVICE_BLUEPRINT): PrismServiceBlueprintEditorElement {
  const el = document.createElement('prism-service-blueprint-editor') as PrismServiceBlueprintEditorElement;
  // Stories drive the editor by injecting the service blueprint directly. The Save
  // button still needs a `serviceBlueprintSource` to resolve, so wire an in-memory
  // one seeded with the same serviceBlueprint — this proves the integrator pattern.
  el.serviceBlueprintSource = new InMemoryServiceBlueprintSource([serviceBlueprint]);
  el.blueprintKey = serviceBlueprint.definitionKey;
  el.initialServiceBlueprint = serviceBlueprint;
  el.availableQueues = STORY_QUEUES;
  el.style.cssText = 'display: block; width: 1200px; height: 700px;';
  return el;
}

function makeEmptyServiceBlueprint(): AuthoredServiceBlueprint {
  const serviceBlueprint = JSON.parse(JSON.stringify(PLANNING_SERVICE_BLUEPRINT)) as AuthoredServiceBlueprint;
  return {
    ...serviceBlueprint,
    displayName: 'Empty ServiceBlueprint',
    initialStage: '',
    stages: [],
    gateways: [],
  };
}

function makeSimulationBranchServiceBlueprint(): AuthoredServiceBlueprint {
  const serviceBlueprint = JSON.parse(JSON.stringify(PLANNING_SERVICE_BLUEPRINT)) as AuthoredServiceBlueprint;
  serviceBlueprint.displayName = 'Planning Application Simulation';
  serviceBlueprint.stages = [
    serviceBlueprint.stages[0],
    serviceBlueprint.stages[1],
    {
      stateKey: 'review-decision',
      displayName: 'Reviewer decision',
      description: 'Reviewer chooses whether to approve, reject, or request more checks.',
      kind: 'TaskList',
      actor: 'reviewer',
      actions: [],
      components: [],
      roleGates: ['reviewer'],
    },
    {
      stateKey: 'checks-pending',
      displayName: 'Checks pending',
      description: 'The application is paused while further checks run.',
      kind: 'Question',
      actor: 'reviewer',
      actions: [],
      components: [],
      roleGates: ['reviewer'],
    },
    {
      stateKey: 'approved',
      displayName: 'Application approved',
      description: 'The application has been approved.',
      kind: 'Confirmation',
      actor: 'reviewer',
      actions: [],
      components: [],
      roleGates: ['reviewer'],
    },
    {
      stateKey: 'rejected',
      displayName: 'Application rejected',
      description: 'The application has been rejected.',
      kind: 'Confirmation',
      actor: 'reviewer',
      actions: [],
      components: [],
      roleGates: ['reviewer'],
    },
  ];
  serviceBlueprint.gateways = [
    {
      key: 'review-decision-routes',
      displayName: 'Review decision routes',
      gatewayType: 'Split',
      source: 'review-decision',
      queueKey: 'reviewer',
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
      key: 'declaration-routes',
      displayName: 'Declaration routes',
      gatewayType: 'Split',
      source: 'declaration',
      queueKey: 'applicant',
      roleGates: [],
      routes: [
        { id: 'declaration--continue--application-form', target: 'application-form', trigger: 'continue' },
      ],
    },
    {
      key: 'application-form-routes',
      displayName: 'Application form routes',
      gatewayType: 'Split',
      source: 'application-form',
      queueKey: 'applicant',
      roleGates: [],
      routes: [
        { id: 'application-form--submit--review-decision', target: 'review-decision', trigger: 'submit for review' },
      ],
    },
  ];
  return serviceBlueprint;
}

function makeSimulationBlockerServiceBlueprint(): AuthoredServiceBlueprint {
  const serviceBlueprint = makeSimulationBranchServiceBlueprint();
  serviceBlueprint.displayName = 'Planning Application Simulation Blockers';
  const rejectGateway = (serviceBlueprint.gateways ?? []).find(g => g.key === 'review-decision-routes');
  if (rejectGateway) {
    rejectGateway.routes = (rejectGateway.routes ?? []).map(route =>
      route.trigger === 'reject'
        ? { ...route, target: 'missing-rejection-stage' }
        : route
    );
  }
  return serviceBlueprint;
}

const meta: Meta = {
  title: 'Service Blueprint Editor/Editor Host',
  component: 'prism-service-blueprint-editor',
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

export const PlanningServiceBlueprint: Story = {
  name: 'Planning ServiceBlueprint',
  play: async ({ canvasElement }) => {
    const el = canvasElement.querySelector('prism-service-blueprint-editor') as PrismServiceBlueprintEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;

    const container = root.querySelector('[data-prism-component="service-blueprint-editor"]');
    await expect(container).not.toBeNull();

    const title = root.querySelector('.editor-title');
    await expect(title?.textContent?.trim()).toBe('Planning Application');

    const graph = root.querySelector('prism-service-blueprint-graph');
    await expect(graph).not.toBeNull();

    // The React Flow canvas mounts lazily; wait for it to signal readiness
    // rather than racing a fixed delay against the async import.
    await waitFor(() => {
      expect(graph?.shadowRoot?.querySelectorAll('[data-prism-role-queue]').length ?? 0).toBeGreaterThan(0);
    }, { timeout: 5000 });

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
    const el = canvasElement.querySelector('prism-service-blueprint-editor') as PrismServiceBlueprintEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const graph = root.querySelector('prism-service-blueprint-graph');
    const inspector = root.querySelector('prism-step-inspector');
    await expect(graph).not.toBeNull();
    await expect(inspector).not.toBeNull();

    // The React Flow canvas mounts lazily; wait for the stage button to land.
    await waitFor(() => {
      const stage = graph!.shadowRoot!.querySelector<HTMLButtonElement>(
        'button[aria-label="Declaration, Applicant queue"]'
      );
      expect(stage).not.toBeNull();
    });
    const declarationStage = graph!.shadowRoot!.querySelector<HTMLButtonElement>(
      'button[aria-label="Declaration, Applicant queue"]'
    )!;
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

export const EmptyServiceBlueprint: Story = {
  name: 'Empty ServiceBlueprint',
  render: () => {
    const el = makeEditor();
    el.initialServiceBlueprint = makeEmptyServiceBlueprint();
    return el;
  },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-service-blueprint-editor') as PrismServiceBlueprintEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const graph = root.querySelector('prism-service-blueprint-graph');
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
  render: () => makeEditor(makeSimulationBranchServiceBlueprint()),
};

export const SimulationBlockers: Story = {
  name: 'Simulation Blockers',
  render: () => makeEditor(makeSimulationBlockerServiceBlueprint()),
};

export const GatewayRepresentation: Story = {
  name: 'Gateway Representation',
  render: () => makeEditor(cloneAuthoredServiceBlueprint(LEAVE_REQUEST_STARTER_SERVICE_BLUEPRINT)),
};
