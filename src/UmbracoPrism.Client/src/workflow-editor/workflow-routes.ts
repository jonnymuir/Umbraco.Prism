/**
 * Route helpers — the single read/write path between editor surfaces (graph,
 * inspector, validation, projection) and the gateways-own-routes authored
 * model that landed with Slice C.
 *
 * AuthoredTransition is gone. Authored routing lives on gateways: each
 * gateway carries a `source` stage and an ordered list of `routes`, and the
 * runtime contract is derived from `gateway.source × gateway.routes` by the
 * projector. Surfaces that need to iterate routes as edges call
 * `flattenRoutes(workflow)`; mutations go through `addRoute` / `updateRoute`
 * / `deleteRoute`, which preserve the gateway shape and produce a new
 * immutable workflow snapshot per call (suitable for snapshot-based undo).
 */

import type {
  AuthoredAction,
  AuthoredGateway,
  AuthoredRoute,
  AuthoredWorkflow,
  RouteView,
} from './types.js';

/**
 * Return every route in the workflow, in `(gateway, routes[])` order.
 * This is the single iteration helper editor surfaces use to walk routes —
 * no surface should iterate `workflow.transitions` (the field is gone).
 */
export function flattenRoutes(
  workflow: Pick<AuthoredWorkflow, 'gateways'> | null | undefined
): RouteView[] {
  if (!workflow) return [];
  const gateways = workflow.gateways ?? [];
  const gatewayKeys = new Set(gateways.map(gateway => gateway.gatewayKey));
  const views: RouteView[] = [];

  gateways.forEach(gateway => {
    const source = gateway.source ?? '';
    (gateway.routes ?? []).forEach((route, routeIndex) => {
      const target = route.target ?? '';
      const toGateway = gatewayKeys.has(target) ? target : undefined;
      views.push({
        gatewayKey: gateway.gatewayKey,
        routeIndex,
        routeId: route.id,
        fromStage: source,
        toStage: target,
        action: route.trigger,
        actions: route.actions,
        requiresRole: route.requiresRole,
        condition: route.condition,
        editorComment: route.editorComment,
        fromGateway: gateway.kind === 'Split' ? gateway.gatewayKey : undefined,
        toGateway,
      });
    });
  });

  return views;
}

/** Build the `(gatewayKey, routeId)` address for a route view. */
export function routeAddressFromView(view: RouteView): { gatewayKey: string; routeId: string } {
  return { gatewayKey: view.gatewayKey, routeId: view.routeId };
}

/** Find a route by its (gatewayKey, routeId) address. */
export function findRoute(
  workflow: Pick<AuthoredWorkflow, 'gateways'>,
  gatewayKey: string,
  routeId: string
): { gateway: AuthoredGateway; route: AuthoredRoute; routeIndex: number } | null {
  const gateway = (workflow.gateways ?? []).find(g => g.gatewayKey === gatewayKey);
  if (!gateway) return null;
  const routes = gateway.routes ?? [];
  const routeIndex = routes.findIndex(route => route.id === routeId);
  if (routeIndex < 0) return null;
  return { gateway, route: routes[routeIndex], routeIndex };
}

function replaceGateway(
  workflow: AuthoredWorkflow,
  gatewayKey: string,
  mutator: (gateway: AuthoredGateway) => AuthoredGateway
): AuthoredWorkflow {
  const gateways = (workflow.gateways ?? []).map(gateway =>
    gateway.gatewayKey === gatewayKey ? mutator(gateway) : gateway
  );
  return { ...workflow, gateways };
}

/** Mutate a single route immutably. */
export function updateRoute(
  workflow: AuthoredWorkflow,
  address: { gatewayKey: string; routeId: string },
  mutator: (route: AuthoredRoute) => AuthoredRoute
): AuthoredWorkflow {
  return replaceGateway(workflow, address.gatewayKey, gateway => ({
    ...gateway,
    routes: (gateway.routes ?? []).map(route => (route.id === address.routeId ? mutator(route) : route)),
  }));
}

export function deleteRoute(
  workflow: AuthoredWorkflow,
  address: { gatewayKey: string; routeId: string }
): AuthoredWorkflow {
  return replaceGateway(workflow, address.gatewayKey, gateway => ({
    ...gateway,
    routes: (gateway.routes ?? []).filter(route => route.id !== address.routeId),
  }));
}

export function addRoute(
  workflow: AuthoredWorkflow,
  gatewayKey: string,
  route: AuthoredRoute
): AuthoredWorkflow {
  return replaceGateway(workflow, gatewayKey, gateway => ({
    ...gateway,
    routes: [...(gateway.routes ?? []), route],
  }));
}

/** Build a sensible route id (`source--trigger--target`). */
export function newRouteId(source: string, trigger: string, target: string): string {
  return `${source || 'anonymous'}--${trigger || 'continue'}--${target || 'unknown'}`;
}

/**
 * Find the Split gateway anchored at the given source stage. If none exists,
 * synthesise a minimal one and append it to `workflow.gateways`. Returns the
 * mutated workflow and the gateway key the caller can append routes to.
 */
export function findOrCreateSplitGateway(
  workflow: AuthoredWorkflow,
  sourceStageKey: string
): { workflow: AuthoredWorkflow; gatewayKey: string } {
  const existing = (workflow.gateways ?? []).find(
    g => g.kind === 'Split' && g.source === sourceStageKey
  );
  if (existing) {
    return { workflow, gatewayKey: existing.gatewayKey };
  }

  const stage = workflow.stages.find(s => s.stageKey === sourceStageKey);
  const gatewayKey = `route-from-${sourceStageKey}`;
  const gateway: AuthoredGateway = {
    gatewayKey,
    displayName: stage
      ? `Route from ${stage.displayName}`
      : `Route from ${sourceStageKey}`,
    kind: 'Split',
    source: sourceStageKey,
    laneKey: stage?.actor,
    roleGates: [],
    routes: [],
  };

  return {
    workflow: {
      ...workflow,
      gateways: [...(workflow.gateways ?? []), gateway],
    },
    gatewayKey,
  };
}

/** Convenience — return all RouteViews where `fromStage` equals `stageKey`. */
export function outgoingRouteViews(
  workflow: Pick<AuthoredWorkflow, 'gateways'>,
  stageKey: string
): RouteView[] {
  return flattenRoutes(workflow).filter(view => view.fromStage === stageKey);
}

/** Convenience — return all RouteViews where `toStage` equals `stageKey`. */
export function inboundRouteViews(
  workflow: Pick<AuthoredWorkflow, 'gateways'>,
  stageKey: string
): RouteView[] {
  return flattenRoutes(workflow).filter(view => view.toStage === stageKey);
}

/** Compose a default route from a source stage to a target stage. */
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
