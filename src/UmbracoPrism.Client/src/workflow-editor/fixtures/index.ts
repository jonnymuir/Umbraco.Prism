/**
 * Planning workflow fixture — shared between Core.Tests and Client.
 * The raw JSON is byte-aligned with:
 *   src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json
 *
 * `normalisePlanningFixture` maps the raw fixture field names to the
 * TypeScript AuthoredWorkflow types (camelCase, FieldKind mapping).
 * Stages carry their fields directly; transitions live at the workflow level.
 */

import type {
  AuthoredWorkflow,
  AuthoredStage,
  AuthoredField,
  AuthoredTransition,
  StageKind,
  FieldKind,
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
    case 'Question':       return 'Question';
    case 'CheckAnswers':   return 'CheckAnswers';
    case 'Confirmation':   return 'Confirmation';
    case 'TaskList':       return 'TaskList';
    case 'Waiting':        return 'Waiting';
    case 'StatusTimeline': return 'StatusTimeline';
    default:               return 'Question';
  }
}

function mapFieldKind(raw: string): FieldKind {
  switch (raw) {
    case 'Text':        return 'TextInput';
    case 'Textarea':    return 'Textarea';
    case 'Select':      return 'Select';
    case 'Radios':      return 'Radios';
    case 'Checkboxes':  return 'Checkboxes';
    case 'Date':        return 'DateInput';
    case 'FileUpload':  return 'FileUpload';
    case 'Hidden':      return 'Hidden';
    default:            return 'TextInput';
  }
}

function normalisePlanningFixture(raw: RawPlanningWorkflow): AuthoredWorkflow {
  const transitions: AuthoredTransition[] = raw.transitions.map(t => ({
    fromStage: t.fromStage,
    toStage: t.toStage,
    action: t.action,
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
      kind: mapKind(s.kind),
      actor: s.actor,
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
