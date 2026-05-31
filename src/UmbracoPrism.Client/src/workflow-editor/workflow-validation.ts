import type { ActionCatalogEntry, AuthoredAction, AuthoredStage, AuthoredWorkflow, RouteView } from './types.js';
import { findCatalogEntry, validateAction } from './workflow-action-editing.js';
import { flattenRoutes, outgoingRouteViews, inboundRouteViews } from './workflow-routes.js';

const TERMINAL_STAGE_KINDS = new Set<AuthoredStage['kind']>(['Confirmation']);

export type WorkflowValidationSeverity = 'error' | 'warning';

export type WorkflowValidationLocation =
  | { kind: 'stage'; stageKey: string }
  | { kind: 'route'; gatewayKey: string; routeId: string }
  | {
      kind: 'action';
      target: 'stage' | 'route';
      stageKey?: string;
      gatewayKey?: string;
      routeId?: string;
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
    | 'route-missing-stage'
    | 'action-configuration';
  severity: WorkflowValidationSeverity;
  message: string;
  blocking: boolean;
  location: WorkflowValidationLocation;
}

export function isTerminalStage(stage: AuthoredStage): boolean {
  return TERMINAL_STAGE_KINDS.has(stage.kind);
}

export function workflowOutgoingRoutes(workflow: AuthoredWorkflow, stageKey: string): RouteView[] {
  return outgoingRouteViews(workflow, stageKey);
}

export function workflowInboundRoutes(workflow: AuthoredWorkflow, stageKey: string): RouteView[] {
  return inboundRouteViews(workflow, stageKey);
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

    workflowOutgoingRoutes(workflow, current).forEach(route => {
      if (stageKeys.has(route.toStage) && !reachable.has(route.toStage)) {
        pending.push(route.toStage);
      }
    });
  }

  return reachable;
}

export function workflowOrphanedStages(workflow: AuthoredWorkflow): AuthoredStage[] {
  return workflow.stages.filter(stage =>
    stage.stageKey !== workflow.initialStageKey
    && workflowInboundRoutes(workflow, stage.stageKey).length === 0
    && workflowOutgoingRoutes(workflow, stage.stageKey).length === 0
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
    && workflowOutgoingRoutes(workflow, stage.stageKey).length === 0
  );
}

export function workflowRoutesWithMissingStages(workflow: AuthoredWorkflow): RouteView[] {
  const stageKeys = new Set(workflow.stages.map(stage => stage.stageKey));
  const gatewayKeys = new Set((workflow.gateways ?? []).map(g => g.gatewayKey));
  return flattenRoutes(workflow).filter(route =>
    !stageKeys.has(route.fromStage)
    || (!stageKeys.has(route.toStage) && !gatewayKeys.has(route.toStage))
  );
}

function stageLabel(workflow: AuthoredWorkflow, stageKey: string) {
  return workflow.stages.find(stage => stage.stageKey === stageKey)?.displayName ?? stageKey;
}

function actionLabel(entry: ActionCatalogEntry | null, action: AuthoredAction) {
  return entry?.label ?? action.summary?.trim() ?? action.type;
}

function routeLabel(workflow: AuthoredWorkflow, view: RouteView) {
  return `${stageLabel(workflow, view.fromStage)} → ${stageLabel(workflow, view.toStage)}`;
}

function normaliseValidationMessage(message: string) {
  return message.endsWith('.') ? message : `${message}.`;
}

function actionValidationIssues(
  workflow: AuthoredWorkflow,
  actionCatalog: ActionCatalogEntry[],
  action: AuthoredAction,
  location: Extract<WorkflowValidationLocation, { kind: 'action' }>,
  routeView?: RouteView
): WorkflowValidationIssue[] {
  const entry = findCatalogEntry(actionCatalog, action.type);
  const validation = validateAction(entry, action);
  const baseLabel = actionLabel(entry, action);
  const parentLabel = location.target === 'stage'
    ? stageLabel(workflow, location.stageKey ?? '')
    : routeView
      ? routeLabel(workflow, routeView)
      : `${location.gatewayKey ?? ''}/${location.routeId ?? ''}`;

  const propertyIssues = Object.entries(validation.propertyErrors).map(([fieldKey, message]) => ({
    id: `${location.target}-${location.stageKey ?? `${location.gatewayKey}-${location.routeId}`}-action-${location.actionIndex}-${fieldKey}`,
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
        id: `${location.target}-${location.stageKey ?? `${location.gatewayKey}-${location.routeId}`}-action-${location.actionIndex}-form-${fieldIndex}-${fieldKey}`,
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

  const missingStageRouteIssues = workflowRoutesWithMissingStages(workflow).map(view => {
    const stageKeys = new Set(workflow.stages.map(stage => stage.stageKey));
    const gatewayKeys = new Set((workflow.gateways ?? []).map(g => g.gatewayKey));
    const missingSource = !stageKeys.has(view.fromStage);
    const missingTarget = !stageKeys.has(view.toStage) && !gatewayKeys.has(view.toStage);
    const missingLabel = missingTarget ? view.toStage : view.fromStage;
    const direction = missingTarget ? 'target' : 'source';
    const gatewayKey = view.gatewayKey ?? '';
    const routeId = view.routeId ?? '';

    return {
      id: `route-missing-stage-${gatewayKey}-${routeId}`,
      code: 'route-missing-stage' as const,
      severity: 'error' as const,
      blocking: true,
      location: { kind: 'route', gatewayKey, routeId } as const,
      message: missingSource && missingTarget
        ? `Route “${view.action}” is disconnected because both ends are missing. Reconnect it to existing stages before you save or simulate this workflow.`
        : `Route “${view.action}” points to a missing ${direction} stage “${missingLabel}”. Reconnect it to an existing stage before you save or simulate this workflow.`,
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

  const routeActionIssues = flattenRoutes(workflow).flatMap(view =>
    (view.actions ?? []).flatMap((action, actionIndex) =>
      actionValidationIssues(workflow, actionCatalog, action, {
        kind: 'action',
        target: 'route',
        gatewayKey: view.gatewayKey ?? '',
        routeId: view.routeId ?? '',
        actionIndex,
      }, view)
    )
  );

  return [
    ...initialStageIssues,
    ...orphanedIssues,
    ...unreachableIssues,
    ...deadEndIssues,
    ...missingStageRouteIssues,
    ...stageActionIssues,
    ...routeActionIssues,
  ];
}

// Back-compat aliases for tests that look up the old names.
export const workflowOutgoingTransitions = workflowOutgoingRoutes;
export const workflowInboundTransitions = workflowInboundRoutes;
export const workflowTransitionsWithMissingStages = workflowRoutesWithMissingStages;
