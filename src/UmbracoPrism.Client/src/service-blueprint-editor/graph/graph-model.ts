import { MarkerType, type Edge, type Node } from '@xyflow/react';
import type { AuthoredServiceBlueprint, RouteView } from '../types.js';
import type { GraphProps } from './graph-callbacks.js';
import { declutterChips, type ChipBox } from './chip-declutter.js';
import {
  computeServiceBlueprintGraphLayout,
  parseGraphNodeId,
  LANE_HEADER_OFFSET,
  TOP_PADDING,
  type GatewayTopologyNode,
  type GraphTopology,
  type GraphTopologyEdge,
  type LaneGeometry,
  type NodePlacement,
  type StageTopologyNode,
  type ServiceBlueprintGraphLayout,
} from './service-blueprint-graph-layout.js';

const EDGE_ARROW_COLOR = '#6b7280';
const CHIP_WIDTH = 92;
const CHIP_HEIGHT = 24;
const CHIP_STACK_PITCH = 26;

export type HandleSide = 'top' | 'bottom' | 'left' | 'right';
export type HandleSlot = { id: string; side: HandleSide; offset: number };
type EdgeHandles = { sourceHandle: string; targetHandle: string };

export type StageNodeData = {
  node: StageTopologyNode;
  rowRank: number;
  sourceHandles: HandleSlot[];
  targetHandles: HandleSlot[];
  selected: boolean;
  simulationPath: boolean;
  simulationCurrent: boolean;
  readOnly: boolean;
  [key: string]: unknown;
};

export type GatewayNodeData = {
  node: GatewayTopologyNode;
  rowRank: number;
  sourceHandles: HandleSlot[];
  targetHandles: HandleSlot[];
  selected: boolean;
  readOnly: boolean;
  routeCount: number;
  triggerLabel: string;
  conditionLabel: string | null;
  [key: string]: unknown;
};

export type TransitionChip = {
  index: number;
  label: string;
  ariaLabel: string;
  fromKey: string;
  toKey: string;
  selected: boolean;
  simulationPath: boolean;
  branch: boolean;
  merge: boolean;
  /** Flow-space anchor, pre-resolved to avoid overlapping other chips or node bodies — see declutterChips. */
  x: number;
  y: number;
};

export type RouteEdgeData = {
  edge: GraphTopologyEdge;
  fromKey: string;
  toKey: string;
  simulationPath: boolean;
  chips: TransitionChip[];
  readOnly: boolean;
  [key: string]: unknown;
};

export type StageFlowNode = Node<StageNodeData, 'stage'>;
export type GatewayFlowNode = Node<GatewayNodeData, 'gateway'>;
export type GraphFlowNode = StageFlowNode | GatewayFlowNode;
export type RouteFlowEdge = Edge<RouteEdgeData, 'route'>;

export type GraphModel = {
  nodes: GraphFlowNode[];
  edges: RouteFlowEdge[];
  lanes: LaneGeometry[];
  bounds: { width: number; height: number };
  topology: GraphTopology;
  layout: ServiceBlueprintGraphLayout;
};

function labelForNodeKey(serviceBlueprint: AuthoredServiceBlueprint | null, key: string): string {
  return serviceBlueprint?.stages.find(stage => stage.stateKey === key)?.displayName
    ?? serviceBlueprint?.metadata?.gateways?.find(gateway => gateway.key === key)?.displayName
    ?? key;
}

function transitionDescriptor(serviceBlueprint: AuthoredServiceBlueprint | null, transition: RouteView): string {
  return `${labelForNodeKey(serviceBlueprint, transition.fromStage)} to ${labelForNodeKey(serviceBlueprint, transition.toStage)}`;
}

/**
 * Nodes route through the Top/Bottom side by default, matching the usual
 * top-to-bottom rank flow. Once a manual drag (or an unusual layout) puts a
 * connected node beside — rather than below — its neighbour, forcing the
 * edge through Top/Bottom produces a long detour through empty canvas
 * instead of a short direct line. When the relationship is predominantly
 * horizontal, route through the Left/Right side instead. Backward
 * (loop-back) edges keep Top/Bottom — their looping visual is intentional,
 * not a routing failure.
 */
function pickEdgeSides(
  from: NodePlacement | undefined,
  to: NodePlacement | undefined,
  backward: boolean
): { sourceSide: HandleSide; targetSide: HandleSide } {
  if (!backward && from && to) {
    const dx = (to.x + to.width / 2) - (from.x + from.width / 2);
    const dy = (to.y + to.height / 2) - (from.y + from.height / 2);
    if (Math.abs(dx) > Math.abs(dy)) {
      return dx >= 0
        ? { sourceSide: 'right', targetSide: 'left' }
        : { sourceSide: 'left', targetSide: 'right' };
    }
  }
  return { sourceSide: 'bottom', targetSide: 'top' };
}

type Point = { x: number; y: number };

/** Where a fractional offset along a given node side sits, in flow space. */
function handlePoint(node: NodePlacement, side: HandleSide, offset: number): Point {
  switch (side) {
    case 'left':
      return { x: node.x, y: node.y + node.height * offset };
    case 'right':
      return { x: node.x + node.width, y: node.y + node.height * offset };
    case 'top':
      return { x: node.x + node.width * offset, y: node.y };
    default:
      return { x: node.x + node.width * offset, y: node.y + node.height };
  }
}

function midpoint(a: Point, b: Point): Point {
  return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
}

type EdgeHandleAssignment = {
  sourceHandle: string;
  sourceSide: HandleSide;
  sourceOffset: number;
  targetHandle: string;
  targetSide: HandleSide;
  targetOffset: number;
};

type HandleAssignment = {
  edgeHandles: Map<string, EdgeHandleAssignment>;
  /** Every distinct source/target handle a node needs to render, keyed by node id. */
  nodeHandles: Map<string, { source: HandleSlot[]; target: HandleSlot[] }>;
};

/**
 * Every node exposed exactly one Top/Bottom/Left/Right anchor point, shared
 * by every edge that happened to leave or arrive on that side. When a node
 * genuinely fans out (or in) to several others on the same side, their edges
 * all started from the identical pixel and stayed coincident until they
 * neared their distinct targets — reading as one path, or a route "ghosting"
 * through a sibling's chip, instead of the distinct branches the service blueprint
 * JSON actually describes. This spreads same-side edges across evenly spaced
 * slots along that side instead, ordered by where the edge's other endpoint
 * sits so the fan reads left-to-right in a sane order. A lone edge on a side
 * still lands at offset 0.5 — the original centred position — so ordinary
 * linear flows are unaffected.
 */
function assignHandleSlots(
  edges: { key: string; fromId: string; toId: string; sourceSide: HandleSide; targetSide: HandleSide }[],
  placements: Map<string, NodePlacement>
): HandleAssignment {
  const otherEndpointCoordinate = (side: HandleSide, placement: NodePlacement | undefined): number => {
    if (!placement) {
      return 0;
    }
    return side === 'left' || side === 'right'
      ? placement.y + placement.height / 2
      : placement.x + placement.width / 2;
  };

  type GroupEntry = { edgeKey: string; sortKey: number };
  type GroupMap = Map<string, Map<HandleSide, GroupEntry[]>>;
  const pushEntry = (groups: GroupMap, nodeId: string, side: HandleSide, entry: GroupEntry) => {
    const bySide = groups.get(nodeId) ?? new Map<HandleSide, GroupEntry[]>();
    groups.set(nodeId, bySide);
    const group = bySide.get(side) ?? [];
    bySide.set(side, group);
    group.push(entry);
  };

  const sourceGroups: GroupMap = new Map();
  const targetGroups: GroupMap = new Map();
  edges.forEach(edge => {
    pushEntry(sourceGroups, edge.fromId, edge.sourceSide, {
      edgeKey: edge.key,
      sortKey: otherEndpointCoordinate(edge.sourceSide, placements.get(edge.toId)),
    });
    pushEntry(targetGroups, edge.toId, edge.targetSide, {
      edgeKey: edge.key,
      sortKey: otherEndpointCoordinate(edge.targetSide, placements.get(edge.fromId)),
    });
  });

  const resolve = (groups: GroupMap, rolePrefix: string): Map<string, { id: string; offset: number }> => {
    const slotByEdgeKey = new Map<string, { id: string; offset: number }>();
    groups.forEach(bySide => {
      bySide.forEach((group, side) => {
        const ordered = [...group].sort((left, right) => left.sortKey - right.sortKey
          || left.edgeKey.localeCompare(right.edgeKey));
        ordered.forEach((entry, index) => {
          slotByEdgeKey.set(entry.edgeKey, {
            id: `${rolePrefix}-${side}-${index}`,
            offset: (index + 1) / (ordered.length + 1),
          });
        });
      });
    });
    return slotByEdgeKey;
  };

  const sourceSlotByEdgeKey = resolve(sourceGroups, 'src');
  const targetSlotByEdgeKey = resolve(targetGroups, 'tgt');

  const edgeHandles = new Map<string, EdgeHandleAssignment>();
  const nodeHandles = new Map<string, { source: HandleSlot[]; target: HandleSlot[] }>();
  const nodeEntry = (nodeId: string) => {
    let entry = nodeHandles.get(nodeId);
    if (!entry) {
      entry = { source: [], target: [] };
      nodeHandles.set(nodeId, entry);
    }
    return entry;
  };

  edges.forEach(edge => {
    const sourceSlot = sourceSlotByEdgeKey.get(edge.key)!;
    const targetSlot = targetSlotByEdgeKey.get(edge.key)!;
    edgeHandles.set(edge.key, {
      sourceHandle: sourceSlot.id,
      sourceSide: edge.sourceSide,
      sourceOffset: sourceSlot.offset,
      targetHandle: targetSlot.id,
      targetSide: edge.targetSide,
      targetOffset: targetSlot.offset,
    });
    nodeEntry(edge.fromId).source.push({ id: sourceSlot.id, side: edge.sourceSide, offset: sourceSlot.offset });
    nodeEntry(edge.toId).target.push({ id: targetSlot.id, side: edge.targetSide, offset: targetSlot.offset });
  });

  return { edgeHandles, nodeHandles };
}

export function buildGraphModel(props: GraphProps): GraphModel {
  const { topology, layout } = computeServiceBlueprintGraphLayout(props.serviceBlueprint, props.availableQueues);
  const simulationTransitionIndices = new Set(props.simulationPathTransitionIndices);
  const simulationStageKeys = new Set(props.simulationPathStageKeys);

  // Handle assignment + a natural anchor point per edge, computed once up
  // front so nodes (their rendered handles), edges, and chips (below) can
  // all use it.
  const edgesForSlotting = topology.edges.map(topologyEdge => {
    const sides = pickEdgeSides(
      layout.placements.get(topologyEdge.fromId),
      layout.placements.get(topologyEdge.toId),
      topologyEdge.backward
    );
    return { key: topologyEdge.key, fromId: topologyEdge.fromId, toId: topologyEdge.toId, ...sides };
  });
  const { edgeHandles, nodeHandles } = assignHandleSlots(edgesForSlotting, layout.placements);
  const emptyHandles = { source: [] as HandleSlot[], target: [] as HandleSlot[] };

  const nodes: GraphFlowNode[] = topology.nodes.map(topologyNode => {
    const placement = layout.placements.get(topologyNode.id);
    const position = placement ? { x: placement.x, y: placement.y } : { x: 0, y: 0 };
    const rowRank = placement?.rowRank ?? 0;
    const handles = nodeHandles.get(topologyNode.id) ?? emptyHandles;
    // Draggability, selectability (shift-marquee multi-drag), and
    // connectability are governed by the ReactFlow-level flags driven by
    // readOnly rather than per node.
    const common = {
      id: topologyNode.id,
      position,
      width: topologyNode.width,
      height: topologyNode.height,
      focusable: false,
    } as const;

    if (topologyNode.kind === 'stage') {
      return {
        ...common,
        type: 'stage',
        data: {
          node: topologyNode,
          rowRank,
          sourceHandles: handles.source,
          targetHandles: handles.target,
          selected: props.selectedStageKey === topologyNode.stage.stateKey,
          simulationPath: simulationStageKeys.has(topologyNode.stage.stateKey),
          simulationCurrent: props.simulationCurrentStageKey === topologyNode.stage.stateKey,
          readOnly: props.readOnly,
        },
      } satisfies StageFlowNode;
    }

    const routes = topologyNode.gateway.routes ?? [];
    const pillRoute = topologyNode.pill ? routes[0] : undefined;
    const condition = pillRoute?.condition?.trim();
    return {
      ...common,
      type: 'gateway',
      data: {
        node: topologyNode,
        rowRank,
        sourceHandles: handles.source,
        targetHandles: handles.target,
        selected: props.selectedGatewayKey === topologyNode.gateway.key,
        readOnly: props.readOnly,
        routeCount: routes.length,
        triggerLabel: pillRoute?.trigger ?? '',
        conditionLabel: condition && condition.length > 0 ? condition : null,
      },
    } satisfies GatewayFlowNode;
  });

  const routingByEdgeKey = new Map<string, EdgeHandles & { anchor: Point }>();
  topology.edges.forEach(topologyEdge => {
    const fromPlacement = layout.placements.get(topologyEdge.fromId);
    const toPlacement = layout.placements.get(topologyEdge.toId);
    const handles = edgeHandles.get(topologyEdge.key)!;
    const anchor = fromPlacement && toPlacement
      ? midpoint(
        handlePoint(fromPlacement, handles.sourceSide, handles.sourceOffset),
        handlePoint(toPlacement, handles.targetSide, handles.targetOffset)
      )
      : { x: 0, y: 0 };
    routingByEdgeKey.set(topologyEdge.key, {
      sourceHandle: handles.sourceHandle,
      targetHandle: handles.targetHandle,
      anchor,
    });
  });

  const chipsByEdgeKey = new Map<string, TransitionChip[]>();
  topology.transitionBindings.forEach(binding => {
    if (!binding.edgeKey) {
      return;
    }
    const chip: TransitionChip = {
      index: binding.index,
      label: binding.transition.action,
      ariaLabel: `Transition ${binding.transition.action}, ${transitionDescriptor(props.serviceBlueprint, binding.transition)}`,
      fromKey: parseGraphNodeId(binding.visualFromId).key,
      toKey: parseGraphNodeId(binding.visualToId).key,
      selected: props.selectedTransitionIndex === binding.index,
      simulationPath: simulationTransitionIndices.has(binding.index),
      branch: binding.branch,
      merge: binding.merge,
      x: 0,
      y: 0,
    };
    chipsByEdgeKey.set(binding.edgeKey, [...(chipsByEdgeKey.get(binding.edgeKey) ?? []), chip]);
  });

  // Seed each chip at its edge's anchor (chips sharing an edge stack
  // vertically around it, as before), then let every chip in the graph
  // settle apart from every other chip and every node body — real fan-out
  // and fan-in gateways otherwise pile several edges' anchors on top of one
  // another and on top of the gateway itself.
  const chipBoxes: ChipBox[] = [];
  chipsByEdgeKey.forEach((chips, edgeKey) => {
    const anchor = routingByEdgeKey.get(edgeKey)?.anchor ?? { x: 0, y: 0 };
    chips.forEach((chip, slot) => {
      const offsetY = (slot - (chips.length - 1) / 2) * CHIP_STACK_PITCH;
      chipBoxes.push({
        id: String(chip.index),
        x: anchor.x - CHIP_WIDTH / 2,
        y: anchor.y + offsetY - CHIP_HEIGHT / 2,
        width: CHIP_WIDTH,
        height: CHIP_HEIGHT,
      });
    });
  });
  // Lane header text (label + description, up top in each lane) isn't a
  // node placement, but a chip landing there is just as unreadable as one
  // landing on a node — keep chips out of that band too.
  const headerObstacles = layout.lanes.map(lane => ({
    x: lane.x,
    y: TOP_PADDING,
    width: lane.width,
    height: LANE_HEADER_OFFSET,
  }));
  const obstacles = [
    ...[...layout.placements.values()].map(placement => ({
      x: placement.x,
      y: placement.y,
      width: placement.width,
      height: placement.height,
    })),
    ...headerObstacles,
  ];
  const resolvedChipBoxes = declutterChips(chipBoxes, obstacles);
  chipsByEdgeKey.forEach(chips => {
    chips.forEach(chip => {
      const box = resolvedChipBoxes.get(String(chip.index));
      if (box) {
        chip.x = box.x + CHIP_WIDTH / 2;
        chip.y = box.y + CHIP_HEIGHT / 2;
      }
    });
  });

  const edges: RouteFlowEdge[] = topology.edges.map(topologyEdge => {
    const simulationPath = topologyEdge.transitionIndices.some(index => simulationTransitionIndices.has(index));
    const { sourceHandle, targetHandle } = routingByEdgeKey.get(topologyEdge.key)!;
    return {
      id: topologyEdge.key,
      source: topologyEdge.fromId,
      target: topologyEdge.toId,
      sourceHandle,
      targetHandle,
      type: 'route',
      focusable: false,
      selectable: false,
      animated: simulationPath,
      markerEnd: { type: MarkerType.ArrowClosed, color: EDGE_ARROW_COLOR },
      data: {
        edge: topologyEdge,
        fromKey: parseGraphNodeId(topologyEdge.fromId).key,
        toKey: parseGraphNodeId(topologyEdge.toId).key,
        simulationPath,
        chips: chipsByEdgeKey.get(topologyEdge.key) ?? [],
        readOnly: props.readOnly,
      },
    } satisfies RouteFlowEdge;
  });

  return { nodes, edges, lanes: layout.lanes, bounds: layout.bounds, topology, layout };
}
