import type {
  AuthoredAction,
  AuthoredAccordionComponent,
  AuthoredComponent,
  AuthoredContentComponent,
  AuthoredFieldsetComponent,
  AuthoredInputComponent,
  AuthoredPanelComponent,
  AuthoredStage,
  AuthoredSummaryListComponent,
  AuthoredTaskListComponent,
  AuthoredWaitingComponent,
  AuthoredWorkflow,
} from './types.js';
import { flattenRoutes } from './workflow-routes.js';

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

// Authored components and projected components are the same shape — the
// projector is a pass-through. The Projected* aliases are kept for backwards
// compatibility with editor surfaces that import them.
export type ProjectedInputComponent = AuthoredInputComponent;
export type ProjectedFieldsetComponent = AuthoredFieldsetComponent;
export type ProjectedAccordionComponent = AuthoredAccordionComponent;
export type ProjectedPanelComponent = AuthoredPanelComponent;
export type ProjectedWaitingComponent = AuthoredWaitingComponent;
export type ProjectedSummaryListComponent = AuthoredSummaryListComponent;
export type ProjectedTaskListComponent = AuthoredTaskListComponent;
export type ProjectedContentComponent = AuthoredContentComponent;
export type ProjectedComponent = AuthoredComponent;

export function projectWorkflowLocally(workflow: AuthoredWorkflow): ProjectWorkflowResult {
  const states = [...workflow.stages]
    .sort((left, right) => left.stageKey.localeCompare(right.stageKey))
    .map(stage => projectStage(stage, workflow));

  const transitions = flattenRoutes(workflow)
    .slice()
    .sort((left, right) =>
      left.fromStage.localeCompare(right.fromStage)
      || left.toStage.localeCompare(right.toStage)
      || left.action.localeCompare(right.action))
    .map(view => ({
      fromState: view.fromStage,
      toState: view.toStage,
      action: view.action,
      requiresRole: view.requiresRole,
      metadata: {
        conditions: view.condition
          ? [{ kind: 'expression', expression: view.condition }]
          : undefined,
        actions: view.actions?.map(projectAction),
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
  // Authored components are the source of truth — pass through as the runtime
  // shape directly. When a stage declares no components, fall back to a
  // sensible kind-based default so empty stages still render as the right
  // shell. This mirrors WorkflowProjector.EmitComponents on the C# side.
  if (stage.components && stage.components.length > 0) {
    return [...stage.components];
  }

  switch (stage.kind) {
    case 'CheckAnswers':
      return [{
        type: 'summary-list',
        children: workflow.stages
          .filter(candidate => candidate.kind === 'Question')
          .sort((left, right) => left.stageKey.localeCompare(right.stageKey))
          .flatMap(candidate => harvestInputs(candidate.components ?? [])),
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
        children: [],
      }];
  }
}

function harvestInputs(components: AuthoredComponent[]): AuthoredComponent[] {
  const out: AuthoredComponent[] = [];
  for (const component of components) {
    if (component.type === 'fieldset') {
      out.push(...harvestInputs(component.children));
    } else if (component.type === 'accordion') {
      for (const section of component.sections) {
        out.push(...harvestInputs(section.children));
      }
    } else if (
      component.type === 'text' || component.type === 'number' || component.type === 'decimal'
      || component.type === 'select' || component.type === 'radio' || component.type === 'checkboxlist'
      || component.type === 'date' || component.type === 'email' || component.type === 'textarea'
      || component.type === 'boolean'
    ) {
      out.push(component);
    }
  }
  return out;
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

function computeChecksum(file: ProjectedWorkflowDefinition): string {
  const text = JSON.stringify(file);
  let hash = 0;
  for (let index = 0; index < text.length; index += 1) {
    hash = ((hash << 5) - hash) + text.charCodeAt(index);
    hash |= 0;
  }

  return `local-${Math.abs(hash).toString(16)}`;
}
