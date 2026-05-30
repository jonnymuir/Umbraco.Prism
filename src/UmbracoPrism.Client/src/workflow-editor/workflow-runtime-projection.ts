import type { AuthoredAction, AuthoredField, AuthoredStage, AuthoredWorkflow } from './types.js';

export interface ProjectionDiagnostic {
  code: string;
  message: string;
  severity?: 'error' | 'warning' | 'info';
  stageKey?: string | null;
}

export interface ProjectedWorkflowDefinition {
  definitionKey: string;
  displayName: string;
  version: number;
  initialState: string;
  instancePolicy: string;
  states: ProjectedWorkflowState[];
  transitions: ProjectedWorkflowTransition[];
  metadata?: ProjectedWorkflowMetadata;
}

export interface ProjectWorkflowResult {
  file: ProjectedWorkflowDefinition;
  checksum: string;
  diagnostics: ProjectionDiagnostic[];
  hasErrors: boolean;
}

export interface ProjectedWorkflowState {
  stateKey: string;
  displayName: string;
  components: ProjectedComponent[];
  metadata?: ProjectedStateMetadata;
}

export interface ProjectedStateMetadata {
  description?: string;
  stageType?: string;
  actor?: string;
  roleGates?: string[];
  actions?: ProjectedActionDefinition[];
}

export interface ProjectedTransitionMetadata {
  conditions?: Array<{ kind: string; expression: string; description?: string }>;
  actions?: ProjectedActionDefinition[];
}

export interface ProjectedActionDefinition {
  type: string;
  timing: string;
  parameters: Record<string, unknown>;
  parameterSchemaKey?: string;
  summary?: string;
}

export interface ProjectedWorkflowTransition {
  fromState: string;
  toState: string;
  action: string;
  requiresRole?: string;
  metadata?: ProjectedTransitionMetadata;
}

export interface ProjectedWorkflowMetadata {
  authoredWorkflowId?: string;
  description?: string;
  schemaVersion?: string;
  tags?: Record<string, string>;
  handoffs?: Array<{
    id: string;
    fromState: string;
    toState: string;
    label: string;
    actorChange?: string;
  }>;
}

interface ProjectedComponentBase {
  type: string;
}

export interface ProjectedInputComponent extends ProjectedComponentBase {
  type:
    | 'text'
    | 'number'
    | 'decimal'
    | 'select'
    | 'radio'
    | 'checkboxlist'
    | 'date'
    | 'email'
    | 'textarea'
    | 'boolean';
  fieldKey: string;
  label: string;
  hint?: string;
  required: boolean;
  conditionalOn?: string | null;
  visibleWhen?: string | null;
  options?: string[];
  minLength?: number | null;
  maxLength?: number | null;
  pattern?: string | null;
  prefix?: string | null;
  min?: number | null;
  max?: number | null;
}

export interface ProjectedFieldsetComponent extends ProjectedComponentBase {
  type: 'fieldset';
  children: ProjectedComponent[];
  legend?: string | null;
  legendSize?: string | null;
}

export interface ProjectedAccordionComponent extends ProjectedComponentBase {
  type: 'accordion';
  sections: Array<{
    heading: string;
    summary?: string | null;
    children: ProjectedComponent[];
  }>;
}

export interface ProjectedPanelComponent extends ProjectedComponentBase {
  type: 'panel';
  heading: string;
}

export interface ProjectedWaitingComponent extends ProjectedComponentBase {
  type: 'waiting';
  content: string;
  expectedWaitSeconds: number;
  pollIntervalMs: number;
  allowDefer: boolean;
  deferMessage?: string;
}

export interface ProjectedSummaryListComponent extends ProjectedComponentBase {
  type: 'summary-list';
  children: ProjectedComponent[];
  changeStateKey?: string | null;
  title?: string | null;
}

export interface ProjectedTaskListComponent extends ProjectedComponentBase {
  type: 'task-list';
  sections?: Array<{
    heading: string;
    tasks: Array<{ label: string; stateKey?: string | null; href?: string | null }>;
  }> | null;
}

export interface ProjectedContentComponent extends ProjectedComponentBase {
  type: 'body' | 'heading' | 'inset-text' | 'warning-text' | 'details' | 'notification-banner';
  content?: string;
  heading?: string;
  level?: number;
  bannerType?: string;
}

export type ProjectedComponent =
  | ProjectedInputComponent
  | ProjectedFieldsetComponent
  | ProjectedAccordionComponent
  | ProjectedPanelComponent
  | ProjectedWaitingComponent
  | ProjectedSummaryListComponent
  | ProjectedTaskListComponent
  | ProjectedContentComponent;

export function projectWorkflowLocally(workflow: AuthoredWorkflow): ProjectWorkflowResult {
  const states = [...workflow.stages]
    .sort((left, right) => left.stageKey.localeCompare(right.stageKey))
    .map(stage => projectStage(stage, workflow));

  const transitions = [...workflow.transitions]
    .sort((left, right) =>
      left.fromStage.localeCompare(right.fromStage)
      || left.toStage.localeCompare(right.toStage)
      || left.action.localeCompare(right.action))
    .map(transition => ({
      fromState: transition.fromStage,
      toState: transition.toStage,
      action: transition.action,
      requiresRole: transition.requiresRole,
      metadata: {
        conditions: transition.condition
          ? [{ kind: 'expression', expression: transition.condition }]
          : undefined,
        actions: transition.actions?.map(projectAction),
      },
    }));

  const file: ProjectedWorkflowDefinition = {
    definitionKey: workflow.definitionKey,
    displayName: workflow.displayName,
    version: workflow.version,
    initialState: workflow.initialStageKey,
    instancePolicy: workflow.instancePolicy,
    states,
    transitions,
    metadata: {
      description: workflow.authorNote,
    },
  };

  return {
    file,
    checksum: computeChecksum(file),
    diagnostics: [],
    hasErrors: false,
  };
}

function projectStage(stage: AuthoredStage, workflow: AuthoredWorkflow): ProjectedWorkflowState {
  return {
    stateKey: stage.stageKey,
    displayName: stage.displayName,
    components: projectStageComponents(stage, workflow),
    metadata: {
      description: stage.description,
      stageType: stage.kind,
      actor: stage.actor,
      roleGates: stage.roleGates.length > 0 ? [...stage.roleGates] : undefined,
      actions: stage.actions?.map(projectAction),
    },
  };
}

function projectStageComponents(stage: AuthoredStage, workflow: AuthoredWorkflow): ProjectedComponent[] {
  switch (stage.kind) {
    case 'CheckAnswers':
      return [{
        type: 'summary-list',
        children: workflow.stages
          .filter(candidate => candidate.kind === 'Question')
          .sort((left, right) => left.stageKey.localeCompare(right.stageKey))
          .flatMap(candidate =>
            [...(candidate.fields ?? [])]
              .sort((left, right) => left.fieldKey.localeCompare(right.fieldKey))
              .map(projectField)),
      }];
    case 'Confirmation':
      return [{
        type: 'panel',
        heading: stage.displayName,
      }];
    case 'TaskList':
      return [{
        type: 'task-list',
        sections: null,
      }];
    case 'Question':
    default:
      return [{
        type: 'fieldset',
        children: [...(stage.fields ?? [])]
          .sort((left, right) => left.fieldKey.localeCompare(right.fieldKey))
          .map(projectField),
      }];
  }
}

function projectAction(action: AuthoredAction): ProjectedActionDefinition {
  return {
    type: action.type,
    timing: action.timing,
    parameters: { ...(action.params ?? {}) },
    parameterSchemaKey: action.parameterSchemaKey,
    summary: action.summary,
  };
}

function projectField(field: AuthoredField): ProjectedInputComponent {
  switch (field.kind) {
    case 'NumberInput':
      return input(field, 'number');
    case 'Select':
      return input(field, 'select', { options: [...field.options] });
    case 'Radios':
      return input(field, 'radio', { options: [...field.options] });
    case 'Checkboxes':
      return input(field, 'checkboxlist', { options: [...field.options] });
    case 'DateInput':
      return input(field, 'date');
    case 'EmailInput':
      return input(field, 'email', { pattern: field.validationPattern ?? null });
    case 'Toggle':
      return input(field, 'boolean');
    case 'Textarea':
      return input(field, 'textarea');
    case 'TextInput':
    case 'FileUpload':
    case 'Hidden':
    default:
      return input(field, 'text', { pattern: field.validationPattern ?? null });
  }
}

function input(
  field: AuthoredField,
  type: ProjectedInputComponent['type'],
  extras: Partial<ProjectedInputComponent> = {}
): ProjectedInputComponent {
  return {
    type,
    fieldKey: field.fieldKey,
    label: field.label,
    hint: field.hintText,
    required: field.required,
    conditionalOn: null,
    visibleWhen: null,
    options: 'options' in extras ? extras.options : [...field.options],
    ...extras,
  };
}

function computeChecksum(file: ProjectedWorkflowDefinition): string {
  const text = JSON.stringify(file);
  let hash = 0;
  for (let index = 0; index < text.length; index += 1) {
    hash = ((hash << 5) - hash) + text.charCodeAt(index);
    hash |= 0;
  }

  return `local-${Math.abs(hash).toString(16)}`;
}
