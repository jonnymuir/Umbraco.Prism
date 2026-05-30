import type { ActionCatalogEntry, AuthoredAction, AuthoredStage, AuthoredTransition, AuthoredWorkflow } from './types.js';
import { findCatalogEntry, validateAction } from './workflow-action-editing.js';

const TERMINAL_STAGE_KINDS = new Set<AuthoredStage['kind']>(['Confirmation', 'Waiting', 'StatusTimeline']);

export type WorkflowValidationSeverity = 'error' | 'warning';

export type WorkflowValidationLocation =
  | { kind: 'stage'; stageKey: string }
  | { kind: 'transition'; transitionIndex: number }
  | {
      kind: 'action';
      target: 'stage' | 'transition';
      stageKey?: string;
      transitionIndex?: number;
      actionIndex: number;
      fieldKey?: string;
      formFieldIndex?: number;
    };

export interface WorkflowValidationIssue {
  id: string;
  code:
    | 'initial-stage-missing'
    | 'stage-orphaned'
    | 'stage-unreachable'
    | 'stage-dead-end'
    | 'transition-missing-stage'
    | 'action-configuration';
  severity: WorkflowValidationSeverity;
  message: string;
  blocking: boolean;
  location: WorkflowValidationLocation;
}

export function isTerminalStage(stage: AuthoredStage): boolean {
  return TERMINAL_STAGE_KINDS.has(stage.kind);
}

export function workflowOutgoingTransitions(workflow: AuthoredWorkflow, stageKey: string) {
  return workflow.transitions.filter(transition => transition.fromStage === stageKey);
}

export function workflowReachableStageKeys(workflow: AuthoredWorkflow): Set<string> {
  const stageKeys = new Set(workflow.stages.map(stage => stage.stageKey));
  if (stageKeys.size === 0) {
    return new Set<string>();
  }

  const startStageKey = stageKeys.has(workflow.initialStageKey)
    ? workflow.initialStageKey
    : workflow.stages[0]?.stageKey;

  if (!startStageKey) {
    return new Set<string>();
  }

  const reachable = new Set<string>();
  const pending = [startStageKey];

  while (pending.length > 0) {
    const current = pending.shift();
    if (!current || reachable.has(current)) {
      continue;
    }

    reachable.add(current);

    workflow.transitions
      .filter(transition => transition.fromStage === current)
      .forEach(transition => {
        if (stageKeys.has(transition.toStage) && !reachable.has(transition.toStage)) {
          pending.push(transition.toStage);
        }
      });
  }

  return reachable;
}

export function workflowInboundTransitions(workflow: AuthoredWorkflow, stageKey: string) {
  return workflow.transitions.filter(transition => transition.toStage === stageKey);
}

export function workflowOrphanedStages(workflow: AuthoredWorkflow): AuthoredStage[] {
  return workflow.stages.filter(stage =>
    stage.stageKey !== workflow.initialStageKey
    && workflowInboundTransitions(workflow, stage.stageKey).length === 0
    && workflowOutgoingTransitions(workflow, stage.stageKey).length === 0
  );
}

export function workflowUnreachableStages(workflow: AuthoredWorkflow): AuthoredStage[] {
  const reachable = workflowReachableStageKeys(workflow);
  const orphanedKeys = new Set(workflowOrphanedStages(workflow).map(stage => stage.stageKey));
  return workflow.stages.filter(stage => !reachable.has(stage.stageKey) && !orphanedKeys.has(stage.stageKey));
}

export function workflowDeadEndStages(workflow: AuthoredWorkflow): AuthoredStage[] {
  const orphanedKeys = new Set(workflowOrphanedStages(workflow).map(stage => stage.stageKey));
  return workflow.stages.filter(stage =>
    !orphanedKeys.has(stage.stageKey)
    && !isTerminalStage(stage)
    && workflowOutgoingTransitions(workflow, stage.stageKey).length === 0
  );
}

export function workflowTransitionsWithMissingStages(workflow: AuthoredWorkflow) {
  const stageKeys = new Set(workflow.stages.map(stage => stage.stageKey));
  return workflow.transitions
    .map((transition, transitionIndex) => ({ transition, transitionIndex }))
    .filter(({ transition }) =>
      !stageKeys.has(transition.fromStage)
      || !stageKeys.has(transition.toStage)
    );
}

function stageLabel(workflow: AuthoredWorkflow, stageKey: string) {
  return workflow.stages.find(stage => stage.stageKey === stageKey)?.displayName ?? stageKey;
}

function actionLabel(entry: ActionCatalogEntry | null, action: AuthoredAction) {
  return entry?.label ?? action.summary?.trim() ?? action.type;
}

function transitionLabel(workflow: AuthoredWorkflow, transition: AuthoredTransition) {
  return `${stageLabel(workflow, transition.fromStage)} → ${stageLabel(workflow, transition.toStage)}`;
}

function normaliseValidationMessage(message: string) {
  return message.endsWith('.') ? message : `${message}.`;
}

function actionValidationIssues(
  workflow: AuthoredWorkflow,
  actionCatalog: ActionCatalogEntry[],
  action: AuthoredAction,
  location: Extract<WorkflowValidationLocation, { kind: 'action' }>
): WorkflowValidationIssue[] {
  const entry = findCatalogEntry(actionCatalog, action.type);
  const validation = validateAction(entry, action);
  const baseLabel = actionLabel(entry, action);
  const parentLabel = location.target === 'stage'
    ? stageLabel(workflow, location.stageKey ?? '')
    : transitionLabel(workflow, workflow.transitions[location.transitionIndex ?? -1] ?? {
      fromStage: '',
      toStage: '',
      action: '',
    });

  const propertyIssues = Object.entries(validation.propertyErrors).map(([fieldKey, message]) => ({
    id: `${location.target}-${location.stageKey ?? location.transitionIndex}-action-${location.actionIndex}-${fieldKey}`,
    code: 'action-configuration' as const,
    severity: 'warning' as const,
    blocking: false,
    location: { ...location, fieldKey },
    message: location.target === 'stage'
      ? `Stage “${parentLabel}” has an action that needs attention: “${baseLabel}” — ${normaliseValidationMessage(message)}`
      : `Route “${parentLabel}” has an action that needs attention: “${baseLabel}” — ${normaliseValidationMessage(message)}`,
  }));

  const formFieldIssues = Object.entries(validation.formFieldErrors).flatMap(([fieldIndex, fieldErrors]) =>
    Object.entries(fieldErrors).flatMap(([fieldKey, message]) => {
      if (!message) {
        return [];
      }

      return [{
        id: `${location.target}-${location.stageKey ?? location.transitionIndex}-action-${location.actionIndex}-form-${fieldIndex}-${fieldKey}`,
        code: 'action-configuration' as const,
        severity: 'warning' as const,
        blocking: false,
        location: {
          ...location,
          fieldKey: 'fields',
          formFieldIndex: Number(fieldIndex),
        },
        message: location.target === 'stage'
          ? `Stage “${parentLabel}” has a form action that needs attention: “${baseLabel}” — ${normaliseValidationMessage(message)}`
          : `Route “${parentLabel}” has a form action that needs attention: “${baseLabel}” — ${normaliseValidationMessage(message)}`,
      }];
    })
  );

  return [...propertyIssues, ...formFieldIssues];
}

export function validateWorkflow(workflow: AuthoredWorkflow, actionCatalog: ActionCatalogEntry[] = []): WorkflowValidationIssue[] {
  const initialStageExists = workflow.stages.some(stage => stage.stageKey === workflow.initialStageKey);
  const initialStageIssues = initialStageExists || workflow.stages.length === 0
    ? []
    : [{
        id: 'initial-stage-missing',
        code: 'initial-stage-missing' as const,
        severity: 'error' as const,
        blocking: true,
        location: { kind: 'stage', stageKey: workflow.initialStageKey || workflow.stages[0]?.stageKey || '' } as const,
        message: workflow.initialStageKey
          ? `The workflow start stage “${workflow.initialStageKey}” is missing. Pick an existing initial stage before you save or simulate this workflow.`
          : 'The workflow does not have an initial stage yet. Pick one before you save or simulate this workflow.',
      }];

  const orphanedIssues = workflowOrphanedStages(workflow).map(stage => ({
    id: `stage-orphaned-${stage.stageKey}`,
    code: 'stage-orphaned' as const,
    severity: 'error' as const,
    blocking: true,
    location: { kind: 'stage', stageKey: stage.stageKey } as const,
    message: `Stage “${stage.displayName}” is orphaned. Connect it through a gateway so authors can reach it.`,
  }));

  const unreachableIssues = workflowUnreachableStages(workflow).map(stage => ({
    id: `stage-unreachable-${stage.stageKey}`,
    code: 'stage-unreachable' as const,
    severity: 'error' as const,
    blocking: true,
    location: { kind: 'stage', stageKey: stage.stageKey } as const,
    message: `Stage “${stage.displayName}” is unreachable from the workflow start. Add or retarget a route through a gateway so authors can get there.`,
  }));

  const deadEndIssues = workflowDeadEndStages(workflow).map(stage => ({
    id: `stage-dead-end-${stage.stageKey}`,
    code: 'stage-dead-end' as const,
    severity: 'warning' as const,
    blocking: false,
    location: { kind: 'stage', stageKey: stage.stageKey } as const,
    message: `Stage “${stage.displayName}” has no outgoing route through a gateway yet.`,
  }));

  const missingStageTransitionIssues = workflowTransitionsWithMissingStages(workflow).map(({ transition, transitionIndex }) => {
    const missingSource = !workflow.stages.some(stage => stage.stageKey === transition.fromStage);
    const missingTarget = !workflow.stages.some(stage => stage.stageKey === transition.toStage);
    const missingLabel = missingTarget ? transition.toStage : transition.fromStage;
    const direction = missingTarget ? 'target' : 'source';

    return {
      id: `transition-missing-stage-${transitionIndex}`,
      code: 'transition-missing-stage' as const,
      severity: 'error' as const,
      blocking: true,
      location: { kind: 'transition', transitionIndex } as const,
      message: missingSource && missingTarget
        ? `Route “${transition.action}” is disconnected because both ends are missing. Reconnect it to existing stages before you save or simulate this workflow.`
        : `Route “${transition.action}” points to a missing ${direction} stage “${missingLabel}”. Reconnect it to an existing stage before you save or simulate this workflow.`,
    };
  });

  const stageActionIssues = workflow.stages.flatMap(stage =>
    (stage.actions ?? []).flatMap((action, actionIndex) =>
      actionValidationIssues(workflow, actionCatalog, action, {
        kind: 'action',
        target: 'stage',
        stageKey: stage.stageKey,
        actionIndex,
      })
    )
  );

  const transitionActionIssues = workflow.transitions.flatMap((transition, transitionIndex) =>
    (transition.actions ?? []).flatMap((action, actionIndex) =>
      actionValidationIssues(workflow, actionCatalog, action, {
        kind: 'action',
        target: 'transition',
        transitionIndex,
        actionIndex,
      })
    )
  );

  return [
    ...initialStageIssues,
    ...orphanedIssues,
    ...unreachableIssues,
    ...deadEndIssues,
    ...missingStageTransitionIssues,
    ...stageActionIssues,
    ...transitionActionIssues,
  ];
}
