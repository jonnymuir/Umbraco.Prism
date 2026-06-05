import type { ActionCatalogEntry, AuthoredAction, AuthoredStage, AuthoredWorkflow, RouteView } from './types.js';
import { stageActions, stageKind, workflowGateways, workflowStates } from './types.js';
import { findCatalogEntry, validateAction } from './workflow-action-editing.js';
import { flattenRoutes, outgoingRouteViews, inboundRouteViews } from './workflow-routes.js';

const TERMINAL_STAGE_KINDS = new Set<AuthoredStage['metadata'] extends never ? never : ReturnType<typeof stageKind>>(['Confirmation']);

export type WorkflowValidationSeverity = 'error' | 'warning';

export type WorkflowValidationLocation =
  | { kind: 'stage'; stageKey: string }
  | { kind: 'route'; routeId: string }
  | {
      kind: 'action';
      target: 'stage' | 'route';
      stageKey?: string;
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
    | 'route-duplicate'
    | 'action-configuration';
  severity: WorkflowValidationSeverity;
  message: string;
  blocking: boolean;
  location: WorkflowValidationLocation;
}

export function isTerminalStage(stage: AuthoredStage): boolean {
  return TERMINAL_STAGE_KINDS.has(stageKind(stage));
}

export function workflowOutgoingRoutes(workflow: AuthoredWorkflow, stageKey: string): RouteView[] {
  return outgoingRouteViews(workflow, stageKey);
}

export function workflowInboundRoutes(workflow: AuthoredWorkflow, stageKey: string): RouteView[] {
  return inboundRouteViews(workflow, stageKey);
}

export function workflowReachableStageKeys(workflow: AuthoredWorkflow): Set<string> {
  const stageKeys = new Set(workflowStates(workflow).map(stage => stage.stateKey));
  const gatewayKeys = new Set(workflowGateways(workflow).map(gateway => gateway.key));
  if (stageKeys.size === 0) {
    return new Set<string>();
  }

  const startStageKey = stageKeys.has(workflow.initialState)
    ? workflow.initialState
    : workflow.states[0]?.stateKey;

  if (!startStageKey) {
    return new Set<string>();
  }

  const reachable = new Set<string>();
  const visitedNodes = new Set<string>();
  const pending = [startStageKey];
  const routes = flattenRoutes(workflow);

  while (pending.length > 0) {
    const current = pending.shift();
    if (!current || visitedNodes.has(current)) {
      continue;
    }

    visitedNodes.add(current);

    if (stageKeys.has(current)) {
      reachable.add(current);
    }

    routes.forEach(route => {
      if (route.fromStage !== current) {
        return;
      }

      if ((stageKeys.has(route.toStage) || gatewayKeys.has(route.toStage)) && !visitedNodes.has(route.toStage)) {
        pending.push(route.toStage);
      }
    });
  }

  return reachable;
}

export function workflowOrphanedStages(workflow: AuthoredWorkflow): AuthoredStage[] {
  return workflow.states.filter(stage =>
    stage.stateKey !== workflow.initialState
    && workflowInboundRoutes(workflow, stage.stateKey).length === 0
    && workflowOutgoingRoutes(workflow, stage.stateKey).length === 0
  );
}

export function workflowUnreachableStages(workflow: AuthoredWorkflow): AuthoredStage[] {
  const reachable = workflowReachableStageKeys(workflow);
  const orphanedKeys = new Set(workflowOrphanedStages(workflow).map(stage => stage.stateKey));
  return workflow.states.filter(stage => !reachable.has(stage.stateKey) && !orphanedKeys.has(stage.stateKey));
}

export function workflowDeadEndStages(workflow: AuthoredWorkflow): AuthoredStage[] {
  const orphanedKeys = new Set(workflowOrphanedStages(workflow).map(stage => stage.stateKey));
  return workflow.states.filter(stage =>
    !orphanedKeys.has(stage.stateKey)
    && !isTerminalStage(stage)
    && workflowOutgoingRoutes(workflow, stage.stateKey).length === 0
  );
}

export function workflowRoutesWithMissingStages(workflow: AuthoredWorkflow): RouteView[] {
  const stageKeys = new Set(workflow.states.map(stage => stage.stateKey));
  const gatewayKeys = new Set(workflowGateways(workflow).map(gateway => gateway.key));
  return flattenRoutes(workflow).filter(route =>
    (!stageKeys.has(route.fromStage) && !gatewayKeys.has(route.fromStage))
    || (!stageKeys.has(route.toStage) && !gatewayKeys.has(route.toStage))
  );
}

function stageLabel(workflow: AuthoredWorkflow, stageKey: string) {
  return workflow.states.find(stage => stage.stateKey === stageKey)?.displayName
    ?? workflowGateways(workflow).find(gateway => gateway.key === stageKey)?.displayName
    ?? stageKey;
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
      : location.routeId ?? '';

  const propertyIssues = Object.entries(validation.propertyErrors).map(([fieldKey, message]) => ({
    id: `${location.target}-${location.stageKey ?? location.routeId}-action-${location.actionIndex}-${fieldKey}`,
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
        id: `${location.target}-${location.stageKey ?? location.routeId}-action-${location.actionIndex}-form-${fieldIndex}-${fieldKey}`,
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
  const initialStageExists = workflow.states.some(stage => stage.stateKey === workflow.initialState);
  const initialStageIssues = initialStageExists || workflow.states.length === 0
    ? []
    : [{
        id: 'initial-stage-missing',
        code: 'initial-stage-missing' as const,
        severity: 'error' as const,
        blocking: true,
        location: { kind: 'stage', stageKey: workflow.initialState || workflow.states[0]?.stateKey || '' } as const,
        message: workflow.initialState
          ? `The workflow start stage “${workflow.initialState}” is missing. Pick an existing initial stage before you save or simulate this workflow.`
          : 'The workflow does not have an initial stage yet. Pick one before you save or simulate this workflow.',
      }];

  const orphanedIssues = workflowOrphanedStages(workflow).map(stage => ({
    id: `stage-orphaned-${stage.stateKey}`,
    code: 'stage-orphaned' as const,
    severity: 'error' as const,
    blocking: true,
    location: { kind: 'stage', stageKey: stage.stateKey } as const,
    message: `Stage “${stage.displayName}” is orphaned. Connect it through a gateway so authors can reach it.`,
  }));

  const unreachableIssues = workflowUnreachableStages(workflow).map(stage => ({
    id: `stage-unreachable-${stage.stateKey}`,
    code: 'stage-unreachable' as const,
    severity: 'error' as const,
    blocking: true,
    location: { kind: 'stage', stageKey: stage.stateKey } as const,
    message: `Stage “${stage.displayName}” is unreachable from the workflow start. Add or retarget a route through a gateway so authors can get there.`,
  }));

  const deadEndIssues = workflowDeadEndStages(workflow).map(stage => ({
    id: `stage-dead-end-${stage.stateKey}`,
    code: 'stage-dead-end' as const,
    severity: 'warning' as const,
    blocking: false,
    location: { kind: 'stage', stageKey: stage.stateKey } as const,
    message: `Stage “${stage.displayName}” has no outgoing route through a gateway yet.`,
  }));

  const duplicateRouteKeys = new Set<string>();
  const duplicateRouteIssues = flattenRoutes(workflow).flatMap(view => {
    const key = `${view.fromStage}::${view.action}::${view.toStage}`;
    if (duplicateRouteKeys.has(key)) {
      return [{
        id: `route-duplicate-${view.routeId}`,
        code: 'route-duplicate' as const,
        severity: 'error' as const,
        blocking: true,
        location: { kind: 'route', routeId: view.routeId } as const,
        message: `Route “${view.action}” from “${stageLabel(workflow, view.fromStage)}” to “${stageLabel(workflow, view.toStage)}” is duplicated. Keep each route unique in the flat contract.`,
      }];
    }

    duplicateRouteKeys.add(key);
    return [];
  });

  const missingStageRouteIssues = workflowRoutesWithMissingStages(workflow).map(view => {
    const stageKeys = new Set(workflow.states.map(stage => stage.stateKey));
    const gatewayKeys = new Set(workflowGateways(workflow).map(gateway => gateway.key));
    const missingSource = !stageKeys.has(view.fromStage) && !gatewayKeys.has(view.fromStage);
    const missingTarget = !stageKeys.has(view.toStage) && !gatewayKeys.has(view.toStage);
    const missingLabel = missingTarget ? view.toStage : view.fromStage;
    const direction = missingTarget ? 'target' : 'source';

    return {
      id: `route-missing-stage-${view.routeId}`,
      code: 'route-missing-stage' as const,
      severity: 'error' as const,
      blocking: true,
      location: { kind: 'route', routeId: view.routeId } as const,
      message: missingSource && missingTarget
        ? `Route “${view.action}” is disconnected because both ends are missing. Reconnect it to existing stages before you save or simulate this workflow.`
        : `Route “${view.action}” points to a missing ${direction} step “${missingLabel}”. Reconnect it to an existing stage or gateway before you save or simulate this workflow.`,
    };
  });

  const stageActionIssues = workflow.states.flatMap(stage =>
    stageActions(stage).flatMap((action, actionIndex) =>
      actionValidationIssues(workflow, actionCatalog, action, {
        kind: 'action',
        target: 'stage',
        stageKey: stage.stateKey,
        actionIndex,
      })
    )
  );

  const routeActionIssues = flattenRoutes(workflow).flatMap(view =>
    (view.actions ?? []).flatMap((action, actionIndex) =>
      actionValidationIssues(workflow, actionCatalog, action, {
        kind: 'action',
        target: 'route',
        routeId: view.routeId,
        actionIndex,
      }, view)
    )
  );

  return [
    ...initialStageIssues,
    ...orphanedIssues,
    ...unreachableIssues,
    ...deadEndIssues,
    ...duplicateRouteIssues,
    ...missingStageRouteIssues,
    ...stageActionIssues,
    ...routeActionIssues,
  ];
}
