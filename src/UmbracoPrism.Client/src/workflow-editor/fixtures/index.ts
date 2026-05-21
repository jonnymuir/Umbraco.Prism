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
    case 'Waiting':
      return 'Waiting';
    case 'StatusTimeline':
      return 'StatusTimeline';
    default:
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
