import type {
  AuthoredGateway,
  AuthoredStage,
  AuthoredServiceBlueprint,
  RouteView,
  ServiceBlueprintLayoutBlock,
} from '../types.js';
import {
  deriveGatewayBindings,
  gatewayQueueKey,
  type GatewayBinding,
} from '../gateway-representation.js';
import { flattenRoutes } from '../route-model.js';
import {
  stageQueueDescription,
  stageQueueKey,
  stageQueueLabel,
  stageSurface,
  type StageSurface,
  type QueueDefinition,
} from '../stage-assignment.js';

/**
 * Pure derived layout for the service blueprint graph. Extracted from the original
 * hand-drawn canvas so the same top-to-bottom, queue-swim-lane reading order
 * drives React Flow node positions: vertical lane columns per queue (first
 * appearance order), Kahn's longest-path row ranking with Join loop-back
 * edges removed from the ranking graph, and slot ordering within a
 * (lane, row band) bucket.
 */

export const NODE_WIDTH = 224;
export const NODE_HEIGHT = 128;
// Vertical pitch between successive row bands. Each band centres a node
// (stage or gateway); the pitch must clear NODE_HEIGHT so adjacent rows do
// not collide, and leave enough of a gap for the route's inline label chip
// (CHIP_HEIGHT, in graph-model.ts) to sit comfortably between them rather
// than crowding both node edges.
export const ROW_BAND_PITCH = 184;
export const TOP_PADDING = 64;
export const SIDE_PADDING = 56;
// Floor lane column width — lanes widen automatically when a row band needs
// more horizontal space for sibling slots.
export const LANE_WIDTH = 280;
export const LANE_GAP = 36;
// Horizontal padding inside a lane before slot columns start, so cards never
// sit flush against the lane chrome.
export const LANE_INSET = 28;
// Horizontal gap between sibling slot columns inside the same lane row band.
export const SLOT_GAP = 56;
export const GATEWAY_SIZE = 132;
export const GATEWAY_PILL_HEIGHT = 40;
export const GATEWAY_PILL_MIN_WIDTH = 104;
export const GATEWAY_PILL_MAX_WIDTH = 208;
export const LANE_HEADER_OFFSET = 80;

export type GraphNodeKind = 'stage' | 'gateway';

export const stageNodeId = (stateKey: string) => `stage:${stateKey}`;
export const gatewayNodeId = (gatewayKey: string) => `gateway:${gatewayKey}`;

export function parseGraphNodeId(id: string): { kind: GraphNodeKind; key: string } {
  return id.startsWith('gateway:')
    ? { kind: 'gateway', key: id.slice('gateway:'.length) }
    : { kind: 'stage', key: id.startsWith('stage:') ? id.slice('stage:'.length) : id };
}

export type StageTopologyNode = {
  id: string;
  kind: 'stage';
  stage: AuthoredStage;
  stageIndex: number;
  surface: StageSurface;
  queueKey: string;
  queueLabel: string;
  width: number;
  height: number;
};

export type GatewayTopologyNode = {
  id: string;
  kind: 'gateway';
  gateway: AuthoredGateway;
  binding: GatewayBinding;
  surface: StageSurface;
  queueKey: string;
  queueLabel: string;
  width: number;
  height: number;
  pill: boolean;
};

export type GraphTopologyNode = StageTopologyNode | GatewayTopologyNode;

export type GraphTopologyEdge = {
  key: string;
  fromId: string;
  toId: string;
  transitionIndices: number[];
  /**
   * A Join loop-back that would close a cycle. Excluded from the ranking
   * graph so Kahn's stays a DAG, but still rendered (as an upward edge).
   */
  backward: boolean;
  branch: boolean;
  merge: boolean;
};

export type TransitionBinding = {
  transition: RouteView;
  index: number;
  /** Node the authored route visually leaves from (its Split gateway if routed via one). */
  visualFromId: string;
  /** Node the authored route visually arrives at (its Join gateway if it targets one). */
  visualToId: string;
  /** Adjacency edge that hosts this transition's label chip (the final hop). */
  edgeKey: string | null;
  branch: boolean;
  merge: boolean;
};

export type GraphQueueInfo = {
  key: string;
  label: string;
  description: string;
  surface: StageSurface;
  stageCount: number;
};

export type GraphTopology = {
  nodes: GraphTopologyNode[];
  nodeById: Map<string, GraphTopologyNode>;
  edges: GraphTopologyEdge[];
  transitions: RouteView[];
  transitionBindings: TransitionBinding[];
  ranks: Map<string, number>;
  queues: GraphQueueInfo[];
};

export type NodePlacement = {
  id: string;
  kind: GraphNodeKind;
  x: number;
  y: number;
  width: number;
  height: number;
  queueKey: string;
  rowRank: number;
};

export type LaneGeometry = {
  key: string;
  label: string;
  description: string;
  surface: StageSurface;
  columnIndex: number;
  x: number;
  width: number;
  stageCount: number;
};

export type ServiceBlueprintGraphLayout = {
  placements: Map<string, NodePlacement>;
  lanes: LaneGeometry[];
  bounds: { width: number; height: number };
};

export function isPillGateway(gateway: AuthoredGateway): boolean {
  return gateway.gatewayType === 'Split' && (gateway.routes ?? []).length === 1;
}

/**
 * A genuine decision point: a Split with more than one route. A single-route
 * Split is just plumbing (every stage route must target a gateway), not a
 * choice — its edges should read as a plain sequential step, not a branch.
 */
function isDecisionSplit(node: GraphTopologyNode | undefined | null): boolean {
  return node?.kind === 'gateway' && node.gateway.gatewayType === 'Split' && !isPillGateway(node.gateway);
}

export function gatewayNodeSize(gateway: AuthoredGateway): { width: number; height: number } {
  if (!isPillGateway(gateway)) {
    return { width: GATEWAY_SIZE, height: GATEWAY_SIZE };
  }

  const pillLabel = (gateway.routes ?? [])[0]?.trigger?.trim() || gateway.displayName;
  const estimatedWidth = 44 + pillLabel.length * 8;
  return {
    width: Math.max(GATEWAY_PILL_MIN_WIDTH, Math.min(GATEWAY_PILL_MAX_WIDTH, estimatedWidth)),
    height: GATEWAY_PILL_HEIGHT,
  };
}

export function rowBandCenter(rowRank: number): number {
  return TOP_PADDING + LANE_HEADER_OFFSET + NODE_HEIGHT / 2 + rowRank * ROW_BAND_PITCH;
}

/** Lane whose horizontal band contains centerX; nearest lane when outside all bands. */
export function laneForPosition(lanes: LaneGeometry[], centerX: number): LaneGeometry | null {
  if (lanes.length === 0) {
    return null;
  }
  const containing = lanes.find(lane => centerX >= lane.x && centerX <= lane.x + lane.width);
  if (containing) {
    return containing;
  }
  return [...lanes].sort((left, right) => {
    const leftDistance = Math.abs(centerX - (left.x + left.width / 2));
    const rightDistance = Math.abs(centerX - (right.x + right.width / 2));
    return leftDistance - rightDistance;
  })[0];
}

function stageQueueKeyWithFallback(stage: AuthoredStage, surface: StageSurface): string {
  return stageQueueKey(stage) || (surface === 'back-stage' ? 'reviewer' : 'public');
}

function gatewayQueueKeyWithFallback(gateway: AuthoredGateway): string {
  return gatewayQueueKey(gateway) || 'public';
}

export function computeTopology(
  serviceBlueprint: AuthoredServiceBlueprint | null,
  availableQueues: QueueDefinition[] = []
): GraphTopology {
  const stages = serviceBlueprint?.stages ?? [];
  const transitions = flattenRoutes(serviceBlueprint);
  const gatewayBindings = serviceBlueprint ? deriveGatewayBindings(serviceBlueprint) : [];
  const labelForQueue = (queueKey: string) => stageQueueLabel(serviceBlueprint, queueKey, availableQueues);

  // 1. Lane entries: keep first-appearance order so the canvas reads left to
  //    right in the order the author introduced lanes.
  const stageNodes: StageTopologyNode[] = stages.map((stage, stageIndex) => {
    const surface = stageSurface(stage);
    const queueKey = stageQueueKeyWithFallback(stage, surface);
    return {
      id: stageNodeId(stage.stateKey),
      kind: 'stage',
      stage,
      stageIndex,
      surface,
      queueKey,
      queueLabel: labelForQueue(queueKey),
      width: NODE_WIDTH,
      height: NODE_HEIGHT,
    };
  });
  const gatewayNodes: GatewayTopologyNode[] = gatewayBindings.map(binding => {
    const surface = stageSurface(binding.gateway);
    const queueKey = binding.queueKey || gatewayQueueKeyWithFallback(binding.gateway);
    const size = gatewayNodeSize(binding.gateway);
    return {
      id: gatewayNodeId(binding.gateway.key),
      kind: 'gateway',
      gateway: binding.gateway,
      binding,
      surface,
      queueKey,
      queueLabel: labelForQueue(queueKey),
      width: size.width,
      height: size.height,
      pill: isPillGateway(binding.gateway),
    };
  });

  const queueStateByKey = new Map<string, { surface: StageSurface; stageCount: number }>();
  const queueOrder: string[] = [];
  const ensureQueue = (queueKey: string, surface: StageSurface, isStage: boolean) => {
    const existing = queueStateByKey.get(queueKey);
    if (existing) {
      if (isStage) {
        existing.stageCount += 1;
      }
      return;
    }
    queueStateByKey.set(queueKey, { surface, stageCount: isStage ? 1 : 0 });
    queueOrder.push(queueKey);
  };
  stageNodes.forEach(node => ensureQueue(node.queueKey, node.surface, true));
  gatewayNodes.forEach(node => ensureQueue(node.queueKey, node.surface, false));

  // 2. Adjacency graph spanning stages and gateways. Each gateway is wired
  //    to its anchor stage (split: stage→gateway) so the topological sort
  //    produces a stage → gateway → stage reading.
  const nodes: GraphTopologyNode[] = [...stageNodes, ...gatewayNodes];
  const nodeById = new Map(nodes.map(node => [node.id, node]));
  const nodeOrder = new Map(nodes.map((node, index) => [node.id, index]));
  const adjacency = new Map<string, Set<string>>();
  const inDegree = new Map<string, number>(nodes.map(node => [node.id, 0]));
  const edgeTransitionIndices = new Map<string, Set<number>>();

  const addEdge = (fromId: string, toId: string, transitionIndex?: number | number[]) => {
    if (fromId === toId || !nodeById.has(fromId) || !nodeById.has(toId)) {
      return;
    }
    let outgoing = adjacency.get(fromId);
    if (!outgoing) {
      outgoing = new Set<string>();
      adjacency.set(fromId, outgoing);
    }
    if (!outgoing.has(toId)) {
      outgoing.add(toId);
      inDegree.set(toId, (inDegree.get(toId) ?? 0) + 1);
    }
    const indices = transitionIndex === undefined
      ? []
      : Array.isArray(transitionIndex)
        ? transitionIndex
        : [transitionIndex];
    if (indices.length > 0) {
      const key = `${fromId}->${toId}`;
      const existing = edgeTransitionIndices.get(key) ?? new Set<number>();
      indices.forEach(index => existing.add(index));
      edgeTransitionIndices.set(key, existing);
    }
  };

  const splitGatewayKeyByAnchorStage = new Map<string, string>();

  gatewayNodes.forEach(node => {
    const anchorStageKey = node.binding.anchorStageKey;
    if (!anchorStageKey) {
      return;
    }
    if (node.gateway.gatewayType === 'Split') {
      if (!splitGatewayKeyByAnchorStage.has(anchorStageKey)) {
        splitGatewayKeyByAnchorStage.set(anchorStageKey, node.gateway.key);
      }
      addEdge(stageNodeId(anchorStageKey), node.id, node.binding.relatedTransitionIndices);
    }
    // Join gateways get no anchor edge: in the routes model the anchor is an
    // upstream stage, not the downstream merge target, so adding that edge
    // would create a cycle. The correct downstream edge (join → next stage)
    // is built in the transitions loop from the gateway's own routes.
  });

  transitions.forEach((transition, index) => {
    const sourceStageId = stageNodeId(transition.fromStage);
    const targetStageId = stageNodeId(transition.toStage);
    // Routes that genuinely target a join gateway already carry an explicit
    // toGateway value set by flattenRoutes; falling back to a join anchor
    // lookup here would intercept direct routes to regular stages.
    const targetGatewayKey = transition.toGateway ?? null;
    // Mirror image of the guard above: a route whose target is already a
    // resolved gateway fully describes its own routing (stage → that
    // gateway) and needs no source-side wrapping. Falling back to "whichever
    // Split gateway is anchored to this stage" regardless would, on a stage
    // with several routes to several distinct gateways, wire every route
    // after the first through the first route's gateway instead of the
    // stage itself — fabricating an edge between two unrelated gateways
    // that never appears in the authored JSON.
    const sourceGatewayKey = transition.fromGateway
      ?? (targetGatewayKey ? null : splitGatewayKeyByAnchorStage.get(transition.fromStage) ?? null);
    const sourceGatewayId = sourceGatewayKey ? gatewayNodeId(sourceGatewayKey) : null;
    const targetGatewayId = targetGatewayKey ? gatewayNodeId(targetGatewayKey) : null;

    if (sourceGatewayId) {
      addEdge(sourceStageId, sourceGatewayId, index);
    }
    const routedSourceId = sourceGatewayId ?? sourceStageId;
    if (targetGatewayId) {
      addEdge(routedSourceId, targetGatewayId, index);
      addEdge(targetGatewayId, targetStageId, index);
      return;
    }
    addEdge(routedSourceId, targetStageId, index);
  });

  const byIntroductionOrder = (left: string, right: string) =>
    (nodeOrder.get(left) ?? 0) - (nodeOrder.get(right) ?? 0);

  // 2b. Remove backward edges so Kahn's stays a DAG. Any route that closes a
  //     cycle back to an earlier point in the graph — not just a Join
  //     merging back (the common case), but equally a Split's "reject, go
  //     back to an earlier stage" branch — leaves nothing in the cycle
  //     rankable, and the whole cycle (often the whole graph, if nothing
  //     else anchors it) collapses to rank 0: a linear-looking serviceBlueprint
  //     renders as one wide horizontal row instead of flowing down the
  //     page.
  //
  //     Detection is a standard 3-colour DFS: a "back edge" is one that
  //     lands on a node still on the current DFS path (an ancestor), which
  //     is the graph-theoretic definition of the edge that actually closes
  //     a cycle — as opposed to every other edge *inside* the cycle, which
  //     also technically has a path back to its source but isn't the edge
  //     doing the closing. (An earlier version of this checked "can the
  //     target reach back to the source" for every edge, which is true for
  //     every edge in a cycle, not just the back edge — it flagged whichever
  //     edge got visited first, discarding a normal forward edge instead of
  //     the actual loop-back.) DFS starts from natural entry points
  //     (in-degree 0) in first-appearance order, then sweeps any nodes a
  //     pure cycle with no entry point would otherwise leave unvisited.
  //     Backward edges stay in the emitted edge list (flagged) so they
  //     still render.
  const backwardEdgeKeys = new Set<string>();
  const dfsState = new Map<string, 'visiting' | 'done'>();
  const visitForBackEdges = (fromId: string) => {
    dfsState.set(fromId, 'visiting');
    const neighbors = adjacency.get(fromId);
    if (neighbors) {
      [...neighbors].sort(byIntroductionOrder).forEach(toId => {
        const state = dfsState.get(toId);
        if (state === 'visiting') {
          neighbors.delete(toId);
          inDegree.set(toId, (inDegree.get(toId) ?? 1) - 1);
          backwardEdgeKeys.add(`${fromId}->${toId}`);
          return;
        }
        if (state === undefined) {
          visitForBackEdges(toId);
        }
      });
    }
    dfsState.set(fromId, 'done');
  };
  nodes
    .map(node => node.id)
    .filter(id => (inDegree.get(id) ?? 0) === 0)
    .sort(byIntroductionOrder)
    .forEach(id => {
      if (!dfsState.has(id)) {
        visitForBackEdges(id);
      }
    });
  nodes.forEach(node => {
    if (!dfsState.has(node.id)) {
      visitForBackEdges(node.id);
    }
  });

  // 3. Row-rank via longest-path (Kahn's algorithm): rank(B) > rank(A) for
  //    every forward edge A→B regardless of lane.
  const ranks = new Map<string, number>(nodes.map(node => [node.id, 0]));
  const inDegreeCopy = new Map(inDegree);

  const queue = nodes
    .map(node => node.id)
    .filter(id => (inDegreeCopy.get(id) ?? 0) === 0)
    .sort(byIntroductionOrder);

  while (queue.length > 0) {
    const currentId = queue.shift()!;
    const currentRank = ranks.get(currentId) ?? 0;
    const neighbours = adjacency.get(currentId);
    if (!neighbours) {
      continue;
    }
    [...neighbours]
      .sort(byIntroductionOrder)
      .forEach(nextId => {
        ranks.set(nextId, Math.max(ranks.get(nextId) ?? 0, currentRank + 1));

        const nextInDegree = (inDegreeCopy.get(nextId) ?? 0) - 1;
        inDegreeCopy.set(nextId, nextInDegree);
        if (nextInDegree === 0) {
          queue.push(nextId);
          queue.sort(byIntroductionOrder);
        }
      });
  }

  // Emitted edge list: forward adjacency edges plus flagged backward edges.
  const edges: GraphTopologyEdge[] = [];
  const pushEdge = (fromId: string, toId: string, backward: boolean) => {
    const key = `${fromId}->${toId}`;
    const fromNode = nodeById.get(fromId);
    const toNode = nodeById.get(toId);
    edges.push({
      key,
      fromId,
      toId,
      transitionIndices: [...(edgeTransitionIndices.get(key) ?? [])].sort((a, b) => a - b),
      backward,
      branch: isDecisionSplit(fromNode),
      merge: toNode?.kind === 'gateway' && toNode.gateway.gatewayType === 'Join',
    });
  };
  adjacency.forEach((targets, fromId) => {
    [...targets].sort(byIntroductionOrder).forEach(toId => pushEdge(fromId, toId, false));
  });
  backwardEdgeKeys.forEach(key => {
    const [fromId, toId] = key.split('->');
    pushEdge(fromId, toId, true);
  });

  // Per-authored-transition visual endpoints and hosting edge (the final hop
  // of the routed path stage → split gateway? → join gateway? → target).
  const transitionBindings: TransitionBinding[] = transitions.map((transition, index) => {
    const sourceStageId = stageNodeId(transition.fromStage);
    // See the matching guard in the adjacency-building loop above: a route
    // that already resolves its own target gateway needs no source-side
    // anchor fallback.
    const sourceGatewayKey = transition.fromGateway
      ?? (transition.toGateway ? null : splitGatewayKeyByAnchorStage.get(transition.fromStage) ?? null);
    const sourceGatewayId = sourceGatewayKey && nodeById.has(gatewayNodeId(sourceGatewayKey))
      ? gatewayNodeId(sourceGatewayKey)
      : null;
    const targetGatewayId = transition.toGateway && nodeById.has(gatewayNodeId(transition.toGateway))
      ? gatewayNodeId(transition.toGateway)
      : null;
    const targetStageId = stageNodeId(transition.toStage);

    const effectiveSourceId = nodeById.has(sourceStageId) ? sourceStageId : sourceGatewayId;
    const effectiveTargetId = nodeById.has(targetStageId) ? targetStageId : targetGatewayId;
    if (!effectiveSourceId || !effectiveTargetId) {
      return {
        transition,
        index,
        visualFromId: sourceGatewayId ?? sourceStageId,
        visualToId: targetGatewayId ?? targetStageId,
        edgeKey: null,
        branch: false,
        merge: false,
      };
    }

    const routedIds: string[] = [effectiveSourceId];
    if (sourceGatewayId && routedIds[routedIds.length - 1] !== sourceGatewayId) {
      routedIds.push(sourceGatewayId);
    }
    if (targetGatewayId && routedIds[routedIds.length - 1] !== targetGatewayId) {
      routedIds.push(targetGatewayId);
    }
    if (routedIds[routedIds.length - 1] !== effectiveTargetId) {
      routedIds.push(effectiveTargetId);
    }

    const finalFrom = routedIds[routedIds.length - 2];
    const finalTo = routedIds[routedIds.length - 1];
    const sourceGatewayNode = sourceGatewayId ? nodeById.get(sourceGatewayId) : null;
    const targetGatewayNode = targetGatewayId ? nodeById.get(targetGatewayId) : null;
    return {
      transition,
      index,
      visualFromId: sourceGatewayId ?? sourceStageId,
      visualToId: targetGatewayId ?? targetStageId,
      edgeKey: routedIds.length >= 2 ? `${finalFrom}->${finalTo}` : null,
      branch: isDecisionSplit(sourceGatewayNode),
      merge: targetGatewayNode?.kind === 'gateway' && targetGatewayNode.gateway.gatewayType === 'Join',
    };
  });

  const queues: GraphQueueInfo[] = queueOrder.map(queueKey => {
    const queueState = queueStateByKey.get(queueKey)!;
    return {
      key: queueKey,
      label: labelForQueue(queueKey),
      description: stageQueueDescription(serviceBlueprint, queueKey, availableQueues),
      surface: queueState.surface,
      stageCount: queueState.stageCount,
    };
  });

  return { nodes, nodeById, edges, transitions, transitionBindings, ranks, queues };
}

export function computeDerivedLayout(topology: GraphTopology): ServiceBlueprintGraphLayout {
  // 4. Bucket nodes by (lane, rowRank) so each band can size and centre its
  //    slot columns. Same-lane fan-out widens the lane horizontally.
  const nodesByQueueRow = new Map<string, Map<number, GraphTopologyNode[]>>();
  const rankFor = (node: GraphTopologyNode) =>
    topology.ranks.get(node.id) ?? (node.kind === 'gateway' ? 1 : 0);
  const allRanks = new Set<number>();
  topology.nodes.forEach(node => {
    let rows = nodesByQueueRow.get(node.queueKey);
    if (!rows) {
      rows = new Map<number, GraphTopologyNode[]>();
      nodesByQueueRow.set(node.queueKey, rows);
    }
    const rowRank = rankFor(node);
    allRanks.add(rowRank);
    const rowItems = rows.get(rowRank) ?? [];
    rowItems.push(node);
    rows.set(rowRank, rowItems);
  });

  // 5. Queue width = widest row band in that queue.
  const laneWidthByKey = new Map<string, number>();
  topology.queues.forEach(queue => {
    const rows = nodesByQueueRow.get(queue.key);
    let widestRow = LANE_WIDTH;
    rows?.forEach(items => {
      const contentWidth = items.reduce((sum, item) => sum + item.width, 0);
      widestRow = Math.max(
        widestRow,
        LANE_INSET * 2 + contentWidth + Math.max(items.length - 1, 0) * SLOT_GAP
      );
    });
    laneWidthByKey.set(queue.key, widestRow);
  });

  const lanes: LaneGeometry[] = [];
  const laneByKey = new Map<string, LaneGeometry>();
  let currentLaneX = SIDE_PADDING;
  topology.queues.forEach((queue, columnIndex) => {
    const lane: LaneGeometry = {
      key: queue.key,
      label: queue.label,
      description: queue.description,
      surface: queue.surface,
      columnIndex,
      x: currentLaneX,
      width: laneWidthByKey.get(queue.key) ?? LANE_WIDTH,
      stageCount: queue.stageCount,
    };
    laneByKey.set(queue.key, lane);
    lanes.push(lane);
    currentLaneX += lane.width + LANE_GAP;
  });

  // 6. Place nodes rank-by-rank, globally ascending, queue by queue within
  //    each rank — rather than centring every row independently in its lane.
  //    A row's slot order and block position lean on where its predecessors
  //    already landed (a one-pass barycenter sweep), so a fan-out's branches
  //    hold a stable column all the way down to their merge instead of each
  //    row re-centring on its own and flattening real branching into what
  //    reads as a single straight line. Forward (non-backward) edges always
  //    target a strictly higher rank, so every predecessor referenced here
  //    has already been placed by the time its dependents are laid out.
  const nodeOrder = new Map(topology.nodes.map((node, index) => [node.id, index]));
  const incomingByNode = new Map<string, string[]>();
  topology.edges.forEach(edge => {
    if (edge.backward) {
      return;
    }
    const incoming = incomingByNode.get(edge.toId) ?? [];
    incoming.push(edge.fromId);
    incomingByNode.set(edge.toId, incoming);
  });

  const placements = new Map<string, NodePlacement>();
  const preferredCenterX = (nodeId: string, lane: LaneGeometry): number => {
    const predecessorCenters = (incomingByNode.get(nodeId) ?? [])
      .map(id => placements.get(id))
      .filter((placement): placement is NodePlacement => placement !== undefined)
      .map(placement => placement.x + placement.width / 2);
    if (predecessorCenters.length === 0) {
      return lane.x + lane.width / 2;
    }
    return predecessorCenters.reduce((sum, x) => sum + x, 0) / predecessorCenters.length;
  };

  [...allRanks].sort((left, right) => left - right).forEach(rowRank => {
    topology.queues.forEach(queue => {
      const lane = laneByKey.get(queue.key);
      const items = nodesByQueueRow.get(queue.key)?.get(rowRank);
      if (!lane || !items || items.length === 0) {
        return;
      }

      // Order left-to-right by where each item's predecessors already sit
      // (its preferred column), falling back to introduction order for
      // siblings tied on the same predecessor (e.g. two routes fanning out
      // of the same gateway).
      const entries = items
        .map(item => ({ item, preferredCenter: preferredCenterX(item.id, lane) }))
        .sort((left, right) =>
          left.preferredCenter - right.preferredCenter
          || (nodeOrder.get(left.item.id) ?? 0) - (nodeOrder.get(right.item.id) ?? 0));

      const contentWidth = entries.reduce((sum, entry) => sum + entry.item.width, 0);
      const totalWidth = contentWidth + Math.max(entries.length - 1, 0) * SLOT_GAP;
      const blockCenter = entries.reduce((sum, entry) => sum + entry.preferredCenter, 0) / entries.length;

      // The widest row in this lane already sizes the lane to
      // LANE_INSET*2 + its own totalWidth, so every row's totalWidth fits
      // within lane.width - LANE_INSET*2 — this clamp keeps the row's block
      // as close to its preferred column as the lane allows, without ever
      // spilling past the lane's inset edges.
      const insetMin = lane.x + LANE_INSET;
      const insetMax = lane.x + lane.width - LANE_INSET - totalWidth;
      const startX = Math.min(Math.max(blockCenter - totalWidth / 2, insetMin), insetMax);

      const bandCenter = rowBandCenter(rowRank);
      let cursorX = startX;
      entries.forEach(({ item }) => {
        placements.set(item.id, {
          id: item.id,
          kind: item.kind,
          x: cursorX,
          y: bandCenter - item.height / 2,
          width: item.width,
          height: item.height,
          queueKey: queue.key,
          rowRank,
        });
        cursorX += item.width + SLOT_GAP;
      });
    });
  });

  const width = lanes.length === 0
    ? SIDE_PADDING * 2 + LANE_WIDTH
    : currentLaneX - LANE_GAP + SIDE_PADDING;
  const contentBottom = Math.max(
    TOP_PADDING + LANE_HEADER_OFFSET + NODE_HEIGHT,
    ...[...placements.values()].map(placement => placement.y + placement.height)
  );
  const height = contentBottom + TOP_PADDING;

  return { placements, lanes, bounds: { width, height } };
}

/**
 * Derived layout with stored manual positions applied on top. Lane bands are
 * elastic: they stretch to cover their members' final positions (a dragged
 * node widens its lane rather than escaping it), and the canvas bounds grow
 * with the content. Nodes without a stored position keep their derived slot.
 */
export function mergeLayout(
  topology: GraphTopology,
  layoutBlock?: ServiceBlueprintLayoutBlock | null
): ServiceBlueprintGraphLayout {
  const derived = computeDerivedLayout(topology);
  const stored = layoutBlock?.nodes;
  if (!stored || Object.keys(stored).length === 0) {
    return derived;
  }

  const placements = new Map<string, NodePlacement>();
  derived.placements.forEach((placement, id) => {
    const override = stored[id];
    placements.set(
      id,
      override ? { ...placement, x: override.x, y: override.y } : placement
    );
  });

  const lanes = derived.lanes.map(lane => {
    const members = [...placements.values()].filter(placement => placement.queueKey === lane.key);
    if (members.length === 0) {
      return lane;
    }
    const left = Math.min(lane.x, ...members.map(member => member.x - LANE_INSET));
    const right = Math.max(
      lane.x + lane.width,
      ...members.map(member => member.x + member.width + LANE_INSET)
    );
    return { ...lane, x: left, width: right - left };
  });

  const contentBottom = Math.max(
    derived.bounds.height - TOP_PADDING,
    ...[...placements.values()].map(placement => placement.y + placement.height)
  );
  const contentRight = Math.max(
    derived.bounds.width - SIDE_PADDING,
    ...lanes.map(lane => lane.x + lane.width)
  );

  return {
    placements,
    lanes,
    bounds: { width: contentRight + SIDE_PADDING, height: contentBottom + TOP_PADDING },
  };
}

export function computeServiceBlueprintGraphLayout(
  serviceBlueprint: AuthoredServiceBlueprint | null,
  availableQueues: QueueDefinition[] = []
): { topology: GraphTopology; layout: ServiceBlueprintGraphLayout } {
  const topology = computeTopology(serviceBlueprint, availableQueues);
  return { topology, layout: mergeLayout(topology, serviceBlueprint?.layout) };
}
