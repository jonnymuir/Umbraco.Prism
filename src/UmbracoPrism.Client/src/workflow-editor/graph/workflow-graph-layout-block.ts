import type { AuthoredWorkflow, WorkflowNodePosition } from '../types.js';
import { workflowGateways } from '../types.js';
import type { WorkflowQueueDefinition } from '../workflow-stage-assignment.js';
import {
  computeDerivedLayout,
  computeTopology,
  gatewayNodeId,
  stageNodeId,
} from './workflow-graph-layout.js';

/**
 * Immutable helpers for the definition's `layout` block. Positions are stored
 * in whole flow pixels keyed by prefixed node id; queue membership stays on
 * the states/gateways themselves.
 */

function roundPosition(position: WorkflowNodePosition): WorkflowNodePosition {
  return { x: Math.round(position.x), y: Math.round(position.y) };
}

export function getNodePosition(
  workflow: AuthoredWorkflow,
  nodeId: string
): WorkflowNodePosition | null {
  return workflow.layout?.nodes?.[nodeId] ?? null;
}

/** Returns a new workflow with the given node positions written into the layout block. */
export function setNodePositions(
  workflow: AuthoredWorkflow,
  positions: Record<string, WorkflowNodePosition>
): AuthoredWorkflow {
  const nodes: Record<string, WorkflowNodePosition> = { ...(workflow.layout?.nodes ?? {}) };
  for (const [nodeId, position] of Object.entries(positions)) {
    nodes[nodeId] = roundPosition(position);
  }
  return pruneLayout({ ...workflow, layout: { nodes } });
}

/** Drops layout entries whose stage or gateway no longer exists. */
export function pruneLayout(workflow: AuthoredWorkflow): AuthoredWorkflow {
  const entries = Object.entries(workflow.layout?.nodes ?? {});
  if (entries.length === 0) {
    return workflow.layout === undefined ? workflow : { ...workflow, layout: undefined };
  }

  const liveIds = new Set<string>([
    ...workflow.states.map(stage => stageNodeId(stage.stateKey)),
    ...workflowGateways(workflow).map(gateway => gatewayNodeId(gateway.key)),
  ]);
  const nodes: Record<string, WorkflowNodePosition> = {};
  for (const [nodeId, position] of entries) {
    if (liveIds.has(nodeId)) {
      nodes[nodeId] = position;
    }
  }

  if (Object.keys(nodes).length === 0) {
    return { ...workflow, layout: undefined };
  }
  return { ...workflow, layout: { nodes } };
}

/**
 * Tidy layout: recompute the derived auto-layout for every node (ignoring any
 * stored positions) and write the result back as explicit positions, so the
 * arrangement is deterministic and each node stays individually adjustable.
 */
export function applyAutoArrange(
  workflow: AuthoredWorkflow,
  availableQueues: WorkflowQueueDefinition[] = []
): AuthoredWorkflow {
  const layout = computeDerivedLayout(computeTopology(workflow, availableQueues));
  const nodes: Record<string, WorkflowNodePosition> = {};
  layout.placements.forEach(placement => {
    nodes[placement.id] = roundPosition({ x: placement.x, y: placement.y });
  });
  if (Object.keys(nodes).length === 0) {
    return { ...workflow, layout: undefined };
  }
  return { ...workflow, layout: { nodes } };
}
