/**
 * TypeScript interfaces mirroring Blathers' C# AuthoredWorkflow model.
 * Field names match the camelCase JSON emitted by the projection API
 * (WorkflowProjector.CanonicalOptions: PropertyNamingPolicy = CamelCase).
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
  description?: string;
  kind: StageKind;
  /** Actor/persona for this stage (informational). */
  actor?: string;
  /** Typed stage actions from the authoring catalog. */
  actions?: AuthoredAction[];
  /** Fields collected at this stage — matches C# AuthoredStage.Fields. */
  fields?: AuthoredField[];
  roleGates: string[];
  waiting?: WaitingMetadata;
  editorComment?: string;
}

export type StageKind =
  | 'Question'
  | 'CheckAnswers'
  | 'Confirmation'
  | 'TaskList'
  | 'Waiting'
  | 'StatusTimeline';

export type EditorStageType =
  | 'form'
  | 'review'
  | 'decision'
  | 'waiting'
  | 'confirmation'
  | 'system-work';

export function stageKindToEditorStageType(kind: StageKind): EditorStageType {
  switch (kind) {
    case 'CheckAnswers':
      return 'review';
    case 'TaskList':
      return 'decision';
    case 'Waiting':
      return 'waiting';
    case 'Confirmation':
      return 'confirmation';
    case 'StatusTimeline':
      return 'system-work';
    case 'Question':
    default:
      return 'form';
  }
}

export function editorStageTypeToStageKind(type: EditorStageType): StageKind {
  switch (type) {
    case 'review':
      return 'CheckAnswers';
    case 'decision':
      return 'TaskList';
    case 'waiting':
      return 'Waiting';
    case 'confirmation':
      return 'Confirmation';
    case 'system-work':
      return 'StatusTimeline';
    case 'form':
    default:
      return 'Question';
  }
}

export type EditorActor = 'public' | 'member' | 'reviewer' | 'system';

export function actorToEditorActor(actor?: string): EditorActor {
  const normalised = actor?.trim().toLowerCase() ?? '';

  if (!normalised || ['public', 'applicant', 'resident', 'citizen', 'customer'].includes(normalised)) {
    return 'public';
  }

  if (normalised === 'member') {
    return 'member';
  }

  if (['reviewer', 'caseworker', 'officer', 'administrator', 'admin'].includes(normalised)) {
    return 'reviewer';
  }

  if (normalised === 'system') {
    return 'system';
  }

  return normalised.includes('review') || normalised.includes('case') ? 'reviewer' : 'public';
}

export function editorActorToActor(actor: EditorActor): string {
  switch (actor) {
    case 'member':
      return 'member';
    case 'reviewer':
      return 'reviewer';
    case 'system':
      return 'system';
    case 'public':
    default:
      return 'public';
  }
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
  fromStage: string;
  toStage: string;
  action: string;
  actions?: AuthoredAction[];
  requiresRole?: string;
  condition?: string;
  editorComment?: string;
}

// ---------------------------------------------------------------------------
// Authored Action Catalog
// ---------------------------------------------------------------------------

export type ActionTiming = 'OnEntry' | 'OnExit' | 'OnTransition';

export interface AuthoredAction {
  type: string;
  timing: ActionTiming;
  params?: Record<string, unknown>;
  parameterSchemaKey?: string;
  summary?: string;
}

export type ParameterValueKind = 'String' | 'Number' | 'Integer' | 'Boolean' | 'Object' | 'Array' | 'Null';

export interface AuthoredParameterDefinition {
  key: string;
  title: string;
  description?: string;
  valueKind: ParameterValueKind;
  format?: string;
  editor?: string;
  allowedValues?: string[];
  defaultValue?: unknown;
  properties?: AuthoredParameterDefinition[];
  items?: AuthoredParameterDefinition | null;
}

export interface AuthoredParameterSchema {
  key: string;
  title: string;
  description?: string;
  appliesTo?: string[];
  valueKind?: ParameterValueKind;
  allowAdditionalProperties?: boolean;
  properties?: AuthoredParameterDefinition[];
  required?: string[];
}

export interface ActionCatalogEntry {
  type: string;
  label: string;
  summary: string;
  appliesTo: string[];
  paramsSchema: AuthoredParameterSchema;
  parameterWidgets?: Record<string, string>;
  defaultParams?: Record<string, unknown>;
  status?: string;
  runtimeImplementation?: string;
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
  validationPattern?: string;
  defaultValue?: unknown;
  options: string[];
  editorComment?: string;
}

export type FieldKind =
  | 'TextInput'
  | 'NumberInput'
  | 'Textarea'
  | 'Radios'
  | 'Checkboxes'
  | 'Select'
  | 'DateInput'
  | 'EmailInput'
  | 'Toggle'
  | 'FileUpload'
  | 'Hidden';

export type ActionFormFieldType = 'text' | 'number' | 'textarea' | 'select' | 'radio' | 'date';

export interface ActionFormFieldConfig {
  fieldKey: string;
  label: string;
  type: ActionFormFieldType;
  required: boolean;
  hintText?: string;
  validationPattern?: string;
  defaultValue?: string;
  options: string[];
}

// ---------------------------------------------------------------------------
// Proposal Envelope — Tangy's canonical schema
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

export const STUB_ACTION_CATALOG: ActionCatalogEntry[] = [
  {
    type: 'forms.load',
    label: 'Load form',
    summary: 'Load a forms-engine definition when a stage opens.',
    appliesTo: ['stage.onEntry'],
    paramsSchema: {
      key: 'forms.form-reference',
      title: 'Forms engine reference',
      valueKind: 'Object',
      properties: [
        {
          key: 'formDefinitionId',
          title: 'Form definition id',
          valueKind: 'String',
          editor: 'text',
        },
      ],
      required: ['formDefinitionId'],
    },
    defaultParams: { formDefinitionId: '' },
    status: 'available',
    runtimeImplementation: 'reference-business-app',
  },
  {
    type: 'forms.save',
    label: 'Save form',
    summary: 'Persist the current forms-engine payload before leaving a stage.',
    appliesTo: ['stage.onExit'],
    paramsSchema: {
      key: 'forms.form-reference',
      title: 'Forms engine reference',
      valueKind: 'Object',
      properties: [
        {
          key: 'formDefinitionId',
          title: 'Form definition id',
          valueKind: 'String',
          editor: 'text',
        },
      ],
      required: ['formDefinitionId'],
    },
    defaultParams: { formDefinitionId: '' },
    status: 'available',
    runtimeImplementation: 'reference-business-app',
  },
  {
    type: 'forms.submit',
    label: 'Submit form',
    summary: 'Validate and submit a forms-engine definition while taking a transition.',
    appliesTo: ['transition'],
    paramsSchema: {
      key: 'forms.form-reference',
      title: 'Forms engine reference',
      valueKind: 'Object',
      properties: [
        {
          key: 'formDefinitionId',
          title: 'Form definition id',
          valueKind: 'String',
          editor: 'text',
        },
      ],
      required: ['formDefinitionId'],
    },
    defaultParams: { formDefinitionId: '' },
    status: 'available',
    runtimeImplementation: 'reference-business-app',
  },
  {
    type: 'case.assign',
    label: 'Assign case',
    summary: 'Assign the current case to a role, queue, or named user.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'case.assign',
      title: 'Case assignment',
      valueKind: 'Object',
      properties: [
        {
          key: 'assigneeType',
          title: 'Assignment target type',
          valueKind: 'String',
          editor: 'select',
          allowedValues: ['role', 'queue', 'user'],
          defaultValue: 'role',
        },
        { key: 'assigneeValue', title: 'Assignment target', valueKind: 'String', editor: 'text' },
        {
          key: 'overwriteExisting',
          title: 'Overwrite existing assignment',
          valueKind: 'Boolean',
          editor: 'toggle',
          defaultValue: false,
        },
      ],
      required: ['assigneeType', 'assigneeValue'],
    },
    defaultParams: { assigneeType: 'role', assigneeValue: '', overwriteExisting: false },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'case.enqueue',
    label: 'Enqueue case',
    summary: 'Place the case into a named queue with an optional priority.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'case.enqueue',
      title: 'Queue placement',
      valueKind: 'Object',
      properties: [
        { key: 'queue', title: 'Queue', valueKind: 'String', editor: 'text' },
        {
          key: 'priority',
          title: 'Priority',
          valueKind: 'String',
          editor: 'select',
          allowedValues: ['low', 'normal', 'high'],
          defaultValue: 'normal',
        },
      ],
      required: ['queue'],
    },
    defaultParams: { queue: '', priority: 'normal' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'case.set-status',
    label: 'Set case status',
    summary: 'Update the case status shown to staff and applicants.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'case.set-status',
      title: 'Case status',
      valueKind: 'Object',
      properties: [
        { key: 'status', title: 'Status', valueKind: 'String', editor: 'text' },
        { key: 'reason', title: 'Reason', valueKind: 'String', editor: 'textarea' },
      ],
      required: ['status'],
    },
    defaultParams: { status: '', reason: '' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'case.add-note',
    label: 'Add case note',
    summary: 'Attach an internal or public note to the current case.',
    appliesTo: ['stage.onExit', 'transition'],
    paramsSchema: {
      key: 'case.add-note',
      title: 'Case note',
      valueKind: 'Object',
      properties: [
        { key: 'note', title: 'Note', valueKind: 'String', editor: 'textarea' },
        {
          key: 'visibility',
          title: 'Visibility',
          valueKind: 'String',
          editor: 'select',
          allowedValues: ['internal', 'public'],
          defaultValue: 'internal',
        },
      ],
      required: ['note'],
    },
    defaultParams: { note: '', visibility: 'internal' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'notifications.send-email',
    label: 'Send email',
    summary: 'Queue an email notification using a named template.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'notifications.send-email',
      title: 'Email notification',
      valueKind: 'Object',
      properties: [
        { key: 'templateId', title: 'Template id', valueKind: 'String', editor: 'text' },
        {
          key: 'recipientEmail',
          title: 'Recipient email',
          valueKind: 'String',
          format: 'email',
          editor: 'text',
        },
        { key: 'subject', title: 'Subject override', valueKind: 'String', editor: 'text' },
      ],
      required: ['templateId', 'recipientEmail'],
    },
    defaultParams: { templateId: '', recipientEmail: '', subject: '' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'notifications.send-sms',
    label: 'Send SMS',
    summary: 'Queue an SMS notification using a named template.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'notifications.send-sms',
      title: 'SMS notification',
      valueKind: 'Object',
      properties: [
        { key: 'templateId', title: 'Template id', valueKind: 'String', editor: 'text' },
        { key: 'recipientNumber', title: 'Recipient number', valueKind: 'String', editor: 'text' },
      ],
      required: ['templateId', 'recipientNumber'],
    },
    defaultParams: { templateId: '', recipientNumber: '' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'forms.request-evidence',
    label: 'Request evidence form',
    summary: 'Ask the applicant for supporting evidence using a configured response form.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'forms.request-evidence',
      title: 'Evidence request',
      valueKind: 'Object',
      properties: [
        { key: 'title', title: 'Prompt title', valueKind: 'String', editor: 'text' },
        { key: 'helpText', title: 'Intro help text', valueKind: 'String', editor: 'textarea' },
        { key: 'dueDate', title: 'Due date', valueKind: 'String', format: 'date', editor: 'date' },
        {
          key: 'fields',
          title: 'Fields',
          valueKind: 'Array',
          editor: 'collection',
          items: {
            key: 'field',
            title: 'Field',
            valueKind: 'Object',
            properties: [
              { key: 'fieldKey', title: 'Field key', valueKind: 'String', editor: 'text' },
              { key: 'label', title: 'Label', valueKind: 'String', editor: 'text' },
              {
                key: 'type',
                title: 'Field type',
                valueKind: 'String',
                editor: 'select',
                allowedValues: ['text', 'number', 'textarea', 'select', 'radio', 'date'],
                defaultValue: 'text',
              },
              { key: 'required', title: 'Required', valueKind: 'Boolean', editor: 'toggle', defaultValue: false },
              { key: 'hintText', title: 'Help text', valueKind: 'String', editor: 'textarea' },
              { key: 'validationPattern', title: 'Validation pattern', valueKind: 'String', editor: 'text' },
              { key: 'defaultValue', title: 'Default value', valueKind: 'String', editor: 'text' },
              {
                key: 'options',
                title: 'Options',
                valueKind: 'Array',
                editor: 'collection',
                items: {
                  key: 'option',
                  title: 'Option',
                  valueKind: 'String',
                  editor: 'text',
                },
              },
            ],
          },
        },
      ],
      required: ['title', 'fields'],
    },
    defaultParams: {
      title: 'Request supporting evidence',
      helpText: 'Explain what evidence the applicant should upload or complete.',
      dueDate: '',
      fields: [
        {
          fieldKey: 'supporting-evidence',
          label: 'Supporting evidence',
          type: 'select',
          required: true,
          hintText: 'Choose the evidence the applicant needs to provide.',
          validationPattern: '',
          defaultValue: '',
          options: ['Site photos', 'Ownership certificate', 'Tree survey'],
        },
      ],
    },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
];

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
      description: 'Collect applicant details and site context.',
      kind: 'Question',
      actor: 'public',
      actions: [
        {
          type: 'forms.load',
          timing: 'OnEntry',
          parameterSchemaKey: 'forms.form-reference',
          params: { formDefinitionId: 'planning-applicant-details' },
          summary: 'Load the applicant details form.',
        },
        {
          type: 'notifications.send-email',
          timing: 'OnEntry',
          parameterSchemaKey: 'notifications.send-email',
          params: {
            templateId: 'planning-started',
            recipientEmail: 'planning.officers@council.example',
            subject: 'Planning application started',
          },
          summary: 'Send email to Planning Officers',
        },
      ],
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'check-answers',
      displayName: 'Check Your Answers',
      description: 'Review the captured answers before submission.',
      kind: 'CheckAnswers',
      actor: 'public',
      actions: [],
      fields: [],
      roleGates: [],
    },
    {
      stageKey: 'waiting-for-review',
      displayName: 'Waiting for Review',
      description: 'Pause while the planning team picks up the case.',
      kind: 'Waiting',
      actor: 'public',
      actions: [],
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
      description: 'Internal assessment and decision making.',
      kind: 'Question',
      actor: 'reviewer',
      actions: [
        {
          type: 'case.assign',
          timing: 'OnEntry',
          parameterSchemaKey: 'case.assign',
          params: { assigneeType: 'role', assigneeValue: 'reviewer', overwriteExisting: false },
          summary: 'Assign the case to a reviewer.',
        },
        {
          type: 'forms.request-evidence',
          timing: 'OnEntry',
          parameterSchemaKey: 'forms.request-evidence',
          params: {
            title: 'Request supporting evidence',
            helpText: 'Capture any extra evidence the reviewer needs before deciding.',
            dueDate: '',
            fields: [
              {
                fieldKey: 'decision-note',
                label: 'Decision note',
                type: 'textarea',
                required: true,
                hintText: 'Explain why the reviewer is requesting more evidence.',
                validationPattern: '',
                defaultValue: '',
                options: [],
              },
            ],
          },
          summary: 'Request evidence form: 1 field',
        },
      ],
      fields: [],
      roleGates: ['reviewer'],
    },
    {
      stageKey: 'confirmation',
      displayName: 'Application Submitted',
      description: 'Confirm the application has been submitted.',
      kind: 'Confirmation',
      actor: 'public',
      actions: [],
      fields: [],
      roleGates: [],
    },
  ],
  transitions: [
    {
      fromStage: 'applicant-details',
      toStage: 'check-answers',
      action: 'submit',
      actions: [
        {
          type: 'forms.submit',
          timing: 'OnTransition',
          parameterSchemaKey: 'forms.form-reference',
          params: { formDefinitionId: 'planning-applicant-details' },
          summary: 'Submit the applicant details form.',
        },
      ],
    },
    { fromStage: 'check-answers', toStage: 'waiting-for-review', action: 'submit', actions: [] },
    { fromStage: 'reviewer-assessment', toStage: 'confirmation', action: 'approve', requiresRole: 'reviewer', actions: [] },
    { fromStage: 'reviewer-assessment', toStage: 'applicant-details', action: 'reject', requiresRole: 'reviewer', actions: [] },
  ],
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
