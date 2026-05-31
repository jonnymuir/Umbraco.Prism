/**
 * Wire-format helpers for HTTP-backed `WorkflowSource` implementations.
 *
 * The editor's TS authored model uses keys (`stageKey`, `displayName`, `kind`,
 * `fromStage`, …) that differ from the canonical wire shape Prism's projector
 * speaks (`key`, `title`, `type`, `source`, `target`, …).
 *
 * Hosts that store workflows as canonical JSON should call `serialiseWorkflow`
 * on save and `normaliseWorkflow` on load. `InMemoryWorkflowSource` does not
 * use these — it stores TS shape directly.
 */

import type {
  AuthoredAction,
  AuthoredField,
  AuthoredGateway,
  AuthoredStage,
  AuthoredTransition,
  AuthoredWorkflow,
  FieldKind,
  GatewayKind,
  StageKind,
} from './types.js';

function stripEditorOnlyStageSurface<T extends AuthoredStage>(stage: T): T {
  const { editorSurface: _editorSurface, ...rest } = stage as T & {
    editorSurface?: 'front-stage' | 'back-stage';
  };
  return rest as T;
}

function serialiseTransition(transition: AuthoredTransition): Record<string, unknown> {
  // Editor-only fields (fromGateway/toGateway) and TS-side aliases
  // (fromStage/toStage/action) never go on the wire.
  const {
    fromStage,
    toStage,
    action,
    fromGateway: _fromGateway,
    toGateway: _toGateway,
    ...rest
  } = transition;
  return {
    ...rest,
    source: fromStage,
    target: toStage,
    trigger: action,
  };
}

function serialiseStage(stage: AuthoredStage): Record<string, unknown> {
  const stripped = stripEditorOnlyStageSurface(stage);
  const { stageKey, displayName, kind, ...rest } = stripped as AuthoredStage & Record<string, unknown>;
  return {
    ...rest,
    key: stageKey,
    title: displayName,
    type: kind,
  };
}

function serialiseGateway(gateway: AuthoredGateway): Record<string, unknown> {
  const { gatewayKey, displayName, kind, ...rest } = gateway as AuthoredGateway & Record<string, unknown>;
  return {
    ...rest,
    key: gatewayKey,
    title: displayName,
    type: kind,
  };
}

function serialiseField(field: AuthoredField): Record<string, unknown> {
  const { fieldKey, kind, hintText, ...rest } = field as AuthoredField & Record<string, unknown>;
  return {
    ...rest,
    key: fieldKey,
    type: kind,
    hint: hintText,
  };
}

export function serialiseWorkflow(workflow: AuthoredWorkflow): Record<string, unknown> {
  return {
    ...workflow,
    stages: workflow.stages.map(stage => {
      const serialised = serialiseStage(stage);
      if (Array.isArray((serialised as { fields?: unknown[] }).fields)) {
        (serialised as { fields: unknown[] }).fields =
          ((serialised as { fields: unknown[] }).fields as AuthoredField[]).map(serialiseField);
      }
      return serialised;
    }),
    transitions: workflow.transitions.map(serialiseTransition),
    gateways: Array.isArray(workflow.gateways)
      ? workflow.gateways.map(serialiseGateway)
      : workflow.gateways,
  };
}

function mapStageKind(raw: string | undefined): StageKind {
  if (raw === undefined || raw === '') {
    return 'Question';
  }
  switch (raw) {
    case 'Question':
    case 'CheckAnswers':
    case 'Confirmation':
    case 'TaskList':
      return raw;
    default:
      throw new Error(
        `Unknown stage kind "${raw}". Allowed kinds: Question, CheckAnswers, Confirmation, TaskList.`
      );
  }
}

function mapFieldKind(raw: string | undefined): FieldKind {
  switch (raw) {
    case 'Number':
    case 'Decimal':
      return 'NumberInput';
    case 'Email':
      return 'EmailInput';
    case 'Textarea':
      return 'Textarea';
    case 'Radios':
      return 'Radios';
    case 'Checkboxes':
      return 'Checkboxes';
    case 'Select':
      return 'Select';
    case 'Date':
    case 'DateInput':
      return 'DateInput';
    case 'Boolean':
      return 'Toggle';
    case 'FileUpload':
      return 'FileUpload';
    case 'Hidden':
      return 'Hidden';
    default:
      return 'TextInput';
  }
}

function mapGatewayKind(raw: string | undefined): GatewayKind {
  switch (raw) {
    case 'Join':
      return 'Join';
    case 'Split':
    default:
      return 'Split';
  }
}

function normaliseField(raw: Record<string, unknown>): AuthoredField {
  return {
    fieldKey: String(raw.key ?? ''),
    label: String(raw.label ?? ''),
    kind: mapFieldKind(typeof raw.type === 'string' ? raw.type : undefined),
    required: Boolean(raw.required),
    hintText: typeof raw.hint === 'string' ? raw.hint : undefined,
    validationPattern:
      typeof raw.validationPattern === 'string' ? raw.validationPattern : undefined,
    defaultValue: raw.defaultValue,
    options: Array.isArray(raw.options) ? raw.options.map(value => String(value)) : [],
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

function normaliseAction(raw: Record<string, unknown>): AuthoredAction {
  return {
    type: String(raw.type ?? ''),
    timing: (typeof raw.timing === 'string' ? raw.timing : 'OnEntry') as AuthoredAction['timing'],
    params:
      typeof raw.params === 'object' && raw.params !== null ? (raw.params as Record<string, unknown>) : {},
    parameterSchemaKey: typeof raw.parameterSchemaKey === 'string' ? raw.parameterSchemaKey : undefined,
    summary: typeof raw.summary === 'string' ? raw.summary : undefined,
  };
}

function normaliseStage(raw: Record<string, unknown>): AuthoredStage {
  const kind = mapStageKind(typeof raw.type === 'string' ? raw.type : undefined);
  return {
    stageKey: String(raw.key ?? ''),
    displayName: String(raw.title ?? ''),
    description: typeof raw.description === 'string' ? raw.description : undefined,
    kind,
    actor: typeof raw.actor === 'string' ? raw.actor : undefined,
    actions: Array.isArray(raw.actions)
      ? raw.actions.map(action => normaliseAction(action as Record<string, unknown>))
      : [],
    fields: Array.isArray(raw.fields)
      ? raw.fields.map(field => normaliseField(field as Record<string, unknown>))
      : [],
    roleGates: Array.isArray(raw.roleGates) ? raw.roleGates.map(value => String(value)) : [],
    waiting:
      typeof raw.waiting === 'object' && raw.waiting !== null
        ? (raw.waiting as AuthoredStage['waiting'])
        : undefined,
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

function normaliseGateway(raw: Record<string, unknown>): AuthoredGateway {
  return {
    gatewayKey: String(raw.key ?? ''),
    displayName: String(raw.title ?? ''),
    description: typeof raw.description === 'string' ? raw.description : undefined,
    kind: mapGatewayKind(typeof raw.type === 'string' ? raw.type : undefined),
    laneKey: typeof raw.laneKey === 'string' ? raw.laneKey : undefined,
    actor: typeof raw.actor === 'string' ? raw.actor : undefined,
    roleGates: Array.isArray(raw.roleGates) ? raw.roleGates.map(value => String(value)) : [],
    waiting:
      typeof raw.waiting === 'object' && raw.waiting !== null
        ? (raw.waiting as AuthoredGateway['waiting'])
        : undefined,
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

function normaliseTransition(raw: Record<string, unknown>): AuthoredTransition {
  const firstCondition =
    Array.isArray(raw.conditions) &&
    raw.conditions.length > 0 &&
    typeof raw.conditions[0] === 'object' &&
    raw.conditions[0] !== null
      ? (raw.conditions[0] as Record<string, unknown>)
      : null;

  return {
    fromStage: String(raw.source ?? ''),
    toStage: String(raw.target ?? ''),
    action: String(raw.trigger ?? ''),
    actions: Array.isArray(raw.actions)
      ? raw.actions.map(action => normaliseAction(action as Record<string, unknown>))
      : [],
    requiresRole: typeof raw.requiresRole === 'string' ? raw.requiresRole : undefined,
    condition:
      typeof raw.condition === 'string'
        ? raw.condition
        : typeof firstCondition?.expression === 'string'
          ? firstCondition.expression
          : undefined,
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

export function normaliseWorkflow(raw: Record<string, unknown>): AuthoredWorkflow {
  return {
    definitionKey: String(raw.definitionKey ?? ''),
    displayName: String(raw.displayName ?? ''),
    version: typeof raw.version === 'number' ? raw.version : 1,
    schemaVersion: String(raw.schemaVersion ?? '1.0'),
    instancePolicy: String(raw.instancePolicy ?? 'single'),
    initialStageKey: String(raw.initialStageKey ?? ''),
    stages: Array.isArray(raw.stages)
      ? raw.stages.map(stage => normaliseStage(stage as Record<string, unknown>))
      : [],
    transitions: Array.isArray(raw.transitions)
      ? raw.transitions.map(transition => normaliseTransition(transition as Record<string, unknown>))
      : [],
    gateways: Array.isArray(raw.gateways)
      ? raw.gateways.map(gateway => normaliseGateway(gateway as Record<string, unknown>))
      : [],
    authorNote: typeof raw.authorNote === 'string' ? raw.authorNote : undefined,
  };
}
