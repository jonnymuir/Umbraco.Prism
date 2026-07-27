import type { AuthoredServiceBlueprint, ServiceBlueprintNodePosition } from '../types.js';
import { serviceBlueprintGateways } from '../types.js';
import type { QueueDefinition } from '../stage-assignment.js';
import {
  computeDerivedLayout,
  computeTopology,
  gatewayNodeId,
  stageNodeId,
} from './service-blueprint-graph-layout.js';

/**
 * Immutable helpers for the definition's `layout` block. Positions are stored
 * in whole flow pixels keyed by prefixed node id; queue membership stays on
 * the stages/gateways themselves.
 */

function roundPosition(position: ServiceBlueprintNodePosition): ServiceBlueprintNodePosition {
  return { x: Math.round(position.x), y: Math.round(position.y) };
}

export function getNodePosition(
  serviceBlueprint: AuthoredServiceBlueprint,
  nodeId: string
): ServiceBlueprintNodePosition | null {
  return serviceBlueprint.layout?.nodes?.[nodeId] ?? null;
}

/** Returns a new serviceBlueprint with the given node positions written into the layout block. */
export function setNodePositions(
  serviceBlueprint: AuthoredServiceBlueprint,
  positions: Record<string, ServiceBlueprintNodePosition>
): AuthoredServiceBlueprint {
  const nodes: Record<string, ServiceBlueprintNodePosition> = { ...(serviceBlueprint.layout?.nodes ?? {}) };
  for (const [nodeId, position] of Object.entries(positions)) {
    nodes[nodeId] = roundPosition(position);
  }
  return pruneLayout({ ...serviceBlueprint, layout: { nodes } });
}

/** Drops layout entries whose stage or gateway no longer exists. */
export function pruneLayout(serviceBlueprint: AuthoredServiceBlueprint): AuthoredServiceBlueprint {
  const entries = Object.entries(serviceBlueprint.layout?.nodes ?? {});
  if (entries.length === 0) {
    return serviceBlueprint.layout === undefined ? serviceBlueprint : { ...serviceBlueprint, layout: undefined };
  }

  const liveIds = new Set<string>([
    ...serviceBlueprint.stages.map(stage => stageNodeId(stage.stateKey)),
    ...serviceBlueprintGateways(serviceBlueprint).map(gateway => gatewayNodeId(gateway.key)),
  ]);
  const nodes: Record<string, ServiceBlueprintNodePosition> = {};
  for (const [nodeId, position] of entries) {
    if (liveIds.has(nodeId)) {
      nodes[nodeId] = position;
    }
  }

  if (Object.keys(nodes).length === 0) {
    return { ...serviceBlueprint, layout: undefined };
  }
  return { ...serviceBlueprint, layout: { nodes } };
}

/**
 * Tidy layout: recompute the derived auto-layout for every node (ignoring any
 * stored positions) and write the result back as explicit positions, so the
 * arrangement is deterministic and each node stays individually adjustable.
 */
export function applyAutoArrange(
  serviceBlueprint: AuthoredServiceBlueprint,
  availableQueues: QueueDefinition[] = []
): AuthoredServiceBlueprint {
  const layout = computeDerivedLayout(computeTopology(serviceBlueprint, availableQueues));
  const nodes: Record<string, ServiceBlueprintNodePosition> = {};
  layout.placements.forEach(placement => {
    nodes[placement.id] = roundPosition({ x: placement.x, y: placement.y });
  });
  if (Object.keys(nodes).length === 0) {
    return { ...serviceBlueprint, layout: undefined };
  }
  return { ...serviceBlueprint, layout: { nodes } };
}
