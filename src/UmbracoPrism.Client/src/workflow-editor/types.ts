/**
 * TypeScript interfaces mirroring Blathers' C# AuthoredWorkflow model.
 * Field names match the camelCase JSON emitted by the projection API.
 * See docs/design/workflow-editor-v1/02-runtime-projection.md for the canonical C# shapes.
 */

// ---------------------------------------------------------------------------
// Authored Workflow
// ---------------------------------------------------------------------------

export interface AuthoredWorkflow {
  definitionKey: string;
  displayName: string;
  version: number;
  schemaVersion: string;
  instancePolicy: InstancePolicy;
  initialStageKey: string;
  stages: AuthoredStage[];
  transitions: AuthoredTransition[];
  roles: AuthoredRole[];
  fields: AuthoredField[];
  authorNote?: string;
}

export type InstancePolicy = 'Single' | 'Multiple' | 'Prompt';

// ---------------------------------------------------------------------------
// Authored Stage
// ---------------------------------------------------------------------------

export interface AuthoredStage {
  stageKey: string;
  displayName: string;
  kind: StageKind;
  views: AuthoredView[];
  roleGates: string[];
  exits: AuthoredExit[];
  waiting?: WaitingMetadata;
  editorComment?: string;
}

export type StageKind =
  | 'Capture'
  | 'Review'
  | 'Decision'
  | 'TaskList'
  | 'Waiting'
  | 'Confirmation'
  | 'Backstage'
  | 'Complete';

export interface AuthoredView {
  viewKey: string;
  audience: ViewAudience;
  fields: AuthoredFieldRef[];
}

export type ViewAudience = 'Public' | 'Member' | 'BusinessApp' | 'Operator';

export interface AuthoredFieldRef {
  fieldKey: string;
  labelOverride?: string;
  requiredOverride?: boolean;
}

export interface AuthoredExit {
  action: string;
  toStageKey: string;
  condition?: string;
  requiresRole?: string;
}

export interface WaitingMetadata {
  content?: string;
  expectedWaitSeconds?: number;
  pollIntervalMs?: number;
  allowDefer: boolean;
  deferMessage?: string;
}

// ---------------------------------------------------------------------------
// Authored Transition
// ---------------------------------------------------------------------------

export interface AuthoredTransition {
  fromStageKey: string;
  toStageKey: string;
  action: string;
  requiresRole?: string;
  condition?: string;
  editorComment?: string;
}

// ---------------------------------------------------------------------------
// Authored Role & Field
// ---------------------------------------------------------------------------

export interface AuthoredRole {
  roleKey: string;
  displayName: string;
  claimMapping?: string;
}

export interface AuthoredField {
  fieldKey: string;
  label: string;
  kind: FieldKind;
  required: boolean;
  hintText?: string;
  options: string[];
  editorComment?: string;
}

export type FieldKind =
  | 'TextInput'
  | 'Textarea'
  | 'Radios'
  | 'Checkboxes'
  | 'Select'
  | 'DateInput'
  | 'FileUpload'
  | 'Hidden';

// ---------------------------------------------------------------------------
// Proposal Envelope — Tangy's canonical schema
// See docs/design/workflow-editor-v1/04-agentic-surfaces.md §4
// ---------------------------------------------------------------------------

export interface ProposalEnvelope {
  id: string;
  createdAt: string;
  agent: ProposalAgent;
  targetWorkflowId: string;
  rationale: string;
  ops: ProposalOp[];
  placement: ProposalPlacement;
  validationResult: ValidationResult;
  previewArtifactRef?: string | null;
}

export interface ProposalAgent {
  kind: 'github-copilot' | 'custom-agent' | 'human-assisted';
  identity: string;
  sessionRef?: string;
}

export interface ProposalOp {
  op: 'insert-stage' | 'remove-stage' | 'update-stage' | 'insert-handoff' | 'update-transition';
  path: string;
  value?: unknown;
  before?: string;
  after?: string;
}

export interface ProposalPlacement {
  insertAfterStageKey?: string | null;
  insertBeforeStageKey?: string | null;
  handoffId?: string | null;
  transitionId?: string | null;
}

export interface ValidationResult {
  status: 'pass' | 'fail' | 'not-run';
  checkedAt: string | null;
  errors: string[];
}

// ---------------------------------------------------------------------------
// Stub data for Storybook / development
// ---------------------------------------------------------------------------

export const STUB_WORKFLOW: AuthoredWorkflow = {
  definitionKey: 'planning-permission',
  displayName: 'Planning Permission Application',
  version: 1,
  schemaVersion: '1.0',
  instancePolicy: 'Single',
  initialStageKey: 'applicant-details',
  stages: [
    {
      stageKey: 'applicant-details',
      displayName: 'Applicant Details',
      kind: 'Capture',
      views: [{ viewKey: 'public', audience: 'Public', fields: [] }],
      roleGates: [],
      exits: [{ action: 'submit', toStageKey: 'check-answers' }],
    },
    {
      stageKey: 'check-answers',
      displayName: 'Check Your Answers',
      kind: 'Review',
      views: [{ viewKey: 'public', audience: 'Public', fields: [] }],
      roleGates: [],
      exits: [{ action: 'submit', toStageKey: 'waiting-for-review' }],
    },
    {
      stageKey: 'waiting-for-review',
      displayName: 'Waiting for Review',
      kind: 'Waiting',
      views: [],
      roleGates: [],
      exits: [],
      waiting: {
        allowDefer: true,
        content: 'Your application is under review by a planning officer.',
        expectedWaitSeconds: 86400,
      },
    },
    {
      stageKey: 'reviewer-assessment',
      displayName: 'Reviewer Assessment',
      kind: 'Decision',
      views: [{ viewKey: 'reviewer', audience: 'BusinessApp', fields: [] }],
      roleGates: ['reviewer'],
      exits: [
        { action: 'approve', toStageKey: 'confirmation', requiresRole: 'reviewer' },
        { action: 'reject', toStageKey: 'applicant-details', requiresRole: 'reviewer' },
      ],
    },
    {
      stageKey: 'confirmation',
      displayName: 'Application Submitted',
      kind: 'Confirmation',
      views: [{ viewKey: 'public', audience: 'Public', fields: [] }],
      roleGates: [],
      exits: [],
    },
  ],
  transitions: [],
  roles: [{ roleKey: 'reviewer', displayName: 'Planning Officer' }],
  fields: [],
};

export const STUB_PROPOSAL: ProposalEnvelope = {
  id: 'a3f7c221-8b14-4e02-9d61-f23a10b5e7c9',
  createdAt: '2026-05-16T13:20:33.659+01:00',
  agent: {
    kind: 'github-copilot',
    identity: 'github-copilot/gpt-4o',
    sessionRef: 'copilot-session-2026-05-16-planning-idv',
  },
  targetWorkflowId: 'planning-permission',
  rationale:
    'Insert a mandatory external identity-and-verification (ID&V) stage between application submission and reviewer assessment. The ID&V step validates the applicant\'s identity with an external provider before the case is assigned to a planning officer.',
  ops: [
    {
      op: 'insert-stage',
      path: '/stages/2',
      before: 'reviewer-assessment',
    },
    {
      op: 'update-transition',
      path: '/transitions/0',
    },
  ],
  placement: {
    insertAfterStageKey: 'check-answers',
    insertBeforeStageKey: 'reviewer-assessment',
    handoffId: null,
    transitionId: null,
  },
  validationResult: {
    status: 'pass',
    checkedAt: '2026-05-16T13:20:33.659+01:00',
    errors: [],
  },
  previewArtifactRef: null,
};
