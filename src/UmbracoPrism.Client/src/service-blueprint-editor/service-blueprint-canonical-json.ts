import type { AuthoredGateway, AuthoredRoute, AuthoredStage, AuthoredServiceBlueprint } from './types.js';

/**
 * Stable, deterministic JSON serialization for the flattened serviceBlueprint definition
 * used by the Definition tab.
 */
const TOP_LEVEL_KEY_ORDER: readonly string[] = [
  'definitionKey',
  'displayName',
  'version',
  'initialStage',
  'requestPolicy',
  'description',
  'schemaVersion',
  'calculations',
  'queues',
  'stages',
  'gateways',
  'parameterSchemas',
  'layout',
];

function serialisableRoute(route: AuthoredRoute): Record<string, unknown> {
  return {
    id: route.id,
    target: route.target,
    trigger: route.trigger,
    label: route.label,
    style: route.style,
    condition: route.condition,
    requiresRole: route.requiresRole,
    actions: route.actions,
    editorComment: route.editorComment,
  };
}

function serialisableState(stage: AuthoredStage): Record<string, unknown> {
  return {
    stageKey: stage.stateKey,
    displayName: stage.displayName,
    components: stage.components ?? [],
    description: stage.description,
    stageType: stage.kind,
    actor: stage.actor,
    queueKey: stage.queueKey,
    routes: (stage.routes ?? []).map(serialisableRoute),
    actions: stage.actions,
    roleGates: stage.roleGates,
    editorComment: stage.editorComment,
    icon: stage.icon,
  };
}

function serialisableGateway(gateway: AuthoredGateway): Record<string, unknown> {
  return {
    key: gateway.key,
    displayName: gateway.displayName,
    description: gateway.description,
    gatewayType: gateway.gatewayType ?? gateway.kind,
    queueKey: gateway.queueKey,
    actor: gateway.actor,
    roleGates: gateway.roleGates,
    routes: (gateway.routes ?? []).map(serialisableRoute),
    waitingContent: gateway.waitingContent,
    waitingExpectedSeconds: gateway.waitingExpectedSeconds,
    waitingPollIntervalMs: gateway.waitingPollIntervalMs,
    waitingAllowDefer: gateway.waitingAllowDefer,
    waitingDeferMessage: gateway.waitingDeferMessage,
    requiredIncomingQueues: gateway.requiredIncomingQueues,
    icon: gateway.icon,
  };
}

function serialisableServiceBlueprint(serviceBlueprint: AuthoredServiceBlueprint): Record<string, unknown> {
  return {
    definitionKey: serviceBlueprint.definitionKey,
    displayName: serviceBlueprint.displayName,
    version: serviceBlueprint.version,
    initialStage: serviceBlueprint.initialStage,
    requestPolicy: serviceBlueprint.requestPolicy,
    description: serviceBlueprint.description,
    schemaVersion: serviceBlueprint.schemaVersion,
    queues: serviceBlueprint.queues ?? [],
    stages: serviceBlueprint.stages.map(serialisableState),
    gateways: (serviceBlueprint.gateways ?? []).map(serialisableGateway),
    calculations: serviceBlueprint.calculations,
    parameterSchemas: serviceBlueprint.parameterSchemas,
    layout: serialisableLayout(serviceBlueprint.layout),
  };
}

function serialisableLayout(layout: AuthoredServiceBlueprint['layout']): Record<string, unknown> | undefined {
  const entries = Object.entries(layout?.nodes ?? {});
  if (entries.length === 0) {
    return undefined;
  }
  const nodes: Record<string, { x: number; y: number }> = {};
  for (const [key, position] of entries) {
    // Whole pixels only: drag jitter must never produce spurious dirty state.
    nodes[key] = { x: Math.round(position.x), y: Math.round(position.y) };
  }
  return { nodes };
}

function orderTopLevel(value: Record<string, unknown>): Record<string, unknown> {
  const ordered: Record<string, unknown> = {};
  for (const key of TOP_LEVEL_KEY_ORDER) {
    if (key in value && value[key] !== undefined) {
      ordered[key] = value[key];
    }
  }
  for (const key of Object.keys(value).sort()) {
    if (!(key in ordered) && value[key] !== undefined) {
      ordered[key] = value[key];
    }
  }
  return ordered;
}

function sortKeys(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(sortKeys);
  }
  if (value && typeof value === 'object') {
    const record = value as Record<string, unknown>;
    const sorted: Record<string, unknown> = {};
    // PrismComponent (UmbracoPrism.Shared) is a polymorphic type discriminated by "type".
    // System.Text.Json's built-in polymorphic deserialization requires that discriminator
    // to be the first property in the JSON object, so it must survive alphabetical sorting.
    if (record.type !== undefined) {
      sorted.type = sortKeys(record.type);
    }
    for (const key of Object.keys(record).sort()) {
      if (key === 'type') {
        continue;
      }
      if (record[key] !== undefined) {
        sorted[key] = sortKeys(record[key]);
      }
    }
    return sorted;
  }
  return value;
}

export function serializeAuthoredServiceBlueprint(serviceBlueprint: AuthoredServiceBlueprint): string {
  const top = orderTopLevel(serialisableServiceBlueprint(serviceBlueprint));
  const canonical: Record<string, unknown> = {};
  for (const key of Object.keys(top)) {
    canonical[key] = sortKeys(top[key]);
  }
  return JSON.stringify(canonical, null, 2);
}

export function authoredServiceBlueprintJsonEquals(
  left: AuthoredServiceBlueprint | null,
  right: AuthoredServiceBlueprint | null
): boolean {
  if (!left && !right) {
    return true;
  }
  if (!left || !right) {
    return false;
  }
  return serializeAuthoredServiceBlueprint(left) === serializeAuthoredServiceBlueprint(right);
}
