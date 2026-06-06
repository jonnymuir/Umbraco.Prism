import type {
  AuthoredAction,
  AuthoredGateway,
  AuthoredRoute,
  AuthoredWorkflow,
  RouteView,
} from './types.js';
import { gatewayKind, workflowGateways, workflowStates } from './types.js';

function routeIdFor(sourceKey: string, route: Pick<AuthoredRoute, 'id' | 'trigger' | 'target'>): string {
  return route.id || `${sourceKey || 'unknown'}--${route.trigger || 'continue'}--${route.target || 'unknown'}`;
}

type RouteOwner =
  | { kind: 'state'; key: string; route: AuthoredRoute }
  | { kind: 'gateway'; key: string; route: AuthoredRoute };

function routeOwners(workflow: Pick<AuthoredWorkflow, 'states' | 'gateways'> | null | undefined): RouteOwner[] {
  if (!workflow) {
    return [];
  }

  const stateOwners = workflowStates(workflow).flatMap(stage =>
    (stage.routes ?? []).map(route => ({ kind: 'state' as const, key: stage.stateKey, route }))
  );
  const gatewayOwners = workflowGateways(workflow).flatMap(gateway =>
    (gateway.routes ?? []).map(route => ({ kind: 'gateway' as const, key: gateway.key, route }))
  );

  return [...stateOwners, ...gatewayOwners];
}

function mapRouteView(owner: RouteOwner, workflow: Pick<AuthoredWorkflow, 'states' | 'gateways'>, routeIndex: number): RouteView {
  const gatewayKeys = new Set(workflowGateways(workflow).map(gateway => gateway.key));
  const fromGateway = owner.kind === 'gateway' ? owner.key : undefined;
  const toGateway = gatewayKeys.has(owner.route.target) ? owner.route.target : undefined;

  return {
    fromStage: owner.key,
    toStage: owner.route.target,
    action: owner.route.trigger,
    actions: owner.route.actions,
    requiresRole: owner.route.requiresRole,
    condition: owner.route.condition,
    editorComment: owner.route.editorComment,
    fromGateway,
    toGateway,
    gatewayKey: fromGateway ?? toGateway,
    key: fromGateway ?? toGateway,
    routeIndex,
    routeId: routeIdFor(owner.key, owner.route),
  };
}

export function flattenRoutes(
  workflow: Pick<AuthoredWorkflow, 'states' | 'gateways'> | null | undefined
): RouteView[] {
  if (!workflow) {
    return [];
  }

  return routeOwners(workflow).map((owner, routeIndex) => mapRouteView(owner, workflow, routeIndex));
}

export function routeAddressFromView(view: RouteView): { routeId: string } {
  return { routeId: view.routeId };
}

export function findRoute(
  workflow: Pick<AuthoredWorkflow, 'states' | 'gateways'>,
  routeId: string
): { route: AuthoredRoute; routeIndex: number } | null {
  const owners = routeOwners(workflow);
  const routeIndex = owners.findIndex(owner => routeIdFor(owner.key, owner.route) === routeId);
  if (routeIndex < 0) {
    return null;
  }

  return {
    route: owners[routeIndex].route,
    routeIndex,
  };
}

function mutateRouteOwners(
  workflow: AuthoredWorkflow,
  routeId: string,
  mutator: (route: AuthoredRoute) => AuthoredRoute | null
): AuthoredWorkflow {
  const nextStates = workflowStates(workflow).map(stage => ({
    ...stage,
    routes: (stage.routes ?? []).flatMap(route => {
      const nextRoute = routeIdFor(stage.stateKey, route) === routeId ? mutator(route) : route;
      return nextRoute ? [nextRoute] : [];
    }),
  }));
  const nextGateways = workflowGateways(workflow).map(gateway => ({
    ...gateway,
    routes: (gateway.routes ?? []).flatMap(route => {
      const nextRoute = routeIdFor(gateway.key, route) === routeId ? mutator(route) : route;
      return nextRoute ? [nextRoute] : [];
    }),
  }));

  return {
    ...workflow,
    states: nextStates,
    gateways: nextGateways,
  };
}

export function updateRoute(
  workflow: AuthoredWorkflow,
  address: { routeId: string },
  mutator: (route: AuthoredRoute) => AuthoredRoute
): AuthoredWorkflow {
  return mutateRouteOwners(workflow, address.routeId, route => mutator(route));
}

export function deleteRoute(
  workflow: AuthoredWorkflow,
  address: { gatewayKey?: string; routeId: string }
): AuthoredWorkflow {
  return mutateRouteOwners(workflow, address.routeId, () => null);
}

export function addRoute(
  workflow: AuthoredWorkflow,
  gatewayKey: string,
  route: AuthoredRoute
): AuthoredWorkflow {
  return {
    ...workflow,
    gateways: workflowGateways(workflow).map(gateway =>
      gateway.key === gatewayKey
        ? { ...gateway, routes: [...(gateway.routes ?? []), route] }
        : gateway
    ),
  };
}

export function newRouteId(source: string, trigger: string, target: string): string {
  return routeIdFor(source, { id: '', trigger, target });
}

export function findOrCreateSplitGateway(
  workflow: AuthoredWorkflow,
  sourceStageKey: string
): { workflow: AuthoredWorkflow; gatewayKey: string } {
  const existingGateway = workflowGateways(workflow).find(gateway =>
    gatewayKind(gateway) === 'Split'
    && workflowStates(workflow)
      .find(stage => stage.stateKey === sourceStageKey)
      ?.routes?.some(route => route.target === gateway.key)
  );

  if (existingGateway) {
    return { workflow, gatewayKey: existingGateway.key };
  }

  const stage = workflowStates(workflow).find(candidate => candidate.stateKey === sourceStageKey);
  const gatewayKey = `route-from-${sourceStageKey}`;
  const gateway: AuthoredGateway = {
    key: gatewayKey,
    displayName: stage ? `Route from ${stage.displayName}` : `Route from ${sourceStageKey}`,
    gatewayType: 'Split',
    kind: 'Split',
    queueKey: stage?.queueKey,
    actor: stage?.actor,
    roleGates: stage?.roleGates ?? [],
    routes: [],
  };

  const anchoredStates = workflowStates(workflow).map(candidate =>
    candidate.stateKey === sourceStageKey
      ? {
          ...candidate,
          routes: candidate.routes?.some(route => route.target === gatewayKey)
            ? candidate.routes
            : [
                ...(candidate.routes ?? []),
                {
                  id: newRouteId(sourceStageKey, 'route', gatewayKey),
                  target: gatewayKey,
                  trigger: 'route',
                },
              ],
        }
      : candidate
  );

  return {
    workflow: {
      ...workflow,
      states: anchoredStates,
      gateways: [...workflowGateways(workflow), gateway],
    },
    gatewayKey,
  };
}

export function outgoingRouteViews(
  workflow: Pick<AuthoredWorkflow, 'states' | 'gateways'>,
  stageKey: string
): RouteView[] {
  return flattenRoutes(workflow).filter(view => view.fromStage === stageKey);
}

export function inboundRouteViews(
  workflow: Pick<AuthoredWorkflow, 'states' | 'gateways'>,
  stageKey: string
): RouteView[] {
  return flattenRoutes(workflow).filter(view => view.toStage === stageKey);
}

export function buildRoute(options: {
  source: string;
  target: string;
  trigger: string;
  requiresRole?: string;
  condition?: string;
  actions?: AuthoredAction[];
}): AuthoredRoute {
  return {
    id: newRouteId(options.source, options.trigger, options.target),
    target: options.target,
    trigger: options.trigger,
    requiresRole: options.requiresRole,
    condition: options.condition,
    actions: options.actions ?? [],
  };
}
