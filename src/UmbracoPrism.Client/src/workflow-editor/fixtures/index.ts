/**
 * Planning workflow fixture — shared between Core.Tests and Client.
 * The raw JSON is byte-aligned with:
 *   src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json
 */

import type {
  AuthoredAction,
  AuthoredComponent,
  AuthoredGateway,
  AuthoredInputComponent,
  AuthoredRoute,
  AuthoredStage,
  AuthoredWorkflow,
  StageKind,
} from '../types.js';
interface FixtureField {
  key: string;
  label: string;
  type: string;
  required: boolean;
  hint?: string;
  options: string[];
}

interface FixtureAction {
  type: string;
  timing: AuthoredAction['timing'];
  parameterSchemaKey?: string;
  params?: Record<string, unknown>;
  summary?: string;
}

interface FixtureStage {
  stageKey: string;
  displayName: string;
  description?: string;
  kind: string;
  actor?: string;
  actions: FixtureAction[];
  fields: FixtureField[];
  roleGates: string[];
  editorComment?: string;
}

interface FixtureRoute {
  id: string;
  target: string;
  trigger: string;
  condition?: string;
  requiresRole?: string;
  actions?: FixtureAction[];
}

interface FixtureGateway {
  gatewayKey: string;
  displayName: string;
  description?: string;
  kind: 'Split' | 'Join';
  laneKey?: string;
  actor?: string;
  source?: string;
  roleGates?: string[];
  routes: FixtureRoute[];
}

interface RawPlanningWorkflow {
  id: string;
  definitionKey: string;
  displayName: string;
  version: number;
  schemaVersion: string;
  initialStageKey: string;
  instancePolicy: string;
  stages: FixtureStage[];
  gateways: FixtureGateway[];
}

const RAW: RawPlanningWorkflow = {
  id: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
  definitionKey: 'planning-application',
  displayName: 'Planning Application',
  version: 1,
  schemaVersion: '1.0',
  initialStageKey: 'declaration',
  instancePolicy: 'single',
  stages: [
    {
      stageKey: 'declaration',
      displayName: 'Declaration',
      description: 'Collects applicant and site identity before the full planning form.',
      kind: 'Question',
      actor: 'applicant',
      actions: [
        {
          type: 'forms.load',
          timing: 'OnEntry',
          parameterSchemaKey: 'forms-form-definition',
          params: { formDefinitionId: 'planning-declaration' },
          summary: 'Load the declaration form.',
        },
      ],
      fields: [
        {
          key: 'applicant-name',
          label: 'Applicant name',
          type: 'Text',
          required: true,
          hint: 'Enter the full name of the person or organisation applying.',
          options: [],
        },
        {
          key: 'site-address',
          label: 'Site address',
          type: 'Textarea',
          required: true,
          hint: 'Enter the full address of the site where development is proposed.',
          options: [],
        },
      ],
      roleGates: [],
      editorComment: 'Entry point — collects basic applicant and site identity.',
    },
    {
      stageKey: 'application-form',
      displayName: 'Application Form',
      description: 'Captures the substantive planning request.',
      kind: 'Question',
      actor: 'applicant',
      actions: [
        {
          type: 'forms.save',
          timing: 'OnExit',
          parameterSchemaKey: 'forms-form-definition',
          params: { formDefinitionId: 'planning-application' },
          summary: 'Persist the application form before moving on.',
        },
      ],
      fields: [
        {
          key: 'description',
          label: 'Description of proposed works',
          type: 'Textarea',
          required: true,
          hint: 'Provide a clear description of the development you are proposing.',
          options: [],
        },
        {
          key: 'development-type',
          label: 'Type of development',
          type: 'Select',
          required: true,
          options: ['New build', 'Extension', 'Change of use', 'Demolition', 'Other'],
        },
      ],
      roleGates: [],
    },
    {
      stageKey: 'check-answers',
      displayName: 'Check your answers',
      description: 'Summarises captured answers before final submission.',
      kind: 'CheckAnswers',
      actor: 'applicant',
      actions: [],
      fields: [],
      roleGates: [],
      editorComment: 'Summary of all answers before final submission.',
    },
    {
      stageKey: 'submitted',
      displayName: 'Application submitted',
      description: 'Confirms receipt and moves the case into reviewer handling.',
      kind: 'Confirmation',
      actor: 'applicant',
      actions: [],
      fields: [],
      roleGates: [],
    },
  ],
  gateways: [
    {
      gatewayKey: 'route-application-form',
      displayName: 'Route to application form',
      kind: 'Split',
      laneKey: 'applicant',
      actor: 'applicant',
      source: 'declaration',
      roleGates: [],
      routes: [
        {
          id: 'declaration--continue--application-form',
          target: 'application-form',
          trigger: 'continue',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'route-check-answers',
      displayName: 'Route to check answers',
      kind: 'Split',
      laneKey: 'applicant',
      actor: 'applicant',
      source: 'application-form',
      roleGates: [],
      routes: [
        {
          id: 'application-form--continue--check-answers',
          target: 'check-answers',
          trigger: 'continue',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'route-submitted',
      displayName: 'Route to submitted',
      kind: 'Split',
      laneKey: 'applicant',
      actor: 'applicant',
      source: 'check-answers',
      roleGates: [],
      routes: [
        {
          id: 'check-answers--submit--submitted',
          target: 'submitted',
          trigger: 'submit',
          condition: 'guard:application.isComplete == true',
          actions: [
            {
              type: 'forms.submit',
              timing: 'OnTransition',
              parameterSchemaKey: 'forms-form-definition',
              params: { formDefinitionId: 'planning-application' },
              summary: 'Submit the application form to the business app.',
            },
          ],
        },
      ],
    },
  ],
};

function mapKind(raw: string): StageKind {
  switch (raw) {
    case 'Question':
      return 'Question';
    case 'CheckAnswers':
      return 'CheckAnswers';
    case 'Confirmation':
      return 'Confirmation';
    case 'TaskList':
      return 'TaskList';
    default:
      // Closed enum. The server rejects unknown kinds with PROJ005; mirror
      // that here so a malformed fixture fails loudly at load time.
      throw new Error(
        `Unknown stage kind "${raw}". Allowed kinds: Question, CheckAnswers, Confirmation, TaskList.`
      );
  }
}

function mapFieldTypeToComponentType(raw: string): AuthoredInputComponent['type'] {
  switch (raw) {
    case 'Textarea': return 'textarea';
    case 'Select': return 'select';
    case 'Radios': return 'radio';
    case 'Checkboxes': return 'checkboxlist';
    case 'Date': return 'date';
    case 'Email': return 'email';
    case 'Number': return 'number';
    case 'Decimal': return 'decimal';
    case 'Boolean': return 'boolean';
    case 'Text':
    default:
      return 'text';
  }
}

function fixtureFieldToInputComponent(f: FixtureField): AuthoredInputComponent {
  return {
    type: mapFieldTypeToComponentType(f.type),
    fieldKey: f.key,
    label: f.label,
    required: f.required,
    hint: f.hint,
    options: f.options,
  };
}

function normaliseAction(raw: FixtureAction): AuthoredAction {
  return {
    type: raw.type,
    timing: raw.timing,
    parameterSchemaKey: raw.parameterSchemaKey,
    params: raw.params ?? {},
    summary: raw.summary,
  };
}

function normalisePlanningFixture(raw: RawPlanningWorkflow): AuthoredWorkflow {
  const stages: AuthoredStage[] = raw.stages.map(s => {
    const components: AuthoredComponent[] = s.fields.length === 0
      ? []
      : [{
          type: 'fieldset',
          legend: s.displayName,
          legendSize: 'm',
          children: s.fields.map(fixtureFieldToInputComponent),
        }];

    return {
      stageKey: s.stageKey,
      displayName: s.displayName,
      description: s.description,
      kind: mapKind(s.kind),
      actor: s.actor,
      actions: s.actions.map(normaliseAction),
      components,
      roleGates: s.roleGates,
      editorComment: s.editorComment,
    };
  });

  const gateways: AuthoredGateway[] = raw.gateways.map(g => {
    const routes: AuthoredRoute[] = g.routes.map(r => ({
      id: r.id,
      target: r.target,
      trigger: r.trigger,
      condition: r.condition,
      requiresRole: r.requiresRole,
      actions: (r.actions ?? []).map(normaliseAction),
    }));

    return {
      gatewayKey: g.gatewayKey,
      displayName: g.displayName,
      description: g.description,
      kind: g.kind,
      laneKey: g.laneKey,
      actor: g.actor,
      source: g.source,
      roleGates: g.roleGates ?? [],
      routes,
    };
  });

  return {
    definitionKey: raw.definitionKey,
    displayName: raw.displayName,
    version: raw.version,
    schemaVersion: raw.schemaVersion,
    instancePolicy: raw.instancePolicy,
    initialStageKey: raw.initialStageKey,
    stages,
    gateways,
  };
}

export const PLANNING_WORKFLOW: AuthoredWorkflow = normalisePlanningFixture(RAW);

/**
 * Returns a deep clone of an authored workflow so stories and fixtures can
 * mutate a copy without bleeding state across renders. JSON-based because
 * the authored model is pure data (no methods, no symbols).
 */
export function cloneAuthoredWorkflow<T extends AuthoredWorkflow>(workflow: T): T {
  return JSON.parse(JSON.stringify(workflow)) as T;
}

/**
 * Gateway-only starter workflow used by Slice 5 canvas slot-matrix stories
 * and Playwright proofs. Every transition routes through a named gateway:
 * a single `review-split` fans the applicant lane out into three branches
 * (one of which crosses into the reviewer lane) and `decision-join` waits
 * for every branch before releasing the decision-confirmed stage.
 */
export const LEAVE_REQUEST_STARTER_WORKFLOW: AuthoredWorkflow = {
  definitionKey: 'leave-request',
  displayName: 'Leave Request',
  version: 1,
  schemaVersion: '1.0',
  instancePolicy: 'multiple',
  initialStageKey: 'start-request',
  stages: [
    {
      stageKey: 'start-request',
      displayName: 'Start request',
      description: 'Collect the request details before the service branches into review work.',
      kind: 'Question',
      actor: 'applicant',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stageKey: 'applicant-amendments',
      displayName: 'Applicant amendments',
      description: 'Applicant updates the request when more detail is needed.',
      kind: 'Question',
      actor: 'applicant',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stageKey: 'upload-evidence',
      displayName: 'Upload evidence',
      description: 'Applicant provides the supporting documents for the request.',
      kind: 'Question',
      actor: 'applicant',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stageKey: 'reviewer-assessment',
      displayName: 'Reviewer assessment',
      description: 'Reviewer checks the request before the service can continue.',
      kind: 'Question',
      actor: 'reviewer',
      actions: [],
      components: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'decision-confirmed',
      displayName: 'Decision confirmed',
      description: 'The shared path continues here once every branch is complete.',
      kind: 'Confirmation',
      actor: 'applicant',
      actions: [],
      components: [],
      roleGates: [],
    },
  ],
  gateways: [
    {
      gatewayKey: 'review-split',
      displayName: 'Review split',
      description: 'Branch the request into the next pieces of work.',
      kind: 'Split',
      laneKey: 'applicant',
      actor: 'applicant',
      source: 'start-request',
      roleGates: [],
      routes: [
        {
          id: 'start-request--request-amendments--applicant-amendments',
          target: 'applicant-amendments',
          trigger: 'request amendments',
          actions: [],
        },
        {
          id: 'start-request--upload-evidence--upload-evidence',
          target: 'upload-evidence',
          trigger: 'upload evidence',
          actions: [],
        },
        {
          id: 'start-request--send-to-reviewer--reviewer-assessment',
          target: 'reviewer-assessment',
          trigger: 'send to reviewer',
          requiresRole: 'reviewer',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'applicant-amendments-feed',
      displayName: 'Applicant amendments feed',
      description: 'Feed the decision join once amendments are complete.',
      kind: 'Split',
      laneKey: 'applicant',
      actor: 'applicant',
      source: 'applicant-amendments',
      roleGates: [],
      routes: [
        {
          id: 'applicant-amendments--finish-amendments--decision-join',
          target: 'decision-join',
          trigger: 'finish amendments',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'upload-evidence-feed',
      displayName: 'Upload evidence feed',
      description: 'Feed the decision join once evidence is uploaded.',
      kind: 'Split',
      laneKey: 'applicant',
      actor: 'applicant',
      source: 'upload-evidence',
      roleGates: [],
      routes: [
        {
          id: 'upload-evidence--evidence-complete--decision-join',
          target: 'decision-join',
          trigger: 'evidence complete',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'reviewer-assessment-feed',
      displayName: 'Reviewer assessment feed',
      description: 'Feed the decision join once the reviewer confirms.',
      kind: 'Split',
      laneKey: 'reviewer',
      actor: 'reviewer',
      source: 'reviewer-assessment',
      roleGates: ['reviewer'],
      routes: [
        {
          id: 'reviewer-assessment--confirm-review--decision-join',
          target: 'decision-join',
          trigger: 'confirm review',
          requiresRole: 'reviewer',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'decision-join',
      displayName: 'Decision join',
      description: 'Wait for every branch to complete before releasing the next step.',
      kind: 'Join',
      laneKey: 'applicant',
      actor: 'applicant',
      roleGates: [],
      waiting: {
        allowDefer: false,
        content: 'Waiting for amendments, supporting evidence, and reviewer assessment before the decision can continue.',
      },
      routes: [
        {
          id: 'decision-join--continue--decision-confirmed',
          target: 'decision-confirmed',
          trigger: 'continue',
          actions: [],
        },
      ],
    },
  ],
};

export const PAYMENT_DEMO_WORKFLOW: AuthoredWorkflow = {
  definitionKey: 'payment-demo',
  displayName: 'Payment Demo',
  version: 1,
  schemaVersion: '1.0',
  instancePolicy: 'single',
  initialStageKey: 'enter-details',
  stages: [
    {
      stageKey: 'enter-details',
      displayName: 'Enter Payment Details',
      kind: 'Question',
      actor: 'applicant',
      actions: [],
      components: [
        {
          type: 'fieldset',
          legend: 'Enter Payment Details',
          children: [
            {
              type: 'text',
              fieldKey: 'cardholderName',
              label: 'Cardholder name',
              required: true,
            } satisfies AuthoredInputComponent,
            {
              type: 'decimal',
              fieldKey: 'amount',
              label: 'Amount (£)',
              required: true,
            } satisfies AuthoredInputComponent,
          ],
        },
      ],
      roleGates: [],
    },
    {
      stageKey: 'provider-processing',
      displayName: 'Provider processing',
      description: 'Payment provider processing and reconciliation work.',
      kind: 'Question',
      actor: 'payments',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stageKey: 'payment-complete',
      displayName: 'Payment Complete',
      description: 'Payment received. A receipt has been sent to your email address.',
      kind: 'Confirmation',
      actor: 'applicant',
      actions: [],
      components: [],
      roleGates: [],
    },
  ],
  gateways: [
    {
      gatewayKey: 'payment-submitted',
      displayName: 'Payment submitted',
      kind: 'Split',
      laneKey: 'applicant',
      actor: 'applicant',
      source: 'enter-details',
      roleGates: [],
      routes: [
        {
          id: 'enter-details--submit--payment-settled',
          target: 'payment-settled',
          trigger: 'submit',
          actions: [],
        },
        {
          id: 'enter-details--submit--provider-processing',
          target: 'provider-processing',
          trigger: 'submit',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'provider-route',
      displayName: 'Route from provider processing',
      kind: 'Split',
      laneKey: 'payments',
      actor: 'payments',
      source: 'provider-processing',
      roleGates: [],
      routes: [
        {
          id: 'provider-processing--complete--payment-settled',
          target: 'payment-settled',
          trigger: 'complete',
          requiresRole: 'reviewer',
          actions: [],
        },
      ],
    },
    {
      gatewayKey: 'payment-settled',
      displayName: 'Payment settled',
      kind: 'Join',
      laneKey: 'applicant',
      actor: 'applicant',
      roleGates: [],
      waiting: {
        content: 'Your payment is being processed right now.',
        expectedWaitSeconds: 30,
        pollIntervalMs: 5000,
        allowDefer: true,
        deferMessage: 'You can leave this page and return to your applications later. Your progress has been saved.',
      },
      routes: [
        {
          id: 'payment-settled--release--payment-complete',
          target: 'payment-complete',
          trigger: 'release',
          actions: [],
        },
      ],
    },
  ],
};
