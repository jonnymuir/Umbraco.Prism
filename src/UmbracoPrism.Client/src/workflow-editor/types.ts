/**
 * TypeScript interfaces mirroring Blathers' C# AuthoredWorkflow model.
 * Field names match the camelCase JSON emitted by the projection API
 * (WorkflowProjector.CanonicalOptions: PropertyNamingPolicy = CamelCase).
 *
 * Key schema decisions (decisions.md 2026-05-16):
 *  - Stages carry their fields directly (no `views` wrapper) — matches C# AuthoredStage.Fields
 *  - Transitions live at the workflow level only — no `exits` on stages
 *  - StageKind values are PascalCase per JsonStringEnumConverter on the C# enum
 *  - AuthoredTransition uses fromStage/toStage (not fromStageKey/toStageKey)
 */

// ---------------------------------------------------------------------------
// Authored Workflow
// ---------------------------------------------------------------------------

export interface AuthoredWorkflow {
  definitionKey: string;
  displayName: string;
  version: number;
  schemaVersion: string;
  instancePolicy: string;
  initialStageKey: string;
  stages: AuthoredStage[];
  transitions: AuthoredTransition[];
  /** Client-side convenience — not present in C# AuthoredWorkflow; guard all accesses. */
  roles?: AuthoredRole[];
  authorNote?: string;
}

// ---------------------------------------------------------------------------
// Authored Stage
// ---------------------------------------------------------------------------

export interface AuthoredStage {
  stageKey: string;
  displayName: string;
  kind: StageKind;
  /** Actor/persona for this stage (informational). */
  actor?: string;
  /** Fields collected at this stage — matches C# AuthoredStage.Fields. */
  fields?: AuthoredField[];
  roleGates: string[];
  waiting?: WaitingMetadata;
  editorComment?: string;
}

/**
 * Shell intent enum — mirrors C# StageKind with JsonStringEnumConverter (PascalCase).
 * Valid values: Question | CheckAnswers | Confirmation | TaskList | Waiting | StatusTimeline
 */
export type StageKind =
  | 'Question'
  | 'CheckAnswers'
  | 'Confirmation'
  | 'TaskList'
  | 'Waiting'
  | 'StatusTimeline';

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

/**
 * Directed edge in the workflow graph.
 * Field names match C# AuthoredTransition serialised with camelCase naming policy:
 *   fromStage / toStage (NOT fromStageKey / toStageKey).
 */
export interface AuthoredTransition {
  fromStage: string;
  toStage: string;
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
  instancePolicy: 'single',
  initialStageKey: 'applicant-details',
  stages: [
    {
      stageKey: 'applicant-details',
      displayName: 'Applicant Details',
      kind: 'Question',
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'check-answers',
      displayName: 'Check Your Answers',
      kind: 'CheckAnswers',
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'waiting-for-review',
      displayName: 'Waiting for Review',
      kind: 'Waiting',
      fields: [],
      roleGates: [],
      waiting: {
        allowDefer: true,
        content: 'Your application is under review by a planning officer.',
        expectedWaitSeconds: 86400,
      },
    },
    {
      stageKey: 'reviewer-assessment',
      displayName: 'Reviewer Assessment',
      kind: 'Question',
      fields: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'confirmation',
      displayName: 'Application Submitted',
      kind: 'Confirmation',
      fields: [],
      roleGates: [],
    },
  ],
  transitions: [
    { fromStage: 'applicant-details', toStage: 'check-answers', action: 'submit' },
    { fromStage: 'check-answers', toStage: 'waiting-for-review', action: 'submit' },
    { fromStage: 'reviewer-assessment', toStage: 'confirmation', action: 'approve', requiresRole: 'reviewer' },
    { fromStage: 'reviewer-assessment', toStage: 'applicant-details', action: 'reject', requiresRole: 'reviewer' },
  ],
  /** roles is a client-side convenience — not in the C# schema */
  roles: [{ roleKey: 'reviewer', displayName: 'Planning Officer' }],
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
