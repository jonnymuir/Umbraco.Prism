import type {
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
  AuthoredServiceBlueprint,
  AuthoredRoute,
} from './types.js';
import { stageActions, stageDescription, stageKind } from './types.js';

export interface ProjectionDiagnostic {
  code: string;
  message: string;
  severity?: 'error' | 'warning' | 'info';
  stageKey?: string | null;
}

export type ProjectedServiceBlueprintDefinition = AuthoredServiceBlueprint;

export interface ProjectServiceBlueprintResult {
  file: ProjectedServiceBlueprintDefinition;
  checksum: string;
  diagnostics: ProjectionDiagnostic[];
  hasErrors: boolean;
}

export type ProjectedServiceBlueprintState = AuthoredStage;
export type ProjectedServiceBlueprintTransition = AuthoredRoute;
export type ProjectedInputComponent = AuthoredInputComponent;
export type ProjectedFieldsetComponent = AuthoredFieldsetComponent;
export type ProjectedAccordionComponent = AuthoredAccordionComponent;
export type ProjectedPanelComponent = AuthoredPanelComponent;
export type ProjectedWaitingComponent = AuthoredWaitingComponent;
export type ProjectedSummaryListComponent = AuthoredSummaryListComponent;
export type ProjectedTaskListComponent = AuthoredTaskListComponent;
export type ProjectedContentComponent = AuthoredContentComponent;
export type ProjectedComponent = AuthoredComponent;

export function projectServiceBlueprintLocally(serviceBlueprint: AuthoredServiceBlueprint): ProjectServiceBlueprintResult {
  const file: ProjectedServiceBlueprintDefinition = {
    ...serviceBlueprint,
    stages: serviceBlueprint.stages.map(stage => projectStage(stage, serviceBlueprint)),
  };

  return {
    file,
    checksum: computeChecksum(file),
    diagnostics: [],
    hasErrors: false,
  };
}

function projectStage(stage: AuthoredStage, serviceBlueprint: AuthoredServiceBlueprint): AuthoredStage {
  return {
    ...stage,
    components: projectStageComponents(stage, serviceBlueprint),
    metadata: {
      ...(stage.metadata ?? {}),
      description: stageDescription(stage),
      stageType: stageKind(stage),
      actions: stageActions(stage),
    },
  };
}

function projectStageComponents(stage: AuthoredStage, serviceBlueprint: AuthoredServiceBlueprint): ProjectedComponent[] {
  if (stage.components && stage.components.length > 0) {
    return [...stage.components];
  }

  switch (stageKind(stage)) {
    case 'CheckAnswers':
      return [{
        type: 'summary-list',
        children: serviceBlueprint.stages
          .filter(candidate => stageKind(candidate) === 'Question')
          .sort((left, right) => left.stateKey.localeCompare(right.stateKey))
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

function computeChecksum(file: ProjectedServiceBlueprintDefinition): string {
  const text = JSON.stringify(file);
  let hash = 0;
  for (let index = 0; index < text.length; index += 1) {
    hash = ((hash << 5) - hash) + text.charCodeAt(index);
    hash |= 0;
  }

  return `local-${Math.abs(hash).toString(16)}`;
}
