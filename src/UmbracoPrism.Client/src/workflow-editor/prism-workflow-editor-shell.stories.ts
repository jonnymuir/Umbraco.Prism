import type { Meta, StoryObj } from '@storybook/web-components';
import './prism-workflow-editor-shell.js';
import type { PrismWorkflowEditorShellElement } from './prism-workflow-editor-shell.js';
import type { WorkflowAuthoringSummary } from './workflow-authoring-client.js';
import { PLANNING_WORKFLOW } from './fixtures/index.js';
import { STUB_ACTION_CATALOG, type AuthoredWorkflow, type AuthoredStage } from './types.js';
import { projectWorkflowLocally } from './workflow-runtime-projection.js';

const AUTHORING_API_BASE = 'https://example.test';

type WorkflowSeed = {
  workflowKey: string;
  definitionKey: string;
  displayName: string;
  stages: Array<{
    stageKey: string;
    displayName: string;
    actor?: AuthoredStage['actor'];
    kind?: AuthoredStage['kind'];
    editorSurface?: AuthoredStage['editorSurface'];
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
      editorSurface: stageSeed.editorSurface,
      roleGates: stageSeed.roleGates ?? [],
    };
  });

  return {
    ...workflow,
    definitionKey: seed.definitionKey,
    displayName: seed.displayName,
    initialStageKey: stages[0]?.stageKey ?? workflow.initialStageKey,
    stages,
    transitions: stages.slice(0, -1).map((stage, index) => ({
      fromStage: stage.stageKey,
      toStage: stages[index + 1].stageKey,
      action: seed.transitionActions[index] ?? 'continue',
      actions: [],
    })),
  };
}

const WORKFLOWS: Record<string, AuthoredWorkflow> = {
  planning: cloneWorkflow(PLANNING_WORKFLOW),
  'community-enquiry': buildWorkflow({
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
        editorSurface: 'back-stage',
        roleGates: ['reviewer'],
      },
      {
        stageKey: 'enquiry-closed',
        displayName: 'Enquiry closed',
        actor: 'reviewer',
        kind: 'Confirmation',
        editorSurface: 'back-stage',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'send to review', 'close enquiry'],
  }),
  'information-request': buildWorkflow({
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
        editorSurface: 'back-stage',
        roleGates: ['reviewer'],
      },
      {
        stageKey: 'response-sent',
        displayName: 'Response sent',
        actor: 'system',
        kind: 'Confirmation',
        editorSurface: 'back-stage',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'submit evidence', 'send response'],
  }),
  'payment-demo': buildWorkflow({
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
        editorSurface: 'back-stage',
        roleGates: ['reviewer'],
      },
      {
        stageKey: 'payment-received',
        displayName: 'Payment received',
        actor: 'system',
        kind: 'Confirmation',
        editorSurface: 'back-stage',
        roleGates: ['reviewer'],
      },
    ],
    transitionActions: ['continue', 'submit payment', 'confirm payment'],
  }),
};

const WORKFLOW_SUMMARIES: WorkflowAuthoringSummary[] = [
  {
    workflowKey: 'planning',
    id: 'planning-story',
    definitionKey: WORKFLOWS.planning.definitionKey,
    displayName: WORKFLOWS.planning.displayName,
  },
  {
    workflowKey: 'community-enquiry',
    id: 'community-enquiry-story',
    definitionKey: WORKFLOWS['community-enquiry'].definitionKey,
    displayName: WORKFLOWS['community-enquiry'].displayName,
  },
  {
    workflowKey: 'information-request',
    id: 'information-request-story',
    definitionKey: WORKFLOWS['information-request'].definitionKey,
    displayName: WORKFLOWS['information-request'].displayName,
  },
  {
    workflowKey: 'payment-demo',
    id: 'payment-demo-story',
    definitionKey: WORKFLOWS['payment-demo'].definitionKey,
    displayName: WORKFLOWS['payment-demo'].displayName,
  },
];

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function stubFetchFor(element: PrismWorkflowEditorShellElement): void {
  const originalFetch = window.fetch;

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const request = input instanceof Request ? input : undefined;
    const urlString = typeof input === 'string'
      ? input
      : input instanceof URL
        ? input.href
        : request?.url ?? '';
    const method = (init?.method ?? request?.method ?? 'GET').toUpperCase();

    if (/\/api\/workflow-authoring\/action-catalog(?:\?.*)?$/.test(urlString)) {
      return jsonResponse(STUB_ACTION_CATALOG);
    }

    if (/\/api\/workflow-authoring\/workflows(?:\?.*)?$/.test(urlString) && method === 'GET') {
      return jsonResponse(WORKFLOW_SUMMARIES);
    }

    const workflowMatch = urlString.match(/\/api\/workflow-authoring\/workflows\/([^/?#]+)(?:\/([^/?#]+))?(?:\?.*)?$/);
    if (workflowMatch) {
      const workflowKey = decodeURIComponent(workflowMatch[1]);
      const operation = workflowMatch[2] ?? null;
      const workflow = WORKFLOWS[workflowKey];
      if (!workflow) {
        return jsonResponse({ error: `Workflow '${workflowKey}' not found.` }, 404);
      }

      if (!operation && method === 'GET') {
        return jsonResponse(workflow);
      }

      const body = init?.body ? JSON.parse(String(init.body)) : workflow;
      if (operation === 'project' && method === 'POST') {
        return jsonResponse(projectWorkflowLocally(body as AuthoredWorkflow));
      }

      if (['preview', 'apply', 'publish', 'save'].includes(operation ?? '') && method === 'POST') {
        return jsonResponse(body);
      }
    }

    return originalFetch(input, init);
  };

  const observer = new MutationObserver(() => {
    if (!document.contains(element)) {
      window.fetch = originalFetch;
      observer.disconnect();
    }
  });

  observer.observe(document.body, { childList: true, subtree: true });
}

function makeShell(): PrismWorkflowEditorShellElement {
  const element = document.createElement('prism-workflow-editor-shell') as PrismWorkflowEditorShellElement;
  element.workflowKey = 'planning';
  element.authoringApiBase = AUTHORING_API_BASE;
  element.style.cssText = 'display:block;min-height:860px;';
  stubFetchFor(element);
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
