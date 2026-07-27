import type {
  AuthoredAction,
  AuthoredGateway,
  AuthoredRoute,
  AuthoredServiceBlueprint,
  RouteView,
} from './types.js';
import { gatewayKind, serviceBlueprintGateways, serviceBlueprintStages } from './types.js';

function routeIdFor(sourceKey: string, route: Pick<AuthoredRoute, 'id' | 'trigger' | 'target'>): string {
  return route.id || `${sourceKey || 'unknown'}--${route.trigger || 'continue'}--${route.target || 'unknown'}`;
}

type RouteOwner =
  | { kind: 'state'; key: string; route: AuthoredRoute }
  | { kind: 'gateway'; key: string; route: AuthoredRoute };

function routeOwners(serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'> | null | undefined): RouteOwner[] {
  if (!serviceBlueprint) {
    return [];
  }

  const stateOwners = serviceBlueprintStages(serviceBlueprint).flatMap(stage =>
    (stage.routes ?? []).map(route => ({ kind: 'state' as const, key: stage.stateKey, route }))
  );
  const gatewayOwners = serviceBlueprintGateways(serviceBlueprint).flatMap(gateway =>
    (gateway.routes ?? []).map(route => ({ kind: 'gateway' as const, key: gateway.key, route }))
  );

  return [...stateOwners, ...gatewayOwners];
}

function mapRouteView(owner: RouteOwner, serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'>, routeIndex: number): RouteView {
  const gatewayKeys = new Set(serviceBlueprintGateways(serviceBlueprint).map(gateway => gateway.key));
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
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'> | null | undefined
): RouteView[] {
  if (!serviceBlueprint) {
    return [];
  }

  return routeOwners(serviceBlueprint).map((owner, routeIndex) => mapRouteView(owner, serviceBlueprint, routeIndex));
}

export function routeAddressFromView(view: RouteView): { routeId: string } {
  return { routeId: view.routeId };
}

export function findRoute(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'>,
  routeId: string
): { route: AuthoredRoute; routeIndex: number } | null {
  const owners = routeOwners(serviceBlueprint);
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
  serviceBlueprint: AuthoredServiceBlueprint,
  routeId: string,
  mutator: (route: AuthoredRoute) => AuthoredRoute | null
): AuthoredServiceBlueprint {
  const nextStates = serviceBlueprintStages(serviceBlueprint).map(stage => ({
    ...stage,
    routes: (stage.routes ?? []).flatMap(route => {
      const nextRoute = routeIdFor(stage.stateKey, route) === routeId ? mutator(route) : route;
      return nextRoute ? [nextRoute] : [];
    }),
  }));
  const nextGateways = serviceBlueprintGateways(serviceBlueprint).map(gateway => ({
    ...gateway,
    routes: (gateway.routes ?? []).flatMap(route => {
      const nextRoute = routeIdFor(gateway.key, route) === routeId ? mutator(route) : route;
      return nextRoute ? [nextRoute] : [];
    }),
  }));

  return {
    ...serviceBlueprint,
    stages: nextStates,
    gateways: nextGateways,
  };
}

export function updateRoute(
  serviceBlueprint: AuthoredServiceBlueprint,
  address: { routeId: string },
  mutator: (route: AuthoredRoute) => AuthoredRoute
): AuthoredServiceBlueprint {
  return mutateRouteOwners(serviceBlueprint, address.routeId, route => mutator(route));
}

export function deleteRoute(
  serviceBlueprint: AuthoredServiceBlueprint,
  address: { gatewayKey?: string; routeId: string }
): AuthoredServiceBlueprint {
  return mutateRouteOwners(serviceBlueprint, address.routeId, () => null);
}

export function addRoute(
  serviceBlueprint: AuthoredServiceBlueprint,
  gatewayKey: string,
  route: AuthoredRoute
): AuthoredServiceBlueprint {
  return {
    ...serviceBlueprint,
    gateways: serviceBlueprintGateways(serviceBlueprint).map(gateway =>
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
  serviceBlueprint: AuthoredServiceBlueprint,
  sourceStageKey: string
): { serviceBlueprint: AuthoredServiceBlueprint; gatewayKey: string } {
  const existingGateway = serviceBlueprintGateways(serviceBlueprint).find(gateway =>
    gatewayKind(gateway) === 'Split'
    && serviceBlueprintStages(serviceBlueprint)
      .find(stage => stage.stateKey === sourceStageKey)
      ?.routes?.some(route => route.target === gateway.key)
  );

  if (existingGateway) {
    return { serviceBlueprint, gatewayKey: existingGateway.key };
  }

  const stage = serviceBlueprintStages(serviceBlueprint).find(candidate => candidate.stateKey === sourceStageKey);
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

  const anchoredStates = serviceBlueprintStages(serviceBlueprint).map(candidate =>
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
    serviceBlueprint: {
      ...serviceBlueprint,
      stages: anchoredStates,
      gateways: [...serviceBlueprintGateways(serviceBlueprint), gateway],
    },
    gatewayKey,
  };
}

export function outgoingRouteViews(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'>,
  stageKey: string
): RouteView[] {
  return flattenRoutes(serviceBlueprint).filter(view => view.fromStage === stageKey);
}

export function inboundRouteViews(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'>,
  stageKey: string
): RouteView[] {
  return flattenRoutes(serviceBlueprint).filter(view => view.toStage === stageKey);
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
