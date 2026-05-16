/**
 * Planning workflow fixture — shared between Core.Tests and Client.
 * The raw JSON is byte-aligned with:
 *   src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json
 *
 * `normalisePlanningFixture` converts the simplified fixture schema to the
 * client's AuthoredWorkflow type (views/exits/fieldKey conventions).
 */

import type {
  AuthoredWorkflow,
  AuthoredStage,
  AuthoredField,
  AuthoredTransition,
  StageKind,
  FieldKind,
  ViewAudience,
} from '../types.js';

// ---------------------------------------------------------------------------
// Raw fixture shape (mirrors the JSON on disk)
// ---------------------------------------------------------------------------

interface FixtureField {
  key: string;
  label: string;
  type: string;
  required: boolean;
  hint?: string;
  options: string[];
}

interface FixtureStage {
  stageKey: string;
  displayName: string;
  kind: string;
  actor?: string;
  fields: FixtureField[];
  roleGates: string[];
  editorComment?: string;
}

interface FixtureTransition {
  fromStage: string;
  toStage: string;
  action: string;
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

// ---------------------------------------------------------------------------
// Inline fixture data (byte-aligned with the JSON file)
// ---------------------------------------------------------------------------

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
      kind: 'Question',
      actor: 'applicant',
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
      kind: 'Question',
      actor: 'applicant',
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
      kind: 'CheckAnswers',
      actor: 'applicant',
      fields: [],
      roleGates: [],
      editorComment: 'Summary of all answers before final submission.',
    },
    {
      stageKey: 'submitted',
      displayName: 'Application submitted',
      kind: 'Confirmation',
      actor: 'applicant',
      fields: [],
      roleGates: [],
    },
  ],
  transitions: [
    { fromStage: 'declaration', toStage: 'application-form', action: 'continue' },
    { fromStage: 'application-form', toStage: 'check-answers', action: 'continue' },
    { fromStage: 'check-answers', toStage: 'submitted', action: 'submit' },
  ],
};

// ---------------------------------------------------------------------------
// Normalisation helpers
// ---------------------------------------------------------------------------

function mapKind(raw: string): StageKind {
  switch (raw) {
    case 'Question': return 'Capture';
    case 'CheckAnswers': return 'Review';
    case 'Confirmation': return 'Confirmation';
    case 'TaskList': return 'TaskList';
    case 'Waiting': return 'Waiting';
    case 'Decision': return 'Decision';
    default: return 'Backstage';
  }
}

function mapFieldKind(raw: string): FieldKind {
  switch (raw) {
    case 'Text': return 'TextInput';
    case 'Textarea': return 'Textarea';
    case 'Select': return 'Select';
    case 'Radios': return 'Radios';
    case 'Checkboxes': return 'Checkboxes';
    case 'Date': return 'DateInput';
    case 'FileUpload': return 'FileUpload';
    case 'Hidden': return 'Hidden';
    default: return 'TextInput';
  }
}

function mapActor(actor?: string): ViewAudience {
  switch (actor) {
    case 'caseworker':
    case 'reviewer':
      return 'BusinessApp';
    case 'operator': return 'Operator';
    default: return 'Public';
  }
}

function normalisePlanningFixture(raw: RawPlanningWorkflow): AuthoredWorkflow {
  // Collect all fields across stages into top-level fields array
  const allFields: AuthoredField[] = raw.stages.flatMap(s =>
    s.fields.map(f => ({
      fieldKey: f.key,
      label: f.label,
      kind: mapFieldKind(f.type),
      required: f.required,
      hintText: f.hint,
      options: f.options,
    }))
  );

  const transitions: AuthoredTransition[] = raw.transitions.map(t => ({
    fromStageKey: t.fromStage,
    toStageKey: t.toStage,
    action: t.action,
  }));

  const stages: AuthoredStage[] = raw.stages.map(s => {
    const audience = mapActor(s.actor);
    const exits = raw.transitions
      .filter(t => t.fromStage === s.stageKey)
      .map(t => ({ action: t.action, toStageKey: t.toStage }));

    return {
      stageKey: s.stageKey,
      displayName: s.displayName,
      kind: mapKind(s.kind),
      views: [
        {
          viewKey: s.actor ?? 'public',
          audience,
          fields: s.fields.map(f => ({ fieldKey: f.key })),
        },
      ],
      roleGates: s.roleGates,
      exits,
      editorComment: s.editorComment,
    };
  });

  return {
    definitionKey: raw.definitionKey,
    displayName: raw.displayName,
    version: raw.version,
    schemaVersion: raw.schemaVersion,
    instancePolicy: 'Single',
    initialStageKey: raw.initialStageKey,
    stages,
    transitions,
    roles: [],
    fields: allFields,
  };
}

export const PLANNING_WORKFLOW: AuthoredWorkflow = normalisePlanningFixture(RAW);
