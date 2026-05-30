/**
 * Planning workflow fixture — shared between Core.Tests and Client.
 * The raw JSON is byte-aligned with:
 *   src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json
 */

import type {
  AuthoredAction,
  AuthoredField,
  AuthoredStage,
  AuthoredTransition,
  AuthoredWorkflow,
  FieldKind,
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

interface FixtureTransition {
  fromStage: string;
  toStage: string;
  action: string;
  actions?: FixtureAction[];
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
  transitions: FixtureTransition[];
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
  transitions: [
    { fromStage: 'declaration', toStage: 'application-form', action: 'continue', actions: [] },
    { fromStage: 'application-form', toStage: 'check-answers', action: 'continue', actions: [] },
    {
      fromStage: 'check-answers',
      toStage: 'submitted',
      action: 'submit',
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
      // Retired kinds (Waiting, StatusTimeline) and any unknown value collapse
      // to Question — PROJ140 rejects them on save, so the editor must not
      // surface them as valid choices.
      return 'Question';
  }
}

function mapFieldKind(raw: string): FieldKind {
  switch (raw) {
    case 'Text':
      return 'TextInput';
    case 'Textarea':
      return 'Textarea';
    case 'Select':
      return 'Select';
    case 'Radios':
      return 'Radios';
    case 'Checkboxes':
      return 'Checkboxes';
    case 'Date':
      return 'DateInput';
    case 'FileUpload':
      return 'FileUpload';
    case 'Hidden':
      return 'Hidden';
    default:
      return 'TextInput';
  }
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
  const transitions: AuthoredTransition[] = raw.transitions.map(t => ({
    fromStage: t.fromStage,
    toStage: t.toStage,
    action: t.action,
    actions: (t.actions ?? []).map(normaliseAction),
  }));

  const stages: AuthoredStage[] = raw.stages.map(s => {
    const fields: AuthoredField[] = s.fields.map(f => ({
      fieldKey: f.key,
      label: f.label,
      kind: mapFieldKind(f.type),
      required: f.required,
      hintText: f.hint,
      options: f.options,
    }));

    return {
      stageKey: s.stageKey,
      displayName: s.displayName,
      description: s.description,
      kind: mapKind(s.kind),
      actor: s.actor,
      actions: s.actions.map(normaliseAction),
      fields,
      roleGates: s.roleGates,
      editorComment: s.editorComment,
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
    transitions,
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
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'applicant-amendments',
      displayName: 'Applicant amendments',
      description: 'Applicant updates the request when more detail is needed.',
      kind: 'Question',
      actor: 'applicant',
      actions: [],
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'upload-evidence',
      displayName: 'Upload evidence',
      description: 'Applicant provides the supporting documents for the request.',
      kind: 'Question',
      actor: 'applicant',
      actions: [],
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'reviewer-assessment',
      displayName: 'Reviewer assessment',
      description: 'Reviewer checks the request before the service can continue.',
      kind: 'Question',
      actor: 'reviewer',
      actions: [],
      fields: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'decision-confirmed',
      displayName: 'Decision confirmed',
      description: 'The shared path continues here once every branch is complete.',
      kind: 'Confirmation',
      actor: 'applicant',
      actions: [],
      fields: [],
      roleGates: [],
    },
  ],
  transitions: [
    {
      fromStage: 'start-request',
      fromGateway: 'review-split',
      toStage: 'applicant-amendments',
      action: 'request amendments',
      actions: [],
    },
    {
      fromStage: 'start-request',
      fromGateway: 'review-split',
      toStage: 'upload-evidence',
      action: 'upload evidence',
      actions: [],
    },
    {
      fromStage: 'start-request',
      fromGateway: 'review-split',
      toStage: 'reviewer-assessment',
      action: 'send to reviewer',
      actions: [],
      requiresRole: 'reviewer',
    },
    {
      fromStage: 'applicant-amendments',
      toGateway: 'decision-join',
      toStage: 'decision-confirmed',
      action: 'finish amendments',
      actions: [],
    },
    {
      fromStage: 'upload-evidence',
      toGateway: 'decision-join',
      toStage: 'decision-confirmed',
      action: 'evidence complete',
      actions: [],
    },
    {
      fromStage: 'reviewer-assessment',
      toGateway: 'decision-join',
      toStage: 'decision-confirmed',
      action: 'confirm review',
      actions: [],
      requiresRole: 'reviewer',
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
      roleGates: [],
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
    },
  ],
};
