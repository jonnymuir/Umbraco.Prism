import { MarkerType, type Edge, type Node } from '@xyflow/react';
import type { AuthoredWorkflow, RouteView } from '../types.js';
import type { GraphProps } from './graph-callbacks.js';
import { declutterChips, type ChipBox } from './chip-declutter.js';
import {
  computeWorkflowGraphLayout,
  parseGraphNodeId,
  LANE_HEADER_OFFSET,
  TOP_PADDING,
  type GatewayTopologyNode,
  type GraphTopology,
  type GraphTopologyEdge,
  type LaneGeometry,
  type NodePlacement,
  type StageTopologyNode,
  type WorkflowGraphLayout,
} from './workflow-graph-layout.js';

const EDGE_ARROW_COLOR = '#6b7280';
const CHIP_WIDTH = 92;
const CHIP_HEIGHT = 24;
const CHIP_STACK_PITCH = 26;

export type StageNodeData = {
  node: StageTopologyNode;
  rowRank: number;
  selected: boolean;
  simulationPath: boolean;
  simulationCurrent: boolean;
  readOnly: boolean;
  [key: string]: unknown;
};

export type GatewayNodeData = {
  node: GatewayTopologyNode;
  rowRank: number;
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
  layout: WorkflowGraphLayout;
};

function labelForNodeKey(workflow: AuthoredWorkflow | null, key: string): string {
  return workflow?.states.find(stage => stage.stateKey === key)?.displayName
    ?? workflow?.metadata?.gateways?.find(gateway => gateway.key === key)?.displayName
    ?? key;
}

function transitionDescriptor(workflow: AuthoredWorkflow | null, transition: RouteView): string {
  return `${labelForNodeKey(workflow, transition.fromStage)} to ${labelForNodeKey(workflow, transition.toStage)}`;
}

type EdgeHandles = { sourceHandle: string; targetHandle: string };

/**
 * Nodes only expose a Top/Bottom pair by default, matching the usual
 * top-to-bottom rank flow. Once a manual drag (or an unusual layout) puts a
 * connected node beside — rather than below — its neighbour, forcing the
 * edge through Top/Bottom produces a long detour through empty canvas
 * instead of a short direct line. When the relationship is predominantly
 * horizontal, route through the Left/Right pair instead. Backward
 * (loop-back) edges keep Top/Bottom — their looping visual is intentional,
 * not a routing failure.
 */
function pickEdgeHandles(
  from: NodePlacement | undefined,
  to: NodePlacement | undefined,
  backward: boolean
): EdgeHandles {
  if (!backward && from && to) {
    const dx = (to.x + to.width / 2) - (from.x + from.width / 2);
    const dy = (to.y + to.height / 2) - (from.y + from.height / 2);
    if (Math.abs(dx) > Math.abs(dy)) {
      return dx >= 0
        ? { sourceHandle: 'out-right', targetHandle: 'in-left' }
        : { sourceHandle: 'out-left', targetHandle: 'in-right' };
    }
  }
  return { sourceHandle: 'out', targetHandle: 'in' };
}

type Point = { x: number; y: number };

/** Where a given handle id actually sits on a placed node, in flow space. */
function handlePoint(node: NodePlacement, handleId: string): Point {
  switch (handleId) {
    case 'out-left':
    case 'in-left':
      return { x: node.x, y: node.y + node.height / 2 };
    case 'out-right':
    case 'in-right':
      return { x: node.x + node.width, y: node.y + node.height / 2 };
    case 'in':
      return { x: node.x + node.width / 2, y: node.y };
    default:
      return { x: node.x + node.width / 2, y: node.y + node.height };
  }
}

function midpoint(a: Point, b: Point): Point {
  return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
}

export function buildGraphModel(props: GraphProps): GraphModel {
  const { topology, layout } = computeWorkflowGraphLayout(props.workflow, props.availableQueues);
  const simulationTransitionIndices = new Set(props.simulationPathTransitionIndices);
  const simulationStageKeys = new Set(props.simulationPathStageKeys);

  const nodes: GraphFlowNode[] = topology.nodes.map(topologyNode => {
    const placement = layout.placements.get(topologyNode.id);
    const position = placement ? { x: placement.x, y: placement.y } : { x: 0, y: 0 };
    const rowRank = placement?.rowRank ?? 0;
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
        selected: props.selectedGatewayKey === topologyNode.gateway.key,
        readOnly: props.readOnly,
        routeCount: routes.length,
        triggerLabel: pillRoute?.trigger ?? '',
        conditionLabel: condition && condition.length > 0 ? condition : null,
      },
    } satisfies GatewayFlowNode;
  });

  // Handle assignment + a natural anchor point per edge, computed once up
  // front so both the edges themselves and their chips (below) can use it.
  const routingByEdgeKey = new Map<string, EdgeHandles & { anchor: Point }>();
  topology.edges.forEach(topologyEdge => {
    const fromPlacement = layout.placements.get(topologyEdge.fromId);
    const toPlacement = layout.placements.get(topologyEdge.toId);
    const handles = pickEdgeHandles(fromPlacement, toPlacement, topologyEdge.backward);
    const anchor = fromPlacement && toPlacement
      ? midpoint(handlePoint(fromPlacement, handles.sourceHandle), handlePoint(toPlacement, handles.targetHandle))
      : { x: 0, y: 0 };
    routingByEdgeKey.set(topologyEdge.key, { ...handles, anchor });
  });

  const chipsByEdgeKey = new Map<string, TransitionChip[]>();
  topology.transitionBindings.forEach(binding => {
    if (!binding.edgeKey) {
      return;
    }
    const chip: TransitionChip = {
      index: binding.index,
      label: binding.transition.action,
      ariaLabel: `Transition ${binding.transition.action}, ${transitionDescriptor(props.workflow, binding.transition)}`,
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
